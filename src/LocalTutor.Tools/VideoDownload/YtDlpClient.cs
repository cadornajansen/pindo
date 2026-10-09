using System.Net;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("LocalTutor.Tests")]

namespace LocalTutor.Tools.VideoDownload;

/// <summary>Trusted native configuration, never deserialized from model output. No automatic installation or updates.</summary>
public sealed class YtDlpClient
{
    public const string SupportedVersion = "2026.08.19";
    public const string SupportedFfmpegVersion = "9.0.1";
    internal IYtDlpProcess Process { get; }
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> resolve;
    private readonly string? ffmpegPath;

    public YtDlpClient(string executablePath, string? ffmpegExecutablePath = null)
        : this(new NativeYtDlpProcess(executablePath, ffmpegExecutablePath), (host, token) => Dns.GetHostAddressesAsync(host, token))
    { ffmpegPath = ffmpegExecutablePath; }

    internal YtDlpClient(IYtDlpProcess process, Func<string, CancellationToken, Task<IPAddress[]>> resolver)
    { Process = process; resolve = resolver; }

    internal async Task<VideoMetadata> InspectAsync(string url, CancellationToken cancellationToken)
    {
        if (VideoUrl.Validate(url).Count != 0) throw new VideoToolException("InvalidUrl: select a supported public video URL.");
        // Check on each operation, so replacing/updating the installed binary cannot bypass the version pin.
        if ((await Process.VersionAsync(cancellationToken)).Trim() != SupportedVersion)
            throw new VideoToolException("VersionMismatch: install and verify the pinned yt-dlp " + SupportedVersion + " release.");
        string? ffmpegVersion = await Process.FfmpegVersionAsync(cancellationToken);
        string ffmpegPrefix = "ffmpeg version " + SupportedFfmpegVersion;
        if (ffmpegVersion is not null &&
            !ffmpegVersion.StartsWith(ffmpegPrefix + " ", StringComparison.Ordinal) &&
            !ffmpegVersion.StartsWith(ffmpegPrefix + "-", StringComparison.Ordinal))
            throw new VideoToolException("VersionMismatch: configure the pinned FFmpeg " + SupportedFfmpegVersion + " release or omit optional FFmpeg configuration.");
        await VideoUrl.CheckNetworkAsync(url, resolve, cancellationToken);
        return VideoMetadata.Parse(await Process.InspectAsync(url, cancellationToken), url, ffmpegVersion is not null);
    }

    internal async Task CheckMediaHostAsync(VideoCandidate candidate, CancellationToken cancellationToken)
    {
        await VideoUrl.CheckNetworkAsync("https://" + candidate.MediaHost + "/", resolve, cancellationToken);
        if (candidate.AudioMediaHost is not null) await VideoUrl.CheckNetworkAsync("https://" + candidate.AudioMediaHost + "/", resolve, cancellationToken);
    }

    internal string FfmpegNotice => ffmpegPath is null
        ? "FFmpegNotConfigured: combined formats do not require it; separate streams require the pinned FFmpeg."
        : !File.Exists(ffmpegPath)
            ? "FFmpegMissing: the configured file is missing; only combined formats are available."
            : "FFmpegReady: pinned version checked; separate HTTPS streams may be merged without re-encoding.";
}
