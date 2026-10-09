using System.Diagnostics;
using System.IO.Compression;
using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using LocalTutor.Tools.FileInspectionAndConversion;

namespace LocalTutor.Tests.FileInspectionAndConversion;

public sealed class ImageToolTests : IDisposable
{
    private readonly string workspace = Path.Combine(Path.GetTempPath(), "localtutor-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string executable = Environment.GetEnvironmentVariable("LOCAL_TUTOR_IMAGEMAGICK") ??
        (OperatingSystem.IsWindows() ? throw new InvalidOperationException("Set LOCAL_TUTOR_IMAGEMAGICK to the installed magick.exe for real codec tests.") : "/usr/bin/convert");

    public ImageToolTests() => Directory.CreateDirectory(workspace);
    public void Dispose() => Directory.Delete(workspace, recursive: true);
    private string PathFor(string name) => Path.Combine(workspace, name);
    private ImageMagickCodec Codec => new(executable);
    private FileAccessScope Scope(string input, string? output = null) => new(workspace, [input], output is null ? [] : [output]);

    [Fact]
    public async Task InspectionReturnsMetadataWithoutPrivateContentOrPaths()
    {
        string input = PathFor("student-private.png");
        await File.WriteAllBytesAsync(input, Png());
        string[] before = Directory.GetFiles(workspace);
        InspectFileTool tool = new(Scope(input), Codec);
        var result = await tool.ExecuteAsync(new(input));
        Assert.Equal("file.inspect", tool.Id);
        Assert.True(result.Success, result.Error);
        Assert.Equal("PNG", result.Value!.DetectedType);
        Assert.Equal("Decoded", result.Value.Verification);
        Assert.Equal(4, result.Value.Width);
        Assert.Equal(2, result.Value.Height);
        Assert.Equal(new FileInfo(input).Length, result.Value.SizeBytes);
        Assert.Equal(["file.convert_image"], result.Value.SupportedNextActions);
        Assert.Null(result.Value.PageCount);
        Assert.Null(result.Value.DurationSeconds);
        string json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("student-private", json);
        Assert.DoesNotContain("private classroom text", json);
        Assert.DoesNotContain(workspace, json);
        Assert.Equal(before, Directory.GetFiles(workspace));
    }

    public static IEnumerable<object[]> FormatMatrix()
    {
        foreach (ImageFormat source in Enum.GetValues<ImageFormat>())
            foreach (ImageFormat target in Enum.GetValues<ImageFormat>()) yield return [source, target];
    }

    [Theory]
    [MemberData(nameof(FormatMatrix))]
    public async Task EverySupportedFormatPairProducesAReadableNewImage(ImageFormat sourceFormat, ImageFormat targetFormat)
    {
        byte[] original = sourceFormat == ImageFormat.Png ? Png() : await NativeAsync(Png(), ["PNG:-", FormatName(sourceFormat) + ":-"]);
        string input = PathFor("water-cycle." + Extension(sourceFormat));
        string output = PathFor("water-cycle-slides." + Extension(targetFormat));
        await File.WriteAllBytesAsync(input, original);
        RecordingProgress progress = new();
        ConvertImageTool tool = new(Scope(input, output), Codec, progress);
        var result = await tool.ExecuteAsync(new(input, output, targetFormat,
            targetFormat == ImageFormat.Jpeg ? TransparencyMode.FlattenWhite : TransparencyMode.Preserve,
            MaxWidth: 2, MaxHeight: 2, Quality: targetFormat == ImageFormat.Png ? null : 90));
        Assert.Equal("file.convert_image", tool.Id);
        Assert.True(result.Success, result.Error);
        Assert.Equal(output, result.Value!.OutputPath);
        Assert.Equal(2, result.Value.Width);
        Assert.Equal(1, result.Value.Height);
        Assert.Equal(new FileInfo(output).Length, result.Value.SizeBytes);
        Assert.Equal(original, await File.ReadAllBytesAsync(input));
        byte[] pixels = await NativeAsync(await File.ReadAllBytesAsync(output), [FormatName(targetFormat) + ":-", "-depth", "8", "RGBA:-"]);
        Assert.Equal(2 * 1 * 4, pixels.Length);
        Assert.Equal([0, 25, 65, 90, 100], progress.Items.Select(p => p.Percent));
        Assert.DoesNotContain(Directory.GetFiles(workspace), p => p.EndsWith(".partial", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(ImageFormat.Png)]
    [InlineData(ImageFormat.WebP)]
    public async Task TransparencyIsPreserved(ImageFormat target)
    {
        string input = PathFor("source.png"), output = PathFor("alpha." + Extension(target));
        await File.WriteAllBytesAsync(input, Png());
        var result = await new ConvertImageTool(Scope(input, output), Codec).ExecuteAsync(new(input, output, target));
        Assert.True(result.Success, result.Error);
        Assert.True(result.Value!.HasAlphaChannel);
        byte[] rgba = await NativeAsync(await File.ReadAllBytesAsync(output), [FormatName(target) + ":-", "-depth", "8", "RGBA:-"]);
        Assert.Equal(0, rgba[3]);
        Assert.Equal(255, rgba[7]);
    }

    [Fact]
    public async Task JpegFlattensTransparentPixelToWhiteAndStripsMetadata()
    {
        string input = PathFor("source.png"), output = PathFor("lesson.jpg");
        await File.WriteAllBytesAsync(input, Png());
        var tool = new ConvertImageTool(Scope(input, output), Codec);
        var denied = await tool.ExecuteAsync(new(input, output, ImageFormat.Jpeg));
        Assert.False(denied.Success);
        Assert.Contains("FlattenWhite", denied.Error);
        Assert.False(File.Exists(output));
        var result = await tool.ExecuteAsync(new(input, output, ImageFormat.Jpeg, TransparencyMode.FlattenWhite, Quality: 100));
        Assert.True(result.Success, result.Error);
        Assert.False(result.Value!.HasAlphaChannel);
        byte[] data = await File.ReadAllBytesAsync(output);
        Assert.DoesNotContain("private classroom text", Encoding.Latin1.GetString(data));
        byte[] rgba = await NativeAsync(data, ["JPEG:-", "-depth", "8", "RGBA:-"]);
        Assert.InRange(rgba[0], (byte)245, (byte)255);
        Assert.InRange(rgba[1], (byte)245, (byte)255);
        Assert.InRange(rgba[2], (byte)245, (byte)255);
    }

    [Fact]
    public async Task ResizeFitsBoxWithoutUpscalingAndUnknownFilesExposeNoContent()
    {
        string input = PathFor("source.png"), output = PathFor("large.png");
        await File.WriteAllBytesAsync(input, Png());
        var result = await new ConvertImageTool(Scope(input, output), Codec).ExecuteAsync(new(input, output, ImageFormat.Png, MaxWidth: 100, MaxHeight: 100));
        Assert.True(result.Success, result.Error);
        Assert.Equal(4, result.Value!.Width);
        Assert.Equal(2, result.Value.Height);
        string unknown = PathFor("private.txt");
        await File.WriteAllTextAsync(unknown, "private classroom text");
        var inspection = await new InspectFileTool(Scope(unknown)).ExecuteAsync(new(unknown));
        Assert.True(inspection.Success);
        Assert.Equal("Unknown", inspection.Value!.DetectedType);
        Assert.Empty(inspection.Value.SupportedNextActions);
        Assert.DoesNotContain("private classroom text", JsonSerializer.Serialize(inspection));
    }

    [Fact]
    public async Task InvalidRequestsAndUnapprovedPathsNeverWrite()
    {
        string input = PathFor("source.png"), output = PathFor("output.png");
        await File.WriteAllBytesAsync(input, Png());
        var tool = new ConvertImageTool(Scope(input, output), Codec);
        ConvertImageInput[] invalid = [
            new("", output, ImageFormat.Png), new("../source.png", output, ImageFormat.Png),
            new(input, output, (ImageFormat)999), new(input, output, ImageFormat.Png, (TransparencyMode)999),
            new(input, output, ImageFormat.Png, MaxWidth: 2), new(input, output, ImageFormat.Png, MaxWidth: 0, MaxHeight: 2),
            new(input, output, ImageFormat.Png, Quality: 85), new(input, PathFor("output.jpg"), ImageFormat.WebP),
            new(input, PathFor("output.webp"), ImageFormat.WebP, Quality: 101),
            new(input, PathFor("unapproved.png"), ImageFormat.Png),
            new(PathFor("unapproved.png"), output, ImageFormat.Png),
            new(input, output + ":alternate", ImageFormat.Png),
            new(input, PathFor("NUL.png"), ImageFormat.Png),
            new(input, PathFor("COM1.png"), ImageFormat.Png)
        ];
        foreach (var request in invalid) Assert.False((await tool.ExecuteAsync(request)).Success);
        Assert.Equal([input], Directory.GetFiles(workspace));
        Assert.ThrowsAny<Exception>(() => new FileAccessScope(workspace, [Path.Combine(workspace + "-other", "source.png")]));
        Assert.ThrowsAny<Exception>(() => new FileAccessScope(Path.GetPathRoot(workspace)!, [input]));
    }

    [Fact]
    public async Task SourceAndExistingOutputsAreNeverOverwrittenIncludingLateConflict()
    {
        string input = PathFor("source.png"), output = PathFor("occupied.png");
        byte[] original = Png();
        await File.WriteAllBytesAsync(input, original);
        await File.WriteAllTextAsync(output, "keep existing file");
        var conflict = await new ConvertImageTool(Scope(input, output), Codec).ExecuteAsync(new(input, output, ImageFormat.Png));
        Assert.False(conflict.Success);
        Assert.Contains("OutputConflict", conflict.Error);
        Assert.Equal("keep existing file", await File.ReadAllTextAsync(output));
        var same = await new ConvertImageTool(Scope(input, input), Codec).ExecuteAsync(new(input, input, ImageFormat.Png));
        Assert.False(same.Success);
        Assert.Equal(original, await File.ReadAllBytesAsync(input));
        string late = PathFor("late.png");
        RecordingProgress progress = new(p => { if (p.Stage == "Saving") File.WriteAllText(late, "created after preflight"); });
        var raced = await new ConvertImageTool(Scope(input, late), Codec, progress).ExecuteAsync(new(input, late, ImageFormat.Png));
        Assert.False(raced.Success);
        Assert.Equal("created after preflight", await File.ReadAllTextAsync(late));
        Assert.DoesNotContain(Directory.GetFiles(workspace), p => p.EndsWith(".partial", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MalformedMismatchedAnimatedAndOversizedImagesFailBeforeWriting()
    {
        byte[][] inputs = ["not an image"u8.ToArray(), Png()[..30], Png(9000, 1), Png(animation: true)];
        foreach (byte[] data in inputs)
        {
            string input = PathFor("bad.png"), output = PathFor("output.png");
            await File.WriteAllBytesAsync(input, data);
            Assert.False((await new InspectFileTool(Scope(input), Codec).ExecuteAsync(new(input))).Success);
            Assert.False((await new ConvertImageTool(Scope(input, output), Codec).ExecuteAsync(new(input, output, ImageFormat.Png))).Success);
            Assert.False(File.Exists(output));
        }
        string mismatch = PathFor("wrong.jpg");
        await File.WriteAllBytesAsync(mismatch, Png());
        var result = await new InspectFileTool(Scope(mismatch), Codec).ExecuteAsync(new(mismatch));
        Assert.False(result.Success);
        Assert.Contains("FormatMismatch", result.Error);
        string large = PathFor("oversized.png");
        await using (FileStream stream = File.Create(large)) stream.SetLength(21 * 1024 * 1024);
        Assert.False((await new ConvertImageTool(Scope(large, PathFor("output.png")), Codec).ExecuteAsync(new(large, PathFor("output.png"), ImageFormat.Png))).Success);
    }

    [Fact]
    public async Task MissingDependencyAndCancellationHaveNoOutputs()
    {
        string input = PathFor("source.png"), output = PathFor("output.png");
        await File.WriteAllBytesAsync(input, Png());
        ImageMagickCodec missing = new(PathFor(OperatingSystem.IsWindows() ? "magick.exe" : "magick"));
        var unavailable = await new ConvertImageTool(Scope(input, output), missing).ExecuteAsync(new(input, output, ImageFormat.Png));
        Assert.False(unavailable.Success);
        Assert.Contains("MissingDependency", unavailable.Error);
        var headerOnly = await new InspectFileTool(Scope(input)).ExecuteAsync(new(input));
        Assert.True(headerOnly.Success);
        Assert.Null(headerOnly.Value!.Width);
        Assert.Empty(headerOnly.Value.SupportedNextActions);
        using CancellationTokenSource canceled = new();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new InspectFileTool(Scope(input), Codec).ExecuteAsync(new(input), canceled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ConvertImageTool(Scope(input, output), Codec).ExecuteAsync(new(input, output, ImageFormat.Png), canceled.Token));
        using CancellationTokenSource saving = new();
        RecordingProgress progress = new(p => { if (p.Stage == "Saving") saving.Cancel(); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ConvertImageTool(Scope(input, output), Codec, progress).ExecuteAsync(new(input, output, ImageFormat.Png), saving.Token));
        Assert.Equal([input], Directory.GetFiles(workspace));
    }

    [Fact]
    public async Task SymbolicLinksCannotEscapeApproval()
    {
        if (OperatingSystem.IsWindows()) return; // Creating symlinks requires Windows privileges; run junction checks on the demo device.
        string input = PathFor("source.png"), output = PathFor("output.png"), link = PathFor("linked.png");
        await File.WriteAllBytesAsync(input, Png());
        File.CreateSymbolicLink(link, input);
        var result = await new InspectFileTool(Scope(link), Codec).ExecuteAsync(new(link));
        Assert.False(result.Success);
        Assert.Contains("LinkedPath", result.Error);
        File.CreateSymbolicLink(output, PathFor("missing.png"));
        Assert.False((await new ConvertImageTool(Scope(input, output), Codec).ExecuteAsync(new(input, output, ImageFormat.Png))).Success);
        string directoryLink = PathFor("directory-link");
        Directory.CreateSymbolicLink(directoryLink, workspace);
        string linkedInput = Path.Combine(directoryLink, "source.png");
        Assert.False((await new InspectFileTool(Scope(linkedInput), Codec).ExecuteAsync(new(linkedInput))).Success);
    }

    [Fact]
    public void JsonRejectsUnknownArgumentsAndValidationRejectsUndefinedEnums()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<InspectFileInput>("{}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ConvertImageInput>("{\"InputPath\":\"source.png\",\"OutputPath\":\"new.png\"}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<InspectFileInput>("{\"InputPath\":\"source.png\",\"Command\":\"run\"}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ConvertImageInput>("{\"InputPath\":\"source.png\",\"OutputPath\":\"new.png\",\"Format\":\"Png\",\"Arguments\":\"-delete\"}"));
        Assert.NotEmpty(new ConvertImageInput("source.png", "new.png", (ImageFormat)234).Validate());
    }

    [Fact]
    public async Task CmykConversionIsRejectedAndInspectionOffersNoConversion()
    {
        string input = PathFor("print-color.jpg"), output = PathFor("output.png");
        await File.WriteAllBytesAsync(input, await NativeAsync(Png(), ["PNG:-", "-colorspace", "CMYK", "JPEG:-"]));
        var inspection = await new InspectFileTool(Scope(input), Codec).ExecuteAsync(new(input));
        Assert.True(inspection.Success, inspection.Error);
        Assert.Empty(inspection.Value!.SupportedNextActions);
        var conversion = await new ConvertImageTool(Scope(input, output), Codec).ExecuteAsync(new(input, output, ImageFormat.Png));
        Assert.False(conversion.Success);
        Assert.Contains("UnsupportedColor", conversion.Error);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task EmbeddedIccProfilePreventsConversion()
    {
        string input = PathFor("profiled.png"), output = PathFor("output.png");
        await File.WriteAllBytesAsync(input, Png(profile: true));
        var inspection = await new InspectFileTool(Scope(input), Codec).ExecuteAsync(new(input));
        // This deliberately incomplete profile is not a valid color-managed image.
        Assert.False(inspection.Success);
        var conversion = await new ConvertImageTool(Scope(input, output), Codec).ExecuteAsync(new(input, output, ImageFormat.Png));
        Assert.False(conversion.Success);
        Assert.Contains("UnsupportedColor", conversion.Error);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task LateDirectorySymlinkIsRejectedBeforeWriting()
    {
        if (OperatingSystem.IsWindows()) return;
        string input = PathFor("source.png"), directory = PathFor("target"), other = PathFor("other");
        await File.WriteAllBytesAsync(input, Png());
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(other);
        string output = Path.Combine(directory, "output.png");
        RecordingProgress progress = new(p =>
        {
            if (p.Stage != "Saving") return;
            Directory.Delete(directory);
            Directory.CreateSymbolicLink(directory, other);
        });
        var result = await new ConvertImageTool(Scope(input, output), Codec, progress).ExecuteAsync(new(input, output, ImageFormat.Png));
        Assert.False(result.Success);
        Assert.Contains("LinkedPath", result.Error);
        Assert.Empty(Directory.GetFiles(other));
    }

    [Fact]
    public async Task NativeTimeoutAndInFlightCancellationTerminateWorker()
    {
        if (OperatingSystem.IsWindows()) return; // POSIX process fixture; Windows process-tree behavior remains a device check.
        string[] scratchBefore = Directory.GetDirectories(Path.GetTempPath(), "localtutor-codec-*").Order().ToArray();
        string fixture = PathFor("convert"), pidFile = PathFor("worker.pid"), input = PathFor("source.png"), output = PathFor("output.png");
        await File.WriteAllBytesAsync(input, Png());
        await File.WriteAllTextAsync(fixture, "#!/bin/sh\nprintf '%s' \"$$\" > '" + pidFile + "'\nprintf 'transient input' > \"$MAGICK_TEMPORARY_PATH/fixture.tmp\"\nwhile :; do sleep 1; done\n");
        File.SetUnixFileMode(fixture, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        ImageMagickCodec slow = new(fixture, TimeSpan.FromMilliseconds(300));
        var timeout = await new ConvertImageTool(Scope(input, output), slow).ExecuteAsync(new(input, output, ImageFormat.Png));
        Assert.False(timeout.Success);
        Assert.Contains("TimedOut", timeout.Error);
        AssertWorkerExited(pidFile);
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(300));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new InspectFileTool(Scope(input), new(fixture)).ExecuteAsync(new(input), cancellation.Token));
        AssertWorkerExited(pidFile);
        Assert.False(File.Exists(output));
        Assert.Equal(scratchBefore, Directory.GetDirectories(Path.GetTempPath(), "localtutor-codec-*").Order().ToArray());
    }

    private static void AssertWorkerExited(string pidFile)
    {
        int pid = int.Parse(File.ReadAllText(pidFile));
        try { using Process process = Process.GetProcessById(pid); Assert.True(process.HasExited); }
        catch (ArgumentException) { }
    }

    private async Task<byte[]> NativeAsync(byte[] bytes, string[] arguments)
    {
        using Process process = new();
        process.StartInfo = new(executable) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        using MemoryStream output = new();
        Task read = process.StandardOutput.BaseStream.CopyToAsync(output);
        await process.StandardInput.BaseStream.WriteAsync(bytes);
        process.StandardInput.Close();
        await Task.WhenAll(read, errors, process.WaitForExitAsync()).WaitAsync(TimeSpan.FromSeconds(15));
        Assert.True(process.ExitCode == 0, await errors);
        return output.ToArray();
    }

    private static string Extension(ImageFormat format) => format switch { ImageFormat.Png => "png", ImageFormat.Jpeg => "jpg", _ => "webp" };
    private static string FormatName(ImageFormat format) => format switch { ImageFormat.Png => "PNG", ImageFormat.Jpeg => "JPEG", _ => "WEBP" };

    private sealed class RecordingProgress(Action<FileToolProgress>? callback = null) : IProgress<FileToolProgress>
    {
        public List<FileToolProgress> Items { get; } = [];
        public void Report(FileToolProgress value) { Items.Add(value); callback?.Invoke(value); }
    }

    // Synthetic RGBA fixture created with built-in .NET APIs; no downloaded classroom assets.
    private static byte[] Png(int width = 4, int height = 2, bool animation = false, bool profile = false)
    {
        using MemoryStream png = new();
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        byte[] header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; header[9] = 6;
        Chunk("IHDR", header);
        if (profile)
        {
            byte[] icc = new byte[256];
            BinaryPrimitives.WriteInt32BigEndian(icc, icc.Length);
            BinaryPrimitives.WriteInt32BigEndian(icc.AsSpan(8), 0x04300000);
            "mntrRGB XYZ "u8.CopyTo(icc.AsSpan(12));
            BinaryPrimitives.WriteUInt16BigEndian(icc.AsSpan(24), 2026);
            BinaryPrimitives.WriteUInt16BigEndian(icc.AsSpan(26), 10);
            BinaryPrimitives.WriteUInt16BigEndian(icc.AsSpan(28), 9);
            "acsp"u8.CopyTo(icc.AsSpan(36));
            BinaryPrimitives.WriteInt32BigEndian(icc.AsSpan(68), 0x0000f6d6);
            BinaryPrimitives.WriteInt32BigEndian(icc.AsSpan(72), 0x00010000);
            BinaryPrimitives.WriteInt32BigEndian(icc.AsSpan(76), 0x0000d32d);
            using MemoryStream profileData = new();
            profileData.Write("Synthetic\0\0"u8);
            using (ZLibStream zip = new(profileData, CompressionLevel.SmallestSize, leaveOpen: true)) zip.Write(icc);
            Chunk("iCCP", profileData.ToArray());
        }
        Chunk("tEXt", "Comment\0private classroom text"u8.ToArray());
        if (animation) Chunk("acTL", new byte[] { 0, 0, 0, 2, 0, 0, 0, 0 });
        using MemoryStream compressed = new();
        using (ZLibStream zlib = new(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            for (int y = 0; y < height; y++)
            {
                zlib.WriteByte(0);
                for (int x = 0; x < width; x++) zlib.Write(x == 0 ? new byte[] { 0, 255, 0, 0 } : new byte[] { 255, 0, 0, 255 });
            }
        Chunk("IDAT", compressed.ToArray());
        Chunk("IEND", []);
        return png.ToArray();

        void Chunk(string type, byte[] data)
        {
            byte[] length = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
            png.Write(length);
            byte[] name = Encoding.ASCII.GetBytes(type);
            png.Write(name); png.Write(data);
            uint crc = 0xffffffff;
            foreach (byte b in name.Concat(data))
            {
                crc ^= b;
                for (int i = 0; i < 8; i++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320 : crc >> 1;
            }
            BinaryPrimitives.WriteUInt32BigEndian(length, ~crc);
            png.Write(length);
        }
    }
}
