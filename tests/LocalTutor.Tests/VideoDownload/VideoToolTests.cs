using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using LocalTutor.Tools.VideoDownload;

namespace LocalTutor.Tests.VideoDownload;

public sealed class VideoToolTests : IDisposable
{
    private const string Url = "https://www.youtube.com/watch?v=abcdefghijk";
    private readonly string workspace = Path.Combine(Path.GetTempPath(), "localtutor-video-test-" + Guid.NewGuid().ToString("N"));
    private readonly StubProcess process = new();
    private YtDlpClient Client => new(process, (_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }));
    private DownloadVideoInput Input => new(Url, Path.Combine(workspace, "lesson.mp4"), VideoQuality.Mp4_360p);

    public VideoToolTests() => Directory.CreateDirectory(workspace);

    [Fact]
    public async Task InspectionReturnsOnlySafeRelevantMetadata()
    {
        InspectVideoTool tool = new(Client, Url);
        var result = await tool.ExecuteAsync(new InspectVideoInput(Url));
        Assert.True(result.Success, result.Error);
        Assert.Equal("video.inspect_url", tool.Id);
        Assert.Equal("Lesson", result.Value!.Title);
        Assert.Equal(60, result.Value.DurationSeconds);
        Assert.Equal("YouTube", result.Value.Source);
        Assert.Equal(2, result.Value.Formats.Count);
        Assert.All(result.Value.Formats, f => Assert.Equal("mp4", f.Container));
        string output = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("private-token", output);
        Assert.DoesNotContain("googlevideo", output);
        Assert.DoesNotContain("cookie", output);
        Assert.Equal(0, process.Downloads);
    }

    [Theory]
    [InlineData("http://www.youtube.com/watch?v=abcdefghijk")]
    [InlineData("https://127.0.0.1/video.mp4")]
    [InlineData("https://192.168.1.1/video.mp4")]
    [InlineData("https://[::1]/video.mp4")]
    [InlineData("https://localhost/video.mp4")]
    [InlineData("https://youtube.com.attacker.example/watch?v=abcdefghijk")]
    [InlineData("https://user:secret@www.youtube.com/watch?v=abcdefghijk")]
    [InlineData("https://www.youtube.com:8443/watch?v=abcdefghijk")]
    [InlineData("https://www.youtube.com/playlist?list=abc")]
    [InlineData("https://www.youtube.com/watch?v=abcdefghijk&list=abc")]
    [InlineData("https://www.youtube.com/watch?v=abcdefghijk#fragment")]
    [InlineData("https://www.youtube.com/watch?v=abcdefghijk --exec=bad")]
    [InlineData("file:///tmp/video.mp4")]
    [InlineData("--exec=bad")]
    [InlineData("")]
    public async Task UnsafeUrlsNeverReachNativeExecution(string url)
    {
        var result = await new InspectVideoTool(Client, url).ExecuteAsync(new InspectVideoInput(url));
        Assert.False(result.Success);
        Assert.Equal(0, process.Versions);
        Assert.Equal(0, process.Inspections);
    }

    [Fact]
    public async Task ModelCannotSubstituteTheUserSelectedUrl()
    {
        var result = await new InspectVideoTool(Client, "https://youtu.be/abcdefghijk").ExecuteAsync(new InspectVideoInput(Url));
        Assert.False(result.Success);
        Assert.StartsWith("UrlNotApproved", result.Error);
        Assert.Equal(0, process.Versions);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("100.64.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.0.1")]
    [InlineData("198.18.0.1")]
    [InlineData("203.0.113.1")]
    [InlineData("224.0.0.1")]
    [InlineData("::1")]
    [InlineData("::ffff:192.168.0.1")]
    [InlineData("fc00::1")]
    [InlineData("fe80::1")]
    [InlineData("2001:db8::1")]
    [InlineData("2002:0a00:0001::1")]
    public async Task PrivateOrMixedDnsAnswersBlockInspection(string address)
    {
        YtDlpClient client = new(process, (_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8"), IPAddress.Parse(address) }));
        var result = await new InspectVideoTool(client, Url).ExecuteAsync(new InspectVideoInput(Url));
        Assert.False(result.Success);
        Assert.StartsWith("BlockedNetwork", result.Error);
        Assert.Equal(0, process.Inspections);
    }

    [Theory]
    [InlineData("{not json}", "InvalidMetadata")]
    [InlineData("[]", "UnsupportedVideo")]
    [InlineData("{\"_type\":\"playlist\",\"entries\":[]}", "UnsupportedVideo")]
    [InlineData("{\"title\":\"Lesson\",\"duration\":-1}", "InvalidMetadata")]
    [InlineData("{\"title\":\"Lesson\",\"has_drm\":true}", "AccessDenied")]
    [InlineData("{\"title\":\"Lesson\",\"is_live\":true}", "AccessDenied")]
    [InlineData("{\"title\":\"Lesson\",\"availability\":\"needs_auth\"}", "AccessDenied")]
    [InlineData("{\"title\":\"Lesson\",\"age_limit\":18}", "AccessDenied")]
    [InlineData("{\"title\":\"Lesson\",\"has_drm\":\"true\"}", "InvalidMetadata")]
    [InlineData("{\"title\":\"Lesson\",\"availability\":false}", "InvalidMetadata")]
    [InlineData("{\"title\":\"Lesson\",\"duration\":\"60\"}", "InvalidMetadata")]
    public async Task UnsupportedMetadataFailsWithoutNativeDiagnostics(string json, string code)
    {
        process.Metadata = json;
        var result = await new InspectVideoTool(Client, Url).ExecuteAsync(new InspectVideoInput(Url));
        Assert.False(result.Success);
        Assert.StartsWith(code, result.Error);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task UnknownDurationDoesNotOfferDownload()
    {
        process.Metadata = StubProcess.Json(duration: null);
        var result = await new InspectVideoTool(Client, Url).ExecuteAsync(new InspectVideoInput(Url));
        Assert.True(result.Success);
        Assert.Null(result.Value!.DurationSeconds);
        Assert.Empty(result.Value.Formats);
    }

    [Fact]
    public async Task LongVideoCanBeInspectedButNotDownloaded()
    {
        process.Metadata = StubProcess.Json(duration: 601);
        var result = await new InspectVideoTool(Client, Url).ExecuteAsync(new InspectVideoInput(Url));
        Assert.True(result.Success);
        Assert.Equal(601, result.Value!.DurationSeconds);
        Assert.Empty(result.Value.Formats);
        Assert.Contains(result.Value.Warnings, w => w.StartsWith("DownloadLimit", StringComparison.Ordinal));
        Assert.False((await DownloadVideoTool.PreviewAsync(Client, Input, workspace)).Success);
        Assert.Equal(0, process.Downloads);
    }

    [Theory]
    [InlineData("http", "avc1", "aac", false, 360, 1000, "https://r1.googlevideo.com/video")]
    [InlineData("m3u8_native", "avc1", "aac", false, 360, 1000, "https://r1.googlevideo.com/video")]
    [InlineData("https", "avc1", "none", false, 360, 1000, "https://r1.googlevideo.com/video")]
    [InlineData("https", "avc1", "aac", true, 360, 1000, "https://r1.googlevideo.com/video")]
    [InlineData("https", "avc1", "aac", false, 1080, 1000, "https://r1.googlevideo.com/video")]
    [InlineData("https", "avc1", "aac", false, 360, 104857601, "https://r1.googlevideo.com/video")]
    [InlineData("https", "avc1", "aac", false, 360, 1000, "https://127.0.0.1/video")]
    [InlineData("https", "avc1", "aac", false, 360, 1000, "https://attacker.example/video")]
    public async Task OnlyBoundedCombinedPublicHttpsFormatsAreOffered(string protocol, string video, string audio, bool drm, int height, long bytes, string url)
    {
        process.Metadata = StubProcess.Json(protocol: protocol, vcodec: video, acodec: audio, drm: drm, height: height, bytes: bytes, mediaUrl: url);
        var result = await new InspectVideoTool(Client, Url).ExecuteAsync(new InspectVideoInput(Url));
        Assert.True(result.Success);
        Assert.Empty(result.Value!.Formats);
    }

    [Fact]
    public async Task VersionMismatchStopsBeforeUrlFetch()
    {
        process.Version = "2024.04.09";
        var result = await new InspectVideoTool(Client, Url).ExecuteAsync(new InspectVideoInput(Url));
        Assert.False(result.Success);
        Assert.StartsWith("VersionMismatch", result.Error);
        Assert.Equal(0, process.Inspections);
    }

    [Fact]
    public async Task PreviewThenApprovedDownloadPublishesExactlyOneNewFile()
    {
        var preview = await DownloadVideoTool.PreviewAsync(Client, Input, workspace);
        Assert.True(preview.Success, preview.Error);
        Assert.Equal(0, process.Downloads);
        Assert.Equal("YouTube", preview.Value!.Source);
        Assert.Equal("lesson.mp4", preview.Value.Filename);
        Assert.Equal(Input.DestinationPath, preview.Value.DestinationPath);
        Assert.Equal(1000, preview.Value.Format.EstimatedSizeBytes);
        DownloadVideoTool tool = new(Client, new VideoDownloadApproval(preview.Value));
        var result = await tool.ExecuteAsync(Input);
        Assert.True(result.Success, result.Error);
        Assert.Equal("video.download", tool.Id);
        Assert.Equal("Completed", result.Value!.Status);
        Assert.Equal(Input.DestinationPath, result.Value.Path);
        Assert.Equal(32, result.Value.Bytes);
        Assert.Equal("mp4", result.Value.Format);
        Assert.Single(Directory.EnumerateFileSystemEntries(workspace));
        Assert.Equal(1, process.Downloads);
    }

    [Fact]
    public async Task ApprovalDoesNotAuthorizeAnotherDestinationOrQuality()
    {
        var preview = await DownloadVideoTool.PreviewAsync(Client, Input, workspace);
        DownloadVideoTool tool = new(Client, new VideoDownloadApproval(preview.Value!));
        var mismatch = await tool.ExecuteAsync(Input with { DestinationPath = Path.Combine(workspace, "other.mp4") });
        Assert.StartsWith("ApprovalMismatch", mismatch.Error);
        Assert.Equal(0, process.Downloads);
        Assert.True((await tool.ExecuteAsync(Input)).Success);
    }

    [Fact]
    public async Task ApprovalCannotBeReplayedEvenAfterFailure()
    {
        var preview = await DownloadVideoTool.PreviewAsync(Client, Input, workspace);
        DownloadVideoTool tool = new(Client, new VideoDownloadApproval(preview.Value!));
        process.FailDownload = true;
        Assert.False((await tool.ExecuteAsync(Input)).Success);
        Assert.StartsWith("ApprovalConsumed", (await tool.ExecuteAsync(Input)).Error);
        Assert.Equal(1, process.Downloads);
        Assert.Empty(Directory.EnumerateFileSystemEntries(workspace));
    }

    [Fact]
    public async Task ExpiredApprovalCannotStartADownload()
    {
        var preview = await DownloadVideoTool.PreviewAsync(Client, Input, workspace);
        VideoDownloadApproval approval = new(preview.Value!);
        VideoToolException error = Assert.Throws<VideoToolException>(() => approval.Consume(Input, preview.Value!.CreatedAt.AddMinutes(6)));
        Assert.StartsWith("ApprovalExpired", error.Message);
        Assert.Equal(0, process.Downloads);
    }

    [Fact]
    public async Task MediaHostResolvingToPrivateNetworkCannotStartADownload()
    {
        YtDlpClient client = new(process, (host, _) => Task.FromResult(new[] { IPAddress.Parse(host.EndsWith("googlevideo.com", StringComparison.Ordinal) ? "127.0.0.1" : "8.8.8.8") }));
        var preview = await DownloadVideoTool.PreviewAsync(client, Input, workspace);
        var result = await new DownloadVideoTool(client, new VideoDownloadApproval(preview.Value!)).ExecuteAsync(Input);
        Assert.StartsWith("BlockedNetwork", result.Error);
        Assert.Equal(0, process.Downloads);
        Assert.Empty(Directory.EnumerateFileSystemEntries(workspace));
    }

    [Fact]
    public async Task ChangedMetadataRequiresNewPreview()
    {
        var preview = await DownloadVideoTool.PreviewAsync(Client, Input, workspace);
        process.Metadata = StubProcess.Json(duration: 61);
        var result = await new DownloadVideoTool(Client, new VideoDownloadApproval(preview.Value!)).ExecuteAsync(Input);
        Assert.StartsWith("PreviewChanged", result.Error);
        Assert.Equal(0, process.Downloads);
    }

    [Fact]
    public async Task ConflictAfterPreviewPreservesTheExistingFile()
    {
        var preview = await DownloadVideoTool.PreviewAsync(Client, Input, workspace);
        await File.WriteAllTextAsync(Input.DestinationPath, "original");
        var result = await new DownloadVideoTool(Client, new VideoDownloadApproval(preview.Value!)).ExecuteAsync(Input);
        Assert.StartsWith("OutputConflict", result.Error);
        Assert.Equal("original", await File.ReadAllTextAsync(Input.DestinationPath));
        Assert.Equal(0, process.Downloads);
    }

    [Theory]
    [InlineData("../escape.mp4")]
    [InlineData("lesson%.mp4")]
    [InlineData("NUL.mp4")]
    [InlineData("lesson.webm")]
    [InlineData("lesson.mp4:stream")]
    public async Task InvalidDestinationIsRejectedBeforeInspection(string filename)
    {
        var result = await DownloadVideoTool.PreviewAsync(Client, Input with { DestinationPath = Path.Combine(workspace, filename) }, workspace);
        Assert.False(result.Success);
        Assert.Equal(0, process.Inspections);
    }

    [Fact]
    public async Task OutsideWorkspaceIsDenied()
    {
        var outside = await DownloadVideoTool.PreviewAsync(Client, Input with { DestinationPath = Path.Combine(Path.GetTempPath(), "outside.mp4") }, workspace);
        Assert.StartsWith("PathOutsideWorkspace", outside.Error);
        Assert.Equal(0, process.Inspections);
    }

    [NativeUnixVideoFact]
    public async Task SymlinkOutputsAreDenied()
    {
        string target = Path.Combine(workspace, "target");
        string link = Path.Combine(workspace, "link");
        Directory.CreateDirectory(target);
        Directory.CreateSymbolicLink(link, target);
        var linked = await DownloadVideoTool.PreviewAsync(Client, Input with { DestinationPath = Path.Combine(link, "lesson.mp4") }, workspace);
        Assert.StartsWith("LinkedPath", linked.Error);
        Assert.Equal(0, process.Inspections);
    }

    [Fact]
    public async Task OversizedFinalFileIsRemoved()
    {
        var preview = await DownloadVideoTool.PreviewAsync(Client, Input, workspace);
        process.Oversized = true;
        var result = await new DownloadVideoTool(Client, new VideoDownloadApproval(preview.Value!)).ExecuteAsync(Input);
        Assert.StartsWith("ResourceLimit", result.Error);
        Assert.Empty(Directory.EnumerateFileSystemEntries(workspace));
    }

    [Fact]
    public async Task ConflictDuringDownloadPreservesOriginalAndCleansStaging()
    {
        var preview = await DownloadVideoTool.PreviewAsync(Client, Input, workspace);
        process.CreateConflict = Input.DestinationPath;
        var result = await new DownloadVideoTool(Client, new VideoDownloadApproval(preview.Value!)).ExecuteAsync(Input);
        Assert.StartsWith("OutputConflict", result.Error);
        Assert.Equal("original", await File.ReadAllTextAsync(Input.DestinationPath));
        Assert.Single(Directory.EnumerateFileSystemEntries(workspace));
    }

    [Fact]
    public async Task WebMQualityProducesTheSelectedContainer()
    {
        process.Metadata = StubProcess.Json(extension: "webm");
        DownloadVideoInput input = Input with { Quality = VideoQuality.WebM_720p, DestinationPath = Path.Combine(workspace, "lesson.webm") };
        var preview = await DownloadVideoTool.PreviewAsync(Client, input, workspace);
        Assert.True(preview.Success, preview.Error);
        var result = await new DownloadVideoTool(Client, new VideoDownloadApproval(preview.Value!)).ExecuteAsync(input);
        Assert.True(result.Success, result.Error);
        Assert.Equal("webm", result.Value!.Format);
    }

    [Fact]
    public async Task SeparateHttpsStreamsRequirePinnedFfmpegAndBoundTheCombinedSize()
    {
        JsonObject metadata = JsonNode.Parse(StubProcess.Json(acodec: "none"))!.AsObject();
        metadata["formats"]!.AsArray().Add(JsonSerializer.SerializeToNode(new { format_id = "140", ext = "m4a", filesize = 500,
            protocol = "https", vcodec = "none", acodec = "aac", url = "https://r2.googlevideo.com/audio" }));
        process.Metadata = metadata.ToJsonString();
        Assert.Empty((await new InspectVideoTool(Client, Url).ExecuteAsync(new InspectVideoInput(Url))).Value!.Formats);
        process.FfmpegVersion = "ffmpeg version " + YtDlpClient.SupportedFfmpegVersion + " fixture";
        var inspected = await new InspectVideoTool(Client, Url).ExecuteAsync(new InspectVideoInput(Url));
        Assert.All(inspected.Value!.Formats, f => Assert.True(f.RequiresFfmpeg));
        Assert.All(inspected.Value.Formats, f => Assert.Equal(1500, f.EstimatedSizeBytes));
        var preview = await DownloadVideoTool.PreviewAsync(Client, Input, workspace);
        Assert.Equal("18+140", preview.Value!.Candidate.Id);
        Assert.True((await new DownloadVideoTool(Client, new VideoDownloadApproval(preview.Value)).ExecuteAsync(Input)).Success);
        metadata["formats"]![1]!["filesize"] = VideoMetadata.MaxBytes;
        process.Metadata = metadata.ToJsonString();
        Assert.Empty((await new InspectVideoTool(Client, Url).ExecuteAsync(new InspectVideoInput(Url))).Value!.Formats);
    }

    [Theory]
    [InlineData("ffmpeg version 9.0.1-full_build-www.gyan.dev Copyright", true)]
    [InlineData("ffmpeg version 9.0.1 fixture", true)]
    [InlineData("ffmpeg version 9.0.10 fixture", false)]
    public async Task FfmpegReleaseAllowsPackagingSuffixButNotDifferentPatch(string version, bool accepted)
    {
        process.FfmpegVersion = version;
        var result = await new InspectVideoTool(Client, Url).ExecuteAsync(new InspectVideoInput(Url));
        Assert.Equal(accepted, result.Success);
        Assert.Equal(accepted ? 1 : 0, process.Inspections);
    }

    [Fact]
    public async Task FfmpegVersionMismatchIsReportedBeforeSourceAccess()
    {
        process.FfmpegVersion = "ffmpeg version 1.0 fixture";
        var result = await new InspectVideoTool(Client, Url).ExecuteAsync(new InspectVideoInput(Url));
        Assert.StartsWith("VersionMismatch", result.Error);
        Assert.Equal(0, process.Inspections);
    }

    [Fact]
    public void MissingConfiguredFfmpegHasAClearNotice()
    {
        YtDlpClient client = new(Path.Combine(workspace, "yt-dlp"), Path.Combine(workspace, "ffmpeg"));
        Assert.StartsWith("FFmpegMissing", client.FfmpegNotice);
    }

    [Fact]
    public async Task DuplicateOrInjectedFormatIdentifiersCannotReachDownload()
    {
        JsonObject metadata = JsonNode.Parse(StubProcess.Json())!.AsObject();
        metadata["formats"]![0]!["format_id"] = "18;--exec=bad";
        process.Metadata = metadata.ToJsonString();
        Assert.Empty((await new InspectVideoTool(Client, Url).ExecuteAsync(new InspectVideoInput(Url))).Value!.Formats);
        metadata["formats"]![0]!["format_id"] = "18";
        metadata["formats"]!.AsArray().Add(metadata["formats"]![0]!.DeepClone());
        process.Metadata = metadata.ToJsonString();
        var result = await new InspectVideoTool(Client, Url).ExecuteAsync(new InspectVideoInput(Url));
        Assert.StartsWith("InvalidMetadata", result.Error);
        Assert.Equal(0, process.Downloads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedOrInvalidDownloadLeavesNoPartials(bool corrupt)
    {
        var preview = await DownloadVideoTool.PreviewAsync(Client, Input, workspace);
        process.Corrupt = corrupt; process.FailDownload = !corrupt;
        var result = await new DownloadVideoTool(Client, new VideoDownloadApproval(preview.Value!)).ExecuteAsync(Input);
        Assert.False(result.Success);
        Assert.Empty(Directory.EnumerateFileSystemEntries(workspace));
    }

    [Fact]
    public async Task CancellationDuringDownloadRemovesPartials()
    {
        var preview = await DownloadVideoTool.PreviewAsync(Client, Input, workspace);
        using CancellationTokenSource cancellation = new();
        process.CancelDownload = cancellation;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new DownloadVideoTool(Client, new VideoDownloadApproval(preview.Value!)).ExecuteAsync(Input, cancellation.Token));
        Assert.Empty(Directory.EnumerateFileSystemEntries(workspace));
    }

    [Fact]
    public async Task CancellationBeforeInspectionHasNoNativeEffect()
    {
        using CancellationTokenSource cancellation = new(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new InspectVideoTool(Client, Url).ExecuteAsync(new InspectVideoInput(Url), cancellation.Token));
        Assert.Equal(0, process.Versions);
    }

    [Fact]
    public void UnknownModelArgumentsAndMissingRequiredArgumentsAreRejected()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<InspectVideoInput>("{\"Url\":\"https://youtu.be/abcdefghijk\",\"Cookies\":\"secret\"}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DownloadVideoInput>("{}"));
    }

    [Fact]
    public async Task MissingExecutableIsReportedWithoutUrlOrPath()
    {
        string missing = Path.Combine(workspace, "yt-dlp");
        var result = await new InspectVideoTool(new YtDlpClient(missing), Url).ExecuteAsync(new InspectVideoInput(Url));
        Assert.StartsWith("MissingDependency", result.Error);
        Assert.DoesNotContain(Url, result.Error);
        Assert.DoesNotContain(workspace, result.Error);
    }

    [Fact]
    public void NativeInvocationUsesOneValidatedUrlArgumentAndDisablesAmbientSettings()
    {
        NativeYtDlpProcess worker = new(Path.Combine(workspace, "yt-dlp"));
        var start = worker.BuildStart(workspace, ["--simulate", "--skip-download", "--dump-single-json", "--", Url]);
        Assert.False(start.UseShellExecute);
        Assert.Empty(start.Arguments);
        Assert.Equal(Url, start.ArgumentList[^1]);
        Assert.Equal("--", start.ArgumentList[^2]);
        Assert.Contains("--ignore-config", start.ArgumentList);
        Assert.Contains("--no-plugin-dirs", start.ArgumentList);
        Assert.Contains("--no-js-runtimes", start.ArgumentList);
        Assert.Contains("--no-cookies-from-browser", start.ArgumentList);
        Assert.DoesNotContain("--netrc", start.ArgumentList);
        Assert.DoesNotContain("--netrc-cmd", start.ArgumentList);
        Assert.Contains("--no-playlist", start.ArgumentList);
        Assert.DoesNotContain("--cookies", start.ArgumentList);
        Assert.Equal(workspace, start.WorkingDirectory);
    }

    public void Dispose() => Directory.Delete(workspace, true);

    private sealed class StubProcess : IYtDlpProcess
    {
        internal string Version = YtDlpClient.SupportedVersion;
        internal string? FfmpegVersion;
        internal string Metadata = Json();
        internal int Versions, Inspections, Downloads;
        internal bool FailDownload, Corrupt, Oversized;
        internal string? CreateConflict;
        internal CancellationTokenSource? CancelDownload;
        public Task<string> VersionAsync(CancellationToken cancellationToken) { Versions++; return Task.FromResult(Version); }
        public Task<string?> FfmpegVersionAsync(CancellationToken cancellationToken) => Task.FromResult(FfmpegVersion);
        public Task<string> InspectAsync(string url, CancellationToken cancellationToken) { Inspections++; return Task.FromResult(Metadata); }
        public async Task<string> DownloadAsync(string url, VideoCandidate format, string scratch, IProgress<VideoDownloadProgress>? progress, CancellationToken cancellationToken)
        {
            Downloads++;
            byte[] bytes = new byte[32];
            if (!Corrupt)
            {
                if (format.Extension == "mp4") "ftyp"u8.CopyTo(bytes.AsSpan(4));
                else new byte[] { 0x1a, 0x45, 0xdf, 0xa3 }.CopyTo(bytes, 0);
            }
            string media = Path.Combine(scratch, "media." + format.Extension);
            await File.WriteAllBytesAsync(media, bytes, cancellationToken);
            if (Oversized) { using FileStream file = File.OpenWrite(media); file.SetLength(VideoMetadata.MaxBytes + 1); }
            if (CreateConflict is not null) await File.WriteAllTextAsync(CreateConflict, "original");
            if (CancelDownload is not null) { CancelDownload.Cancel(); cancellationToken.ThrowIfCancellationRequested(); }
            if (FailDownload) { await File.WriteAllTextAsync(Path.Combine(scratch, "media.mp4.part"), "partial"); throw new VideoToolException("SourceUnavailable: stub access failure."); }
            return format.Id;
        }

        internal static string Json(double? duration = 60, string protocol = "https", string vcodec = "avc1", string acodec = "aac",
            bool drm = false, int height = 360, long bytes = 1000, string mediaUrl = "https://r1.googlevideo.com/video?private-token=secret", string extension = "mp4") =>
            JsonSerializer.Serialize(new { title = "Lesson", duration, availability = "public", cookies = "never expose",
                formats = new[] { new { format_id = "18", ext = extension, height, filesize = bytes, protocol, vcodec, acodec, has_drm = drm, url = mediaUrl } } });
    }
}
