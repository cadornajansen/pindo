using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using LocalTutor.Tools.Compression;
using LocalTutor.Tools.FileInspectionAndConversion;

namespace LocalTutor.Tests.Compression;

[CollectionDefinition("Compression codec tests", DisableParallelization = true)]
public sealed class CompressionCodecCollection;

[Collection("Compression codec tests")]
public sealed class ImageCompressionTests : IDisposable
{
    private readonly string workspace = Path.Combine(Path.GetTempPath(), "localtutor-compression-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string executable = Environment.GetEnvironmentVariable("LOCAL_TUTOR_IMAGEMAGICK") ??
        (OperatingSystem.IsWindows() ? throw new InvalidOperationException("Set LOCAL_TUTOR_IMAGEMAGICK to a trusted installed magick.exe.") : "/usr/bin/convert");
    private string Source => Path.Combine(workspace, "lesson.png");
    private string Output => Path.Combine(workspace, "lesson-shared.png");
    private ImageMagickCodec Codec => new(executable);
    private FileAccessScope Scope => new(workspace, [Source], [Output]);

    public ImageCompressionTests() => Directory.CreateDirectory(workspace);
    public void Dispose() => Directory.Delete(workspace, recursive: true);

    [Theory]
    [InlineData(ImageCompressionPreset.Slides1600, 1600, 800)]
    [InlineData(ImageCompressionPreset.Share800, 800, 400)]
    public async Task PreviewAndExplicitApprovalPublishOnlyTheSmallerReadableCopy(ImageCompressionPreset preset, int width, int height)
    {
        byte[] original = Png(1800, 900, CompressionLevel.NoCompression);
        await File.WriteAllBytesAsync(Source, original);
        CompressImageInput input = new(Source, Output, preset);
        RecordingProgress progress = new();
        var prepared = await CompressImageTool.PreviewAsync(Scope, Codec, input, progress: progress);
        Assert.True(prepared.Success, prepared.Error);
        using var preview = prepared.Value!;
        Assert.Equal(Source, preview.InputPath);
        Assert.Equal(Output, preview.OutputPath);
        Assert.Equal([Source], Directory.GetFiles(workspace));
        Assert.True(preview.Summary.IsSmaller);
        Assert.Equal(original.Length, preview.Summary.OriginalBytes);
        Assert.Equal((1800, 900, width, height), (preview.Summary.OriginalWidth, preview.Summary.OriginalHeight, preview.Summary.NewWidth, preview.Summary.NewHeight));
        Assert.Contains(preview.Summary.Warnings, w => w.Contains("detail", StringComparison.Ordinal));
        Assert.Contains(preview.Summary.Warnings, w => w.Contains("No lossless", StringComparison.Ordinal));
        CompressImageTool tool = new(new(preview), progress);
        var saved = await tool.ExecuteAsync(input);
        Assert.Equal("file.compress_image", tool.Id);
        Assert.True(saved.Success, saved.Error);
        Assert.Equal(ImageCompressionStatus.Compressed, saved.Value!.Status);
        Assert.Equal(Output, saved.Value.OutputPath);
        Assert.Equal(preview.Summary, saved.Value.Summary);
        byte[] encoded = await File.ReadAllBytesAsync(Output);
        Assert.Equal(encoded.Length, saved.Value.Summary.NewBytes);
        Assert.True(encoded.Length < original.Length);
        Assert.Equal(original, await File.ReadAllBytesAsync(Source));
        byte[] rgba = await DecodeAsync(encoded);
        Assert.Equal(width * height * 4, rgba.Length);
        Assert.Equal(0, rgba[3]);
        Assert.Equal(255, rgba[^1]);
        Assert.DoesNotContain("synthetic metadata", Encoding.Latin1.GetString(encoded));
        Assert.Equal(["Preparing", "Encoding", "Verifying", "Ready", "CheckingSource", "Saving", "Publishing", "Completed"], progress.Stages);
        AssertNoPartials();
    }

    [Fact]
    public async Task AlreadySmallFileReportsNoReductionAndDoesNotWrite()
    {
        byte[] original = Png(1, 1);
        await File.WriteAllBytesAsync(Source, original);
        CompressImageInput input = new(Source, Output, ImageCompressionPreset.Share800);
        var prepared = await CompressImageTool.PreviewAsync(Scope, Codec, input);
        Assert.True(prepared.Success, prepared.Error);
        using var preview = prepared.Value!;
        Assert.False(preview.Summary.IsSmaller);
        Assert.True(preview.Summary.NewBytes >= original.Length);
        Assert.Equal((1, 1), (preview.Summary.NewWidth, preview.Summary.NewHeight));
        Assert.Contains(preview.Summary.Warnings, w => w.Contains("not smaller", StringComparison.Ordinal));
        var result = await new CompressImageTool(new(preview)).ExecuteAsync(input);
        Assert.True(result.Success, result.Error);
        Assert.Equal(ImageCompressionStatus.NoReduction, result.Value!.Status);
        Assert.Null(result.Value.OutputPath);
        Assert.Equal(original, await File.ReadAllBytesAsync(Source));
        Assert.Equal([Source], Directory.GetFiles(workspace));
    }

    [Fact]
    public async Task ApprovalRejectsDifferentPresetDestinationReplayAndDisposedPreview()
    {
        await File.WriteAllBytesAsync(Source, Png(1, 1));
        CompressImageInput input = new(Source, Output, ImageCompressionPreset.Share800);
        var prepared = await CompressImageTool.PreviewAsync(Scope, Codec, input);
        Assert.True(prepared.Success, prepared.Error);
        using var preview = prepared.Value!;
        CompressImageTool tool = new(new(preview));
        var mismatched = await tool.ExecuteAsync(input with { Preset = ImageCompressionPreset.Slides1600 });
        Assert.Contains("ApprovalMismatch", mismatched.Error);
        var wrongDestination = await tool.ExecuteAsync(input with { OutputPath = Path.Combine(workspace, "other.png") });
        Assert.Contains("ApprovalMismatch", wrongDestination.Error);
        Assert.True((await tool.ExecuteAsync(input)).Success);
        // Even another approval wrapper cannot replay the same preview.
        var replay = await new CompressImageTool(new(preview)).ExecuteAsync(input);
        Assert.False(replay.Success);
        Assert.Contains("ApprovalConsumed", replay.Error);
        var second = await CompressImageTool.PreviewAsync(Scope, Codec, input);
        Assert.True(second.Success, second.Error);
        second.Value!.Dispose();
        var disposed = await new CompressImageTool(new(second.Value)).ExecuteAsync(input);
        Assert.Contains("ApprovalConsumed", disposed.Error);
        AssertNoPartials();
    }

    [Fact]
    public async Task ExpiredApprovalAndChangedSourceRequireNewPreview()
    {
        await File.WriteAllBytesAsync(Source, Png(4, 2));
        CompressImageInput input = new(Source, Output, ImageCompressionPreset.Share800);
        TestClock clock = new();
        var prepared = await CompressImageTool.PreviewAsync(Scope, Codec, input, clock: clock);
        Assert.True(prepared.Success, prepared.Error);
        using var preview = prepared.Value!;
        clock.Advance(TimeSpan.FromMinutes(5));
        var expired = await new CompressImageTool(new(preview)).ExecuteAsync(input);
        Assert.False(expired.Success);
        Assert.Contains("ApprovalExpired", expired.Error);
        var fresh = await CompressImageTool.PreviewAsync(Scope, Codec, input);
        Assert.True(fresh.Success, fresh.Error);
        using var freshPreview = fresh.Value!;
        byte[] changed = Png(5, 2);
        await File.WriteAllBytesAsync(Source, changed);
        var sourceChanged = await new CompressImageTool(new(freshPreview)).ExecuteAsync(input);
        Assert.False(sourceChanged.Success);
        Assert.Contains("SourceChanged", sourceChanged.Error);
        Assert.Equal(changed, await File.ReadAllBytesAsync(Source));
        Assert.False(File.Exists(Output));
    }

    [Theory]
    [InlineData("Existing")]
    [InlineData("Late")]
    [InlineData("SourceChanged")]
    [InlineData("Cancel")]
    public async Task ConflictsChangesAndCancellationCleanPartialFiles(string trigger)
    {
        byte[] original = Png(32, 16, CompressionLevel.NoCompression);
        await File.WriteAllBytesAsync(Source, original);
        CompressImageInput input = new(Source, Output, ImageCompressionPreset.Share800);
        var prepared = await CompressImageTool.PreviewAsync(Scope, Codec, input);
        Assert.True(prepared.Success, prepared.Error);
        using var preview = prepared.Value!;
        Assert.True(preview.Summary.IsSmaller);
        using CancellationTokenSource cancellation = new();
        if (trigger == "Existing") await File.WriteAllTextAsync(Output, "keep existing");
        RecordingProgress progress = new(stage =>
        {
            if (stage != "Publishing") return;
            Assert.Single(Directory.GetFiles(workspace, "*.partial"));
            if (trigger == "Late") File.WriteAllText(Output, "keep existing");
            if (trigger == "SourceChanged") File.WriteAllBytes(Source, Png(3, 2));
            if (trigger == "Cancel") cancellation.Cancel();
        });
        CompressImageTool tool = new(new(preview), progress);
        if (trigger == "Cancel")
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tool.ExecuteAsync(input, cancellation.Token));
        else
        {
            var failed = await tool.ExecuteAsync(input);
            Assert.False(failed.Success);
            Assert.Contains(trigger == "SourceChanged" ? "SourceChanged" : "OutputConflict", failed.Error);
        }
        if (trigger is "Existing" or "Late") Assert.Equal("keep existing", await File.ReadAllTextAsync(Output));
        else Assert.False(File.Exists(Output));
        if (trigger != "SourceChanged") Assert.Equal(original, await File.ReadAllBytesAsync(Source));
        AssertNoPartials();
        Assert.Contains("ApprovalConsumed", (await tool.ExecuteAsync(input)).Error);
    }

    [Fact]
    public async Task CancellationBeforePreviewBeforeExecutionAndWhileEncodingDoesNotPublish()
    {
        await File.WriteAllBytesAsync(Source, Png(32, 16, CompressionLevel.NoCompression));
        CompressImageInput input = new(Source, Output, ImageCompressionPreset.Share800);
        using CancellationTokenSource canceled = new();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CompressImageTool.PreviewAsync(Scope, Codec, input, canceled.Token));
        var prepared = await CompressImageTool.PreviewAsync(Scope, Codec, input);
        Assert.True(prepared.Success, prepared.Error);
        using var preview = prepared.Value!;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new CompressImageTool(new(preview)).ExecuteAsync(input, canceled.Token));
        using CancellationTokenSource encoding = new();
        var progress = new RecordingProgress(stage => { if (stage == "Encoding") encoding.Cancel(); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CompressImageTool.PreviewAsync(Scope, Codec, input, encoding.Token, progress));
        Assert.Equal([Source], Directory.GetFiles(workspace));
        AssertNoPartials();
    }

    [Theory]
    [InlineData("Corrupt")]
    [InlineData("Truncated")]
    [InlineData("Mismatched")]
    [InlineData("Animated")]
    [InlineData("Profiled")]
    [InlineData("Pixels")]
    [InlineData("Bytes")]
    public async Task InvalidImagesAndResourceLimitsFailWithoutOutput(string fixture)
    {
        byte[] original = fixture switch
        {
            "Corrupt" => "not an image"u8.ToArray(),
            "Truncated" => Png(2, 2)[..30],
            "Mismatched" => new byte[] { 255, 216, 255, 0 },
            "Animated" => Png(2, 2, animation: true),
            "Profiled" => Png(2, 2, profile: true),
            "Pixels" => Png(2100, 2000),
            _ => new byte[20 * 1024 * 1024 + 1]
        };
        await File.WriteAllBytesAsync(Source, original);
        var failed = await CompressImageTool.PreviewAsync(Scope, Codec, new(Source, Output, ImageCompressionPreset.Share800));
        Assert.False(failed.Success);
        Assert.Null(failed.Value);
        Assert.False(File.Exists(Output));
        Assert.Equal(original, await File.ReadAllBytesAsync(Source));
        AssertNoPartials();
    }

    [Fact]
    public async Task MissingDependencyOutputConflictsAndUnapprovedPathsAreClearFailures()
    {
        await File.WriteAllBytesAsync(Source, Png(2, 2));
        CompressImageInput input = new(Source, Output, ImageCompressionPreset.Share800);
        ImageMagickCodec missing = new(Path.Combine(workspace, "magick"));
        Assert.Contains("MissingDependency", (await CompressImageTool.PreviewAsync(Scope, missing, input)).Error);
        Assert.Contains("InputNotApproved", (await CompressImageTool.PreviewAsync(new(workspace, [], [Output]), Codec, input)).Error);
        Assert.Contains("OutputNotApproved", (await CompressImageTool.PreviewAsync(new(workspace, [Source]), Codec, input)).Error);
        Assert.False((await CompressImageTool.PreviewAsync(Scope, Codec, input with { InputPath = "../lesson.png" })).Success);
        Assert.False((await CompressImageTool.PreviewAsync(Scope, Codec, input with { InputPath = Path.Combine(workspace + "-outside", "lesson.png") })).Success);
        Assert.Contains("OutputConflict", (await CompressImageTool.PreviewAsync(new(workspace, [Source], [Source]), Codec, input with { OutputPath = Source })).Error);
        await File.WriteAllTextAsync(Output, "preserve output");
        Assert.Contains("OutputConflict", (await CompressImageTool.PreviewAsync(Scope, Codec, input)).Error);
        Assert.Equal("preserve output", await File.ReadAllTextAsync(Output));
    }

    [Fact]
    public async Task LinksAndLateDirectoryReplacementAreRejected()
    {
        if (OperatingSystem.IsWindows()) return; // shortcut: POSIX link fixture; qualify junctions on the Windows demo device.
        await File.WriteAllBytesAsync(Source, Png(32, 16, CompressionLevel.NoCompression));
        string link = Path.Combine(workspace, "link.png");
        File.CreateSymbolicLink(link, Source);
        Assert.Contains("LinkedPath", (await CompressImageTool.PreviewAsync(new(workspace, [link], [Output]), Codec,
            new(link, Output, ImageCompressionPreset.Share800))).Error);
        string target = Path.Combine(workspace, "target"), other = Path.Combine(workspace, "other");
        Directory.CreateDirectory(target); Directory.CreateDirectory(other);
        string output = Path.Combine(target, "copy.png");
        CompressImageInput input = new(Source, output, ImageCompressionPreset.Share800);
        var prepared = await CompressImageTool.PreviewAsync(new(workspace, [Source], [output]), Codec, input);
        Assert.True(prepared.Success, prepared.Error);
        using var preview = prepared.Value!;
        RecordingProgress progress = new(stage =>
        {
            if (stage != "Saving") return;
            Directory.Delete(target);
            Directory.CreateSymbolicLink(target, other);
        });
        var failed = await new CompressImageTool(new(preview), progress).ExecuteAsync(input);
        Assert.False(failed.Success);
        Assert.Contains("LinkedPath", failed.Error);
        Assert.Empty(Directory.GetFiles(other));
        AssertNoPartials();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"InputPath\":\"a.png\",\"OutputPath\":\"b.png\"}")]
    [InlineData("{\"InputPath\":\"a.png\",\"OutputPath\":\"b.png\",\"Preset\":\"Share800\",\"Quality\":1}")]
    [InlineData("{\"InputPath\":\"a.png\",\"OutputPath\":\"b.png\",\"Preset\":\"-execute\"}")]
    [InlineData("{\"InputPath\":\"a.png\",\"OutputPath\":\"b.png\",\"Preset\":0}")]
    public void StrictJsonRejectsMissingExtraUnknownAndNumericArguments(string json)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CompressImageInput>(json));

    [Fact]
    public void ValidationRejectsOtherFormatsAndArbitraryPresets()
    {
        Assert.NotEmpty(new CompressImageInput("a.jpg", "b.png", ImageCompressionPreset.Share800).Validate());
        Assert.NotEmpty(new CompressImageInput("a.png", "b.webp", ImageCompressionPreset.Share800).Validate());
        Assert.NotEmpty(new CompressImageInput("a.png", "b.png", (ImageCompressionPreset)9).Validate());
        Assert.NotEmpty(new CompressImageInput("a.png", "NUL.png", ImageCompressionPreset.Share800).Validate());
        Assert.Empty(JsonSerializer.Deserialize<CompressImageInput>("{\"InputPath\":\"a.png\",\"OutputPath\":\"b.png\",\"Preset\":\"Share800\"}")!.Validate());
    }

    [Fact]
    public async Task NativeTimeoutAndInFlightCancellationLeaveNoDestination()
    {
        if (OperatingSystem.IsWindows()) return; // shortcut: POSIX worker fixture; qualify Windows process-tree termination on the demo device.
        await File.WriteAllBytesAsync(Source, Png(2, 2));
        string worker = Path.Combine(workspace, "convert"), pidFile = Path.Combine(workspace, "worker.pid");
        await File.WriteAllTextAsync(worker, "#!/bin/sh\nprintf '%s' \"$$\" > '" + pidFile + "'\nwhile :; do sleep 1; done\n");
        File.SetUnixFileMode(worker, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        CompressImageInput input = new(Source, Output, ImageCompressionPreset.Share800);
        var timedOut = await CompressImageTool.PreviewAsync(Scope, new(worker, TimeSpan.FromMilliseconds(300)), input);
        Assert.False(timedOut.Success);
        Assert.Contains("TimedOut", timedOut.Error);
        AssertWorkerExited();
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(300));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CompressImageTool.PreviewAsync(Scope, new(worker), input, cancellation.Token));
        AssertWorkerExited();
        Assert.False(File.Exists(Output));
        AssertNoPartials();

        void AssertWorkerExited()
        {
            int pid = int.Parse(File.ReadAllText(pidFile));
            try { using Process process = Process.GetProcessById(pid); Assert.True(process.HasExited); }
            catch (ArgumentException) { }
        }
    }

    private void AssertNoPartials() => Assert.Empty(Directory.GetFiles(workspace, "*.partial", SearchOption.AllDirectories));

    private async Task<byte[]> DecodeAsync(byte[] encoded)
    {
        using Process process = new();
        process.StartInfo = new(executable) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in new[] { "PNG:-", "-depth", "8", "RGBA:-" }) process.StartInfo.ArgumentList.Add(arg);
        process.Start();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        using MemoryStream pixels = new();
        Task read = process.StandardOutput.BaseStream.CopyToAsync(pixels);
        await process.StandardInput.BaseStream.WriteAsync(encoded);
        process.StandardInput.Close();
        await Task.WhenAll(read, errors, process.WaitForExitAsync()).WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(0, process.ExitCode);
        return pixels.ToArray();
    }

    private sealed class RecordingProgress(Action<string>? callback = null) : IProgress<FileToolProgress>
    {
        public List<string> Stages { get; } = [];
        public void Report(FileToolProgress value) { Stages.Add(value.Stage); callback?.Invoke(value.Stage); }
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan time) => now += time;
    }

    // Synthetic PNGs use only built-in .NET APIs and no classroom/student assets.
    private static byte[] Png(int width, int height, CompressionLevel level = CompressionLevel.SmallestSize, bool animation = false, bool profile = false)
    {
        using MemoryStream png = new();
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        byte[] header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; header[9] = 6;
        Chunk("IHDR", header);
        if (level == CompressionLevel.NoCompression) Chunk("tEXt", "Comment\0synthetic metadata"u8.ToArray());
        if (animation) Chunk("acTL", new byte[] { 0, 0, 0, 2, 0, 0, 0, 0 });
        if (profile) Chunk("iCCP", "incomplete profile"u8.ToArray());
        using MemoryStream encoded = new();
        using (ZLibStream zip = new(encoded, level, leaveOpen: true))
        {
            byte[] row = new byte[width * 4 + 1];
            for (int x = 0; x < width; x++)
            {
                row[1 + x * 4] = (byte)x;
                row[2 + x * 4] = 140;
                row[3 + x * 4] = 190;
                row[4 + x * 4] = x < width / 2 ? (byte)0 : (byte)255;
            }
            for (int y = 0; y < height; y++) zip.Write(row);
        }
        Chunk("IDAT", encoded.ToArray());
        Chunk("IEND", []);
        return png.ToArray();

        void Chunk(string type, byte[] data)
        {
            byte[] field = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(field, data.Length); png.Write(field);
            byte[] name = Encoding.ASCII.GetBytes(type);
            png.Write(name); png.Write(data);
            uint crc = 0xffffffff;
            foreach (byte b in name.Concat(data))
            {
                crc ^= b;
                for (int i = 0; i < 8; i++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320 : crc >> 1;
            }
            BinaryPrimitives.WriteUInt32BigEndian(field, ~crc); png.Write(field);
        }
    }
}
