using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using Pointly.App.Interop;

namespace Pointly.App.Capture;

/// <summary>
/// Raw COM/WinRT interop needed for Windows.Graphics.Capture without extra
/// NuGet packages. All signatures follow the official Microsoft screen-capture
/// sample (d3d11.dll exports, well-known interface GUIDs).
/// </summary>
internal static class GraphicsCaptureInterop
{
    // IGraphicsCaptureItemInterop IID (windows.graphics.capture.interop.h).
    // Invoked via raw vtable dispatch (slot 3) rather than a ComImport stub:
    // deterministic across .NET interop generations, no RCW/cast behavior involved.
    private static readonly Guid IGraphicsCaptureItemInteropId = new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");
    // IID_IDXGIDevice from the Windows SDK's dxgi.h.
    private static readonly Guid IDXGIDeviceId = new("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
    // IMemoryBufferByteAccess IID from the Windows SDK's MemoryBuffer.h.
    private static readonly Guid IMemoryBufferByteAccessId = new("5b0d3235-4dba-4d44-865e-8f1d0e4fd04d");

    private const string GraphicsCaptureItemClassId = "Windows.Graphics.Capture.GraphicsCaptureItem";

    private const int D3D_DRIVER_TYPE_HARDWARE = 1;
    private const int D3D_DRIVER_TYPE_WARP = 5;
    private const uint D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20;
    private const uint D3D11_SDK_VERSION = 7;
    private const uint DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmGetWindowAttribute(
        IntPtr hwnd, uint attribute, out NativeMethods.RECT bounds, uint size);

    /// <summary>Visible window bounds in physical pixels, excluding invisible resize borders.</summary>
    public static NativeMethods.RECT GetCaptureBounds(nint hwnd)
    {
        int hr = DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS,
            out NativeMethods.RECT bounds, (uint)Marshal.SizeOf<NativeMethods.RECT>());
        if (hr != 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new WindowCaptureException($"Could not get visible window bounds (HR=0x{hr:X8}).");
        }

        return bounds;
    }

    // IMemoryBufferByteAccess::GetBuffer (slot 3). Raw dispatch, same rationale
    // as the capture-item factory above.
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetBufferDelegate(IntPtr self, out IntPtr buffer, out uint capacity);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsCreateString(
        [MarshalAs(UnmanagedType.LPWStr)] string sourceString,
        uint length,
        out IntPtr hstring);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsDeleteString(IntPtr hstring);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int RoGetActivationFactory(IntPtr activatableClassId, [In] ref Guid iid, out IntPtr factory);

    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int D3D11CreateDevice(
        IntPtr adapter,
        int driverType,
        IntPtr software,
        uint flags,
        int[]? featureLevels,
        uint featureLevelsCount,
        uint sdkVersion,
        out IntPtr device,
        out int featureLevel,
        out IntPtr immediateContext);

    [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", ExactSpelling = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int QueryInterfaceDelegate(IntPtr self, ref Guid iid, out IntPtr ppv);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateForWindowDelegate(IntPtr self, IntPtr hwnd, ref Guid iid, out IntPtr result);

    /// <summary>Raw IUnknown::QueryInterface (slot 0) without managed helpers.</summary>
    private static int RawQueryInterface(IntPtr obj, Guid iid, out IntPtr ppv)
    {
        IntPtr fn = Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj), 0);
        var qi = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(fn);
        return qi(obj, ref iid, out ppv);
    }

    /// <summary>Creates a GraphicsCaptureItem for a top-level window HWND.</summary>
    public static IntPtr CreateItemForWindow(nint hwnd)
    {
        Guid iid = IGraphicsCaptureItemInteropId;
        int hr = WindowsCreateString(GraphicsCaptureItemClassId, (uint)GraphicsCaptureItemClassId.Length, out IntPtr classId);
        if (hr != 0 || classId == IntPtr.Zero)
        {
            throw new WindowCaptureException($"WindowsCreateString failed (HR=0x{hr:X8}).");
        }

        try
        {
            hr = RoGetActivationFactory(classId, ref iid, out IntPtr factory);
            if (hr != 0 || factory == IntPtr.Zero)
            {
                throw new WindowCaptureException($"RoGetActivationFactory failed (HR=0x{hr:X8}).");
            }

            try
            {
                // Slot 3 = CreateForWindow(HWND, REFIID, void**). Raw dispatch
                // avoids ComImport-stub variability for this classic-COM factory.
                IntPtr fn = Marshal.ReadIntPtr(Marshal.ReadIntPtr(factory), 3 * IntPtr.Size);
                var createForWindow = Marshal.GetDelegateForFunctionPointer<CreateForWindowDelegate>(fn);

                // NOTE: riid must be an *interface* IID the item supports:
                // IID_IGraphicsCaptureItem = 79C3F95B-31F7-4EC2-A464-632EF5D30760
                // (windows.graphics.capture.idl). typeof(GraphicsCaptureItem).GUID
                // is a CsWinRT class GUID and fails the internal QI with
                // E_NOINTERFACE; the IGraphicsCaptureItem projection is internal,
                // so the IID is pinned here instead of read from the type.
                Guid itemIid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
                hr = createForWindow(factory, hwnd, ref itemIid, out IntPtr item);
                if (hr != 0 || item == IntPtr.Zero)
                {
                    throw new WindowCaptureException($"CreateForWindow failed (HR=0x{hr:X8}, riid={itemIid}).");
                }

                return item;
            }
            finally
            {
                Marshal.Release(factory);
            }
        }
        finally
        {
            WindowsDeleteString(classId);
        }
    }

    /// <summary>
    /// Creates a BGRA-capable D3D11 device and validates it exposes IDXGIDevice.
    /// Tries hardware first, then WARP, so capture also works without a GPU.
    /// Returns raw device + context + DXGI pointers; caller owns all three.
    /// </summary>
    public static void CreateD3DDevice(out IntPtr device, out IntPtr context, out IntPtr dxgiDevice)
    {
        var attempts = new System.Collections.Generic.List<string>();
        int[] driverTypes = [D3D_DRIVER_TYPE_HARDWARE, D3D_DRIVER_TYPE_WARP];
        foreach (int driverType in driverTypes)
        {
            string driverName = driverType == D3D_DRIVER_TYPE_HARDWARE ? "hw" : "warp";
            IntPtr candidate = IntPtr.Zero;
            IntPtr candidateCtx = IntPtr.Zero;
            IntPtr candidateDxgi = IntPtr.Zero;
            try
            {
                int hr = D3D11CreateDevice(
                    IntPtr.Zero, driverType, IntPtr.Zero,
                    D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                    null, 0, D3D11_SDK_VERSION,
                    out candidate, out _, out candidateCtx);
                if (hr != 0 || candidate == IntPtr.Zero)
                {
                    attempts.Add($"{driverName}:create=0x{hr:X8}");
                    continue;
                }

                hr = RawQueryInterface(candidate, IDXGIDeviceId, out candidateDxgi);
                if (hr != 0 || candidateDxgi == IntPtr.Zero)
                {
                    attempts.Add($"{driverName}:create=0x0,qi=0x{hr:X8}");
                    continue;
                }

                device = candidate;
                context = candidateCtx;
                dxgiDevice = candidateDxgi;
                return;
            }
            finally
            {
                // Release anything not handed to the caller.
                if (candidateDxgi == IntPtr.Zero || candidate == IntPtr.Zero || candidateCtx == IntPtr.Zero)
                {
                    if (candidateDxgi != IntPtr.Zero)
                    {
                        Marshal.Release(candidateDxgi);
                    }

                    if (candidate != IntPtr.Zero)
                    {
                        Marshal.Release(candidate);
                    }

                    if (candidateCtx != IntPtr.Zero)
                    {
                        Marshal.Release(candidateCtx);
                    }
                }
            }
        }

        device = IntPtr.Zero;
        context = IntPtr.Zero;
        dxgiDevice = IntPtr.Zero;
        throw new WindowCaptureException($"No usable D3D11 device ({string.Join(";", attempts)}).");
    }

    /// <summary>
    /// Returns the raw byte pointer + capacity of an IMemoryBufferReference's
    /// backing memory. Caller must release the reference afterwards as usual.
    /// </summary>
    public static void GetBufferBytes(IMemoryBufferReference bufferReference, out IntPtr data, out uint capacity)
    {
        IntPtr reference = WinRT.MarshalInterface<IMemoryBufferReference>.FromManaged(bufferReference);
        IntPtr byteAccess = IntPtr.Zero;
        try
        {
            int hr = RawQueryInterface(reference, IMemoryBufferByteAccessId, out byteAccess);
            if (hr != 0 || byteAccess == IntPtr.Zero)
            {
                throw new WindowCaptureException($"IMemoryBufferByteAccess query failed (HR=0x{hr:X8}).");
            }

            // Slot 3 is GetBuffer only on IMemoryBufferByteAccess, not on IUnknown.
            IntPtr fn = Marshal.ReadIntPtr(Marshal.ReadIntPtr(byteAccess), 3 * IntPtr.Size);
            var getBuffer = Marshal.GetDelegateForFunctionPointer<GetBufferDelegate>(fn);
            hr = getBuffer(byteAccess, out data, out capacity);
            if (hr != 0 || data == IntPtr.Zero)
            {
                throw new WindowCaptureException($"Bitmap buffer access failed (HR=0x{hr:X8}).");
            }
        }
        finally
        {
            if (byteAccess != IntPtr.Zero)
            {
                Marshal.Release(byteAccess);
            }

            Marshal.Release(reference);
        }
    }

    /// <summary>
    /// Wraps a native DXGI device as a WinRT IDirect3DDevice for the frame pool.
    /// Releases all three raw pointers on success; caller releases them on failure.
    /// </summary>
    public static IDirect3DDevice WrapDevice(IntPtr device, IntPtr context, IntPtr dxgiDevice)
    {
        int hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out IntPtr abi);
        if (hr != 0 || abi == IntPtr.Zero)
        {
            throw new WindowCaptureException($"CreateDirect3D11DeviceFromDXGIDevice failed (HR=0x{hr:X8}).");
        }

        IDirect3DDevice wrapped;
        try
        {
            // FromAbi takes ownership of the native reference (see item path).
            wrapped = WinRT.MarshalInterface<IDirect3DDevice>.FromAbi(abi);
            abi = IntPtr.Zero;
        }
        finally
        {
            if (abi != IntPtr.Zero)
            {
                Marshal.Release(abi);
            }
        }

        if (wrapped is null)
        {
            throw new WindowCaptureException("Could not project IDirect3DDevice.");
        }

        // The WinRT wrappers hold their own references now.
        Marshal.Release(dxgiDevice);
        Marshal.Release(device);
        Marshal.Release(context);
        return wrapped;
    }
}
