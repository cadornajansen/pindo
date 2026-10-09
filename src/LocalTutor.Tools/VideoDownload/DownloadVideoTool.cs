using LocalTutor.Core.Tools;
using LocalTutor.Tools.FileInspectionAndConversion;

namespace LocalTutor.Tools.VideoDownload;

public sealed class DownloadVideoTool(YtDlpClient client, VideoDownloadApproval approval,
    IProgress<VideoDownloadProgress>? progress = null) : LocalTool<DownloadVideoInput, DownloadedVideo>
{
    public override string Id => "video.download";
    public override string Description => "Download one explicitly approved public video to the previewed new local file, using an offered MP4/WebM quality. Maximum ten minutes and 100 MiB; no playlists, credentials, DRM or overwrites.";

    /// <summary>Call only for a URL explicitly selected by the user. This inspects metadata but downloads no media and grants no execution approval.</summary>
    public static async Task<ToolResult<VideoDownloadPreview>> PreviewAsync(YtDlpClient client, DownloadVideoInput input,
        string userSelectedWorkspace, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<string> errors = input.Validate();
        if (errors.Count > 0) return new(false, null, string.Join("; ", errors));
        try
        {
            FileAccessScope paths = new(userSelectedWorkspace, [], [input.DestinationPath]);
            paths.ResolveOutput(input.DestinationPath);
            VideoMetadata metadata = await client.InspectAsync(input.Url, cancellationToken);
            VideoCandidate candidate = metadata.Select(input.Quality) ?? throw new VideoToolException("UnsupportedFormat: this quality is unavailable. Separate HTTPS streams require configured pinned FFmpeg; HLS is disabled.");
            return new(true, new VideoDownloadPreview(input, paths, metadata.Video, candidate));
        }
        catch (Exception e) when (e is VideoToolException or FileToolException) { return new(false, null, e.Message); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        { return new(false, null, "InvalidDestination: select an accessible existing local workspace and a new output file inside it."); }
    }

    protected override async Task<ToolResult<DownloadedVideo>> ExecuteValidatedAsync(DownloadVideoInput input, CancellationToken cancellationToken)
    {
        try { return await DownloadApprovedAsync(input, cancellationToken); }
        catch (Exception e) when (e is VideoToolException or FileToolException) { return new(false, null, e.Message); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { return new(false, null, "OutputFailed: local output could not be saved; existing files were not overwritten."); }
    }

    private async Task<ToolResult<DownloadedVideo>> DownloadApprovedAsync(DownloadVideoInput input, CancellationToken cancellationToken)
    {
        string? scratch = null;
        bool published = false;
        try
        {
            approval.Consume(input);
            VideoDownloadPreview preview = approval.Preview;
            string output = preview.Paths.ResolveOutput(input.DestinationPath);
            VideoMetadata current = await client.InspectAsync(input.Url, cancellationToken);
            VideoCandidate candidate = current.Select(input.Quality) ?? throw new VideoToolException("PreviewChanged: format unavailable; prepare and approve a fresh preview.");
            if (candidate != preview.Candidate || current.Video.Title != preview.Title || current.Video.DurationSeconds != preview.DurationSeconds)
                throw new VideoToolException("PreviewChanged: metadata changed; prepare and approve a fresh preview.");
            await client.CheckMediaHostAsync(candidate, cancellationToken);
            output = preview.Paths.ResolveOutput(output);
            scratch = Path.Combine(Path.GetDirectoryName(output)!, ".localtutor-video-" + Guid.NewGuid().ToString("N"));
            NativeYtDlpProcess.CreateScratch(scratch);
            string downloadedId = await client.Process.DownloadAsync(input.Url, candidate, scratch, progress, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            string media = Path.Combine(scratch, "media." + candidate.Extension);
            if (downloadedId.Trim() != candidate.Id || !File.Exists(media) || Directory.EnumerateFileSystemEntries(scratch).Count() != 1 ||
                (File.GetAttributes(media) & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0)
                throw new VideoToolException("InvalidOutput: yt-dlp did not complete exactly one approved video.");
            long bytes = new FileInfo(media).Length;
            if (bytes is <= 0 or > VideoMetadata.MaxBytes) throw new VideoToolException("ResourceLimit: completed media must be between one byte and 100 MiB.");
            byte[] header = new byte[12];
            using (FileStream file = File.OpenRead(media))
                if (await file.ReadAsync(header, cancellationToken) < 12 ||
                    (candidate.Extension == "mp4" ? !header.AsSpan(4, 4).SequenceEqual("ftyp"u8) : !header.AsSpan(0, 4).SequenceEqual(new byte[] { 0x1a, 0x45, 0xdf, 0xa3 })))
                    throw new VideoToolException("InvalidOutput: media container signature does not match the selected format.");
            preview.Paths.ResolveOutput(output);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(media, output, overwrite: false);
            // The rename is the commit point: cancellation after it cannot undo the saved video.
            published = true;
            try { NativeYtDlpProcess.DeleteScratch(scratch); }
            catch (VideoToolException)
            { return new(true, new DownloadedVideo(output, candidate.Extension, bytes, "CompletedWithCleanupWarning")); }
            scratch = null;
            return new(true, new DownloadedVideo(output, candidate.Extension, bytes, "Completed"));
        }
        finally { if (scratch is not null && !published) NativeYtDlpProcess.DeleteScratch(scratch); }
    }
}
