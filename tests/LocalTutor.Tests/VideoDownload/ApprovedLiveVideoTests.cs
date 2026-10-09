using LocalTutor.Tools.VideoDownload;

namespace LocalTutor.Tests.VideoDownload;

/// <summary>Run only after a user specifically approves this URL and test. Normal tests skip all network activity.</summary>
public sealed class ApprovedLiveVideoTests
{
    [ApprovedLiveVideoFact]
    public async Task ApprovedSampleCanBeInspectedPreviewedAndDownloaded()
    {
        string url = Environment.GetEnvironmentVariable("LOCAL_TUTOR_APPROVED_VIDEO_URL")!;
        string executable = Environment.GetEnvironmentVariable("LOCAL_TUTOR_YTDLP")!;
        string? root = AppContext.BaseDirectory;
        while (root is not null && !File.Exists(Path.Combine(root, "LocalTutor.slnx"))) root = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(root));
        Assert.NotNull(root);
        string workspace = Path.Combine(root!, "tests", "LocalTutor.Tests", "VideoDownload", ".manual-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            YtDlpClient client = new(executable, Environment.GetEnvironmentVariable("LOCAL_TUTOR_FFMPEG"));
            using CancellationTokenSource deadline = new(TimeSpan.FromMinutes(4));
            var inspected = await new InspectVideoTool(client, url).ExecuteAsync(new InspectVideoInput(url), deadline.Token);
            Assert.True(inspected.Success, inspected.Error);
            Assert.True(inspected.Value!.Formats.Count > 0, "DurationSeconds: " + inspected.Value.DurationSeconds + "; " + string.Join("; ", inspected.Value.Warnings));
            VideoQuality quality = inspected.Value.Formats[0].Quality;
            string extension = inspected.Value.Formats[0].Container;
            DownloadVideoInput input = new(url, Path.Combine(workspace, "approved-sample." + extension), quality);
            var preview = await DownloadVideoTool.PreviewAsync(client, input, workspace, deadline.Token);
            Assert.True(preview.Success, preview.Error);
            // The invoking user approved this exact temporary test effect, including cleanup.
            var result = await new DownloadVideoTool(client, new VideoDownloadApproval(preview.Value!)).ExecuteAsync(input, deadline.Token);
            Assert.True(result.Success, result.Error);
            Assert.Equal("Completed", result.Value!.Status);
            Assert.Equal(new FileInfo(result.Value.Path).Length, result.Value.Bytes);
            Assert.InRange(result.Value.Bytes, 12, 100 * 1024 * 1024);
            Assert.Single(Directory.EnumerateFileSystemEntries(workspace));
        }
        finally { Directory.Delete(workspace, true); }
    }
}

public sealed class ApprovedLiveVideoFactAttribute : FactAttribute
{
    public ApprovedLiveVideoFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("LOCAL_TUTOR_APPROVED_LIVE_VIDEO_TEST") != "1" ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LOCAL_TUTOR_APPROVED_VIDEO_URL")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LOCAL_TUTOR_YTDLP")))
            Skip = "Requires specific user approval, an approved URL, and a trusted pinned yt-dlp executable; no network in default tests.";
    }
}
