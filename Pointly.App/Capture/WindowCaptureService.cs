using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Pointly.App.Interop;

namespace Pointly.App.Capture;

/// <summary>
/// One-shot foreground-window capture via Windows.Graphics.Capture.
/// Capture runs on a thread-pool thread; a free-threaded frame pool means no
/// UI dispatcher is involved and nothing streams or polls continuously.
/// </summary>
public sealed class WindowCaptureService
{
    private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Captures <paramref name="hwnd"/> once and returns packed BGRA32 pixels.
    /// Throws <see cref="WindowCaptureException"/> for expected failures
    /// (window gone, minimized, zero area, unsupported, timeout).
    /// </summary>
    public Task<WindowCaptureResult> CaptureOnceAsync(
        nint hwnd, CancellationToken cancellationToken = default) =>
        Task.Run(() => CaptureCore(hwnd, cancellationToken), cancellationToken);

    private static WindowCaptureResult CaptureCore(nint hwnd, CancellationToken cancellationToken)
    {
        var totalSw = Stopwatch.StartNew();
        var initSw = Stopwatch.StartNew();

        cancellationToken.ThrowIfCancellationRequested();

        if (!NativeMethods.IsWindow(hwnd))
        {
            throw new WindowCaptureException("Window no longer exists.");
        }

        if (NativeMethods.IsIconic(hwnd))
        {
            throw new WindowCaptureException("Window is minimized; nothing to capture.");
        }

        // Capture excludes the invisible resize borders included by GetWindowRect.
        NativeMethods.RECT rect = GraphicsCaptureInterop.GetCaptureBounds(hwnd);
        var windowRect = new Int32Rect(rect.Left, rect.Top, rect.Width, rect.Height);
        double dpiScale = NativeMethods.GetDpiForWindow(hwnd) / 96.0;

        if (!GraphicsCaptureSession.IsSupported())
        {
            throw new WindowCaptureException("Windows.Graphics.Capture is not supported on this system.");
        }

        GraphicsCaptureItem item;
        try
        {
            IntPtr itemPtr = GraphicsCaptureInterop.CreateItemForWindow(hwnd);
            try
            {
                // FromAbi takes ownership of the native reference — do not
                // Marshal.Release it afterwards (that would double-release).
                item = WinRT.MarshalInterface<GraphicsCaptureItem>.FromAbi(itemPtr);
                itemPtr = IntPtr.Zero;
            }
            finally
            {
                if (itemPtr != IntPtr.Zero)
                {
                    Marshal.Release(itemPtr);
                }
            }

            if (item is null)
            {
                throw new WindowCaptureException("Could not project GraphicsCaptureItem.");
            }
        }
        catch (WindowCaptureException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new WindowCaptureException("Failed to create capture item for window.", ex);
        }

        if (item.Size.Width <= 0 || item.Size.Height <= 0)
        {
            throw new WindowCaptureException("Capture item reports zero size (window closing or occluded).");
        }

        GraphicsCaptureInterop.CreateD3DDevice(out IntPtr rawDevice, out IntPtr rawContext, out IntPtr rawDxgi);
        IDirect3DDevice device;
        try
        {
            device = GraphicsCaptureInterop.WrapDevice(rawDevice, rawContext, rawDxgi);
        }
        catch
        {
            Marshal.Release(rawDxgi);
            Marshal.Release(rawDevice);
            Marshal.Release(rawContext);
            throw;
        }

        // Free-threaded pool: frame events arrive on any thread, so no
        // DispatcherQueue is required and the UI thread is never blocked.
        using var pool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, item.Size);
        using var session = pool.CreateCaptureSession(item);
        session.IsCursorCaptureEnabled = false;

        var frameArrived = new TaskCompletionSource<Direct3D11CaptureFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            Direct3D11CaptureFrame frame;
            try
            {
                frame = sender.TryGetNextFrame();
            }
            catch
            {
                return;
            }

            if (frame is null)
            {
                return;
            }

            if (!frameArrived.TrySetResult(frame))
            {
                frame.Dispose();
            }
        }

        pool.FrameArrived += OnFrameArrived;
        try
        {
            session.StartCapture();
        }
        catch (Exception ex)
        {
            throw new WindowCaptureException("Failed to start capture session.", ex);
        }

        initSw.Stop();
        long initMs = initSw.ElapsedMilliseconds;

        var frameSw = Stopwatch.StartNew();
        Direct3D11CaptureFrame? frame = null;
        try
        {
            frame = frameArrived.Task.WaitAsync(FrameTimeout, cancellationToken).GetAwaiter().GetResult();
        }
        catch (TimeoutException)
        {
            throw new WindowCaptureException("Timed out waiting for a capture frame (3 s).");
        }
        catch (OperationCanceledException)
        {
            throw new WindowCaptureException("Capture was cancelled while waiting for a frame.");
        }
        finally
        {
            frameSw.Stop();
        }

        long frameWaitMs = frameSw.ElapsedMilliseconds;
        var timestamp = DateTimeOffset.Now;

        var copySw = Stopwatch.StartNew();
        byte[] pixels;
        int width, height;
        try
        {
            (pixels, width, height) = CopyFramePixels(frame);
        }
        finally
        {
            frame.Dispose();
            copySw.Stop();
        }

        long copyMs = copySw.ElapsedMilliseconds;
        totalSw.Stop();

        // Release session resources deterministically; frame data lives on in the result.
        // (GraphicsCaptureItem has no Close/Dispose; its RCW is reclaimed by GC.)
        session.Dispose();
        pool.Dispose();
        device.Dispose();

        return new WindowCaptureResult(
            hwnd,
            width,
            height,
            timestamp,
            pixels,
            windowRect,
            dpiScale,
            new CaptureTimings(initMs, frameWaitMs, copyMs, totalSw.ElapsedMilliseconds));
    }

    /// <summary>
    /// Copies the frame surface to packed BGRA32 via SoftwareBitmap, which
    /// performs the GPU readback internally — no D3D context vtable needed.
    /// Dimensions come from ContentSize so a mid-capture resize stays truthful.
    /// </summary>
    private static (byte[] Pixels, int Width, int Height) CopyFramePixels(Direct3D11CaptureFrame frame)
    {
        int width = frame.ContentSize.Width;
        int height = frame.ContentSize.Height;
        if (width <= 0 || height <= 0)
        {
            throw new WindowCaptureException("Captured frame has zero content size.");
        }

        using SoftwareBitmap bitmap = SoftwareBitmap
            .CreateCopyFromSurfaceAsync(frame.Surface).AsTask().GetAwaiter().GetResult();
        if (bitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8)
        {
            throw new WindowCaptureException($"Unexpected frame pixel format: {bitmap.BitmapPixelFormat}.");
        }

        using BitmapBuffer buffer = bitmap.LockBuffer(BitmapBufferAccessMode.Read);
        BitmapPlaneDescription plane = buffer.GetPlaneDescription(0);
        int copyWidth = Math.Min(width, plane.Width);
        int copyHeight = Math.Min(height, plane.Height);

        using var reference = buffer.CreateReference();
        GraphicsCaptureInterop.GetBufferBytes(reference, out IntPtr src, out _);

        var pixels = new byte[copyWidth * copyHeight * 4];
        for (int y = 0; y < copyHeight; y++)
        {
            IntPtr srcRow = IntPtr.Add(src, y * plane.Stride);
            Marshal.Copy(srcRow, pixels, y * copyWidth * 4, copyWidth * 4);
        }

        return (pixels, copyWidth, copyHeight);
    }
}
