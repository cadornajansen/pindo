using System.Text.Json;
using System.Text.RegularExpressions;

namespace LocalTutor.Tools.VideoDownload;

internal sealed record VideoCandidate(string Id, string Extension, int Height, long? Bytes, bool Estimated, string MediaHost, string? AudioMediaHost = null)
{
    internal VideoFormatChoice Choice(VideoQuality quality) => new(quality, Extension, Height, Bytes, AudioMediaHost is not null);
}

internal sealed class VideoMetadata
{
    internal const long MaxBytes = 100 * 1024 * 1024;
    internal const double MaxDuration = 600;
    internal InspectedVideo Video { get; }
    private readonly List<VideoCandidate> candidates;

    private VideoMetadata(InspectedVideo video, List<VideoCandidate> formats) { Video = video; candidates = formats; }
    internal static string Container(VideoQuality quality) => quality == VideoQuality.WebM_720p ? "webm" : "mp4";
    internal VideoCandidate? Select(VideoQuality quality) => candidates
        .Where(f => f.Extension == Container(quality) && f.Height <= (quality == VideoQuality.Mp4_360p ? 360 : 720))
        .OrderByDescending(f => f.Height).ThenBy(f => f.Bytes ?? long.MaxValue).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();

    internal static VideoMetadata Parse(string json, string url, bool ffmpegAvailable = false)
    {
        if (json.Length > 2 * 1024 * 1024) throw new VideoToolException("ResourceLimit: metadata exceeded its size budget.");
        try
        {
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.TryGetProperty("entries", out _) ||
                Text(root, "_type") is not (null or "video"))
                throw new VideoToolException("UnsupportedVideo: only one video is supported; playlists and redirects are disabled.");
            if (Flag(root, "has_drm") || Flag(root, "is_live") || Text(root, "live_status") is "is_live" or "is_upcoming" ||
                Number(root, "age_limit") is > 0 || Text(root, "availability") is not (null or "public"))
                throw new VideoToolException("AccessDenied: only public, unrestricted, non-live videos without DRM are supported.");
            string? title = Text(root, "title");
            if (string.IsNullOrWhiteSpace(title) || title.Length > 300 || title.Any(char.IsControl))
                throw new VideoToolException("InvalidMetadata: a bounded video title is required.");
            double? duration = Number(root, "duration");
            if (duration.HasValue && (!double.IsFinite(duration.Value) || duration <= 0))
                throw new VideoToolException("InvalidMetadata: video duration must be a positive finite number.");
            List<VideoCandidate> candidates = [];
            List<VideoCandidate> separateVideo = [], separateAudio = [];
            if (root.TryGetProperty("formats", out JsonElement formats) && formats.ValueKind == JsonValueKind.Array)
            {
                if (formats.GetArrayLength() > 500) throw new VideoToolException("ResourceLimit: too many video formats.");
                HashSet<string> identifiers = new(StringComparer.Ordinal);
                foreach (JsonElement format in formats.EnumerateArray())
                {
                    string? id = Text(format, "format_id"), ext = Text(format, "ext");
                    if (id is not null && !identifiers.Add(id)) throw new VideoToolException("InvalidMetadata: format identifiers must be unique.");
                    double? height = Number(format, "height"), size = Number(format, "filesize") ?? Number(format, "filesize_approx");
                    string? mediaUrl = Text(format, "url");
                    string? vcodec = Text(format, "vcodec"), acodec = Text(format, "acodec");
                    if (id is null || !Regex.IsMatch(id, @"\A[A-Za-z0-9_-]{1,64}\z") || ext is not ("mp4" or "webm" or "m4a") ||
                        Text(format, "protocol") != "https" || Flag(format, "has_drm") ||
                        size.HasValue && (!double.IsFinite(size.Value) || size <= 0 || size > MaxBytes) ||
                        !Uri.TryCreate(mediaUrl, UriKind.Absolute, out Uri? media) || media.Scheme != "https" ||
                        media.Port != 443 || media.UserInfo.Length != 0 || !AllowedMediaHost(media.Host, VideoUrl.Source(url))) continue;
                    VideoCandidate candidate = new(id, ext, 0, size.HasValue ? (long)size.Value : null, Number(format, "filesize") is null, media.DnsSafeHost);
                    if (vcodec == "none" && acodec is not (null or "none") && ext is "m4a" or "webm")
                    { if (ffmpegAvailable) separateAudio.Add(candidate); continue; }
                    if (vcodec is null or "none" || height is null or <= 0 or > 720 || height != Math.Floor(height.Value) || ext == "m4a") continue;
                    candidate = candidate with { Height = (int)height.Value };
                    if (acodec is not (null or "none")) candidates.Add(candidate);
                    else if (ffmpegAvailable && acodec == "none") separateVideo.Add(candidate);
                }
            }
            foreach (VideoCandidate video in separateVideo)
            {
                VideoCandidate? audio = separateAudio.Where(a => a.Extension == (video.Extension == "mp4" ? "m4a" : "webm"))
                    .OrderBy(a => a.Bytes ?? long.MaxValue).ThenBy(a => a.Id, StringComparer.Ordinal).FirstOrDefault();
                if (audio is null) continue;
                long? combined = video.Bytes.HasValue && audio.Bytes.HasValue ? video.Bytes + audio.Bytes : null;
                if (combined > MaxBytes) continue;
                candidates.Add(video with { Id = video.Id + "+" + audio.Id, Bytes = combined, Estimated = video.Estimated || audio.Estimated, AudioMediaHost = audio.MediaHost });
            }
            List<string> warnings = [];
            if (duration is null) { warnings.Add("Duration unknown; download is disabled."); candidates.Clear(); }
            if (duration > MaxDuration) { warnings.Add("DownloadLimit: this video exceeds the ten-minute download limit."); candidates.Clear(); }
            VideoMetadata metadata = new(new InspectedVideo(title, duration, VideoUrl.Source(url), [], warnings), candidates);
            List<VideoFormatChoice> choices = [];
            foreach (VideoQuality quality in Enum.GetValues<VideoQuality>())
                if (metadata.Select(quality) is VideoCandidate choice) choices.Add(choice.Choice(quality));
            if (choices.Count == 0) warnings.Add(ffmpegAvailable
                ? "No supported HTTPS MP4/WebM format at 720p or below; HLS and other transports are disabled."
                : "No supported combined audio/video HTTPS format at 720p or below. Configure the pinned FFmpeg to merge separate HTTPS streams.");
            return new VideoMetadata(metadata.Video with { Formats = choices.AsReadOnly(), Warnings = warnings.AsReadOnly() }, candidates);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        { throw new VideoToolException("InvalidMetadata: yt-dlp returned malformed or unsupported metadata."); }
    }

    private static bool AllowedMediaHost(string host, string source) => source switch
    {
        "YouTube" => host.EndsWith(".googlevideo.com", StringComparison.Ordinal),
        "Vimeo" => host.EndsWith(".vimeocdn.com", StringComparison.Ordinal) || host.EndsWith(".akamaized.net", StringComparison.Ordinal),
        _ => host == "upload.wikimedia.org"
    };
    private static string? Text(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null) return null;
        return value.GetString();
    }
    private static double? Number(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null) return null;
        return value.GetDouble();
    }
    private static bool Flag(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null) return false;
        return value.GetBoolean();
    }
}
