using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace LocalTutor.Tools.FileInspectionAndConversion;

/// <summary>Trusted host configuration; the model cannot choose an executable, flags, or timeout.</summary>
public sealed class ImageMagickCodec
{
    private readonly string executable;
    private readonly TimeSpan timeout;

    public ImageMagickCodec(string executablePath, TimeSpan? operationTimeout = null)
    {
        if (!Path.IsPathFullyQualified(executablePath) || Path.GetFileName(executablePath).ToLowerInvariant() is not ("magick" or "magick.exe" or "convert" or "convert.exe"))
            throw new ArgumentException("Configure the absolute path to a trusted ImageMagick magick or convert executable.");
        executable = executablePath;
        timeout = operationTimeout ?? TimeSpan.FromSeconds(15);
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(15)) throw new ArgumentOutOfRangeException(nameof(operationTimeout));
    }

    internal async Task<ImageMetadata> InspectAsync(byte[] bytes, ImageFormat format, CancellationToken cancellationToken)
    {
        ImageContent.RejectAnimation(bytes, format);
        byte[] output = await RunAsync(bytes, [ImageContent.Coder(format) + ":-", "-format", "%m|%w|%h|%n|%[channels]|%[colorspace]\n", "info:"], 4096, cancellationToken);
        string[] lines = Encoding.UTF8.GetString(output).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length != 1) throw new FileToolException("UnsupportedAnimation: only one still image is supported.");
        string[] values = lines[0].Split('|');
        if (values.Length != 6 || values[0] != ImageContent.Coder(format) || values[3] != "1" ||
            !int.TryParse(values[1], NumberStyles.None, CultureInfo.InvariantCulture, out int width) ||
            !int.TryParse(values[2], NumberStyles.None, CultureInfo.InvariantCulture, out int height))
            throw new FileToolException("InvalidImage: the decoder did not return valid image metadata.");
        if (width < 1 || height < 1 || width > 8192 || height > 8192 || (long)width * height > ImageContent.MaxPixels)
            throw new FileToolException("ResourceLimit: images are limited to 8192 per side and 4 million pixels.");
        bool alpha = values[4] is "srgba" or "rgba" or "graya" or "cmyka" || values[4].EndsWith("a", StringComparison.Ordinal);
        return new ImageMetadata(width, height, alpha, values[5], ImageContent.HasColorProfile(bytes, format));
    }

    internal Task<byte[]> ConvertAsync(byte[] source, ImageFormat sourceFormat, ConvertImageInput input, CancellationToken cancellationToken)
    {
        List<string> arguments = [ImageContent.Coder(sourceFormat) + ":-", "-auto-orient", "-colorspace", "sRGB"];
        if (input.MaxWidth.HasValue)
            arguments.AddRange(["-resize", FormattableString.Invariant($"{input.MaxWidth}x{input.MaxHeight}>")]);
        if (input.Transparency == TransparencyMode.FlattenWhite)
            arguments.AddRange(["-background", "white", "-alpha", "remove", "-alpha", "off"]);
        arguments.AddRange(["-strip", "-depth", "8"]);
        if (input.Format != ImageFormat.Png) arguments.AddRange(["-quality", (input.Quality ?? 85).ToString(CultureInfo.InvariantCulture)]);
        // Retain full alpha precision while the requested quality controls lossy RGB encoding.
        if (input.Format == ImageFormat.WebP) arguments.AddRange(["-define", "webp:alpha-quality=100"]);
        arguments.Add(ImageContent.Coder(input.Format) + ":-");
        return RunAsync(source, arguments, ImageContent.MaxOutputBytes, cancellationToken);
    }

    private async Task<byte[]> RunAsync(byte[] input, IEnumerable<string> arguments, int maxOutput, CancellationToken cancellationToken)
    {
        if (!File.Exists(executable)) throw new FileToolException("MissingDependency: configure an installed local ImageMagick with PNG, JPEG, and WebP codecs.");
        // ImageMagick may spool stdin even with the pixel disk cache disabled. Keep that
        // transient copy in an isolated directory and remove it after the worker exits.
        string scratch = Path.Combine(Path.GetTempPath(), "localtutor-codec-" + Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(scratch);
        else Directory.CreateDirectory(scratch, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        CancellationToken token = deadline.Token;
        ProcessStartInfo start = new(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.WorkingDirectory = scratch;
        foreach (string variable in new[] { "MAGICK_TEMPORARY_PATH", "MAGICK_TMPDIR", "TMPDIR", "TMP", "TEMP" })
            start.Environment[variable] = scratch;
        foreach (string value in new[] { "-limit", "thread", "1", "-limit", "memory", "128MiB", "-limit", "map", "0", "-limit", "disk", "0", "-limit", "width", "8192", "-limit", "height", "8192", "-limit", "time", "15", "-regard-warnings" })
            start.ArgumentList.Add(value);
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = new() { StartInfo = start };
        try
        {
            token.ThrowIfCancellationRequested();
            process.Start();
            using CancellationTokenRegistration stop = token.Register(() => Kill(process));
            Task write = WriteInputAsync();
            Task<byte[]> read = ReadOutputAsync();
            Task discardErrors = process.StandardError.BaseStream.CopyToAsync(Stream.Null, token);
            await Task.WhenAll(write, read, discardErrors, process.WaitForExitAsync(token));
            token.ThrowIfCancellationRequested();
            if (process.ExitCode != 0) throw new FileToolException("CodecFailed: invalid image, unsupported codec, or native resource/policy limit; no output was published.");
            return await read;

            async Task WriteInputAsync()
            {
                try { await process.StandardInput.BaseStream.WriteAsync(input, token); }
                catch (IOException) { /* A rejecting decoder may close stdin early. */ }
                finally { process.StandardInput.Close(); }
            }

            async Task<byte[]> ReadOutputAsync()
            {
                using MemoryStream buffer = new();
                byte[] chunk = new byte[8192];
                int count;
                while ((count = await process.StandardOutput.BaseStream.ReadAsync(chunk, token)) != 0)
                {
                    if (buffer.Length + count > maxOutput)
                    {
                        Kill(process);
                        throw new FileToolException("ResourceLimit: native output exceeded its size budget.");
                    }
                    buffer.Write(chunk, 0, count);
                }
                return buffer.ToArray();
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FileToolException("TimedOut: local image processing exceeded its time budget.");
        }
        catch (Win32Exception)
        {
            throw new FileToolException("MissingDependency: the configured local ImageMagick executable could not be started.");
        }
        finally
        {
            Kill(process);
            // Wait for the killed process to release resources before its Process object is disposed.
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2)); }
            catch (InvalidOperationException) { }
            catch (TimeoutException) { }
            try { Directory.Delete(scratch, recursive: true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            { throw new FileToolException("CleanupFailed: local codec temporary storage could not be removed; no output was published."); }
        }
    }

    private static void Kill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }
    }
}

internal sealed record ImageMetadata(int Width, int Height, bool Alpha, string ColorSpace, bool HasColorProfile);
