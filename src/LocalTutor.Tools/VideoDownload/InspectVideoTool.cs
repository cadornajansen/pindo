using LocalTutor.Core.Tools;

namespace LocalTutor.Tools.VideoDownload;

/// <summary>Bind the exact URL selected by the user in trusted caller configuration.</summary>
public sealed class InspectVideoTool(YtDlpClient client, string userApprovedUrl) : LocalTool<InspectVideoInput, InspectedVideo>
{
    public override string Id => "video.inspect_url";
    public override string Description => "Inspect one user-selected public HTTPS video with local yt-dlp, without downloading media. Returns bounded MP4/WebM choices and whether optional FFmpeg is needed. Only unrestricted YouTube, Vimeo and direct Wikimedia Commons videos are supported.";

    protected override async Task<ToolResult<InspectedVideo>> ExecuteValidatedAsync(InspectVideoInput input, CancellationToken cancellationToken)
    {
        if (input.Url != userApprovedUrl) return new(false, null, "UrlNotApproved: select and approve this exact video URL.");
        try
        {
            InspectedVideo video = (await client.InspectAsync(input.Url, cancellationToken)).Video;
            return new(true, video with { Warnings = Array.AsReadOnly([.. video.Warnings, client.FfmpegNotice]) });
        }
        catch (VideoToolException e) { return new(false, null, e.Message); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { return new(false, null, "LocalAccessFailed: native video inspection could not access its local resources."); }
    }
}
