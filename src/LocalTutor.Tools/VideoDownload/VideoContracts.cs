using System.Text.Json.Serialization;
using LocalTutor.Core.Tools;
using LocalTutor.Tools.FileInspectionAndConversion;

namespace LocalTutor.Tools.VideoDownload;

[JsonConverter(typeof(JsonStringEnumConverter<VideoQuality>))]
public enum VideoQuality { Mp4_360p, Mp4_720p, WebM_720p }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InspectVideoInput([property: JsonRequired] string Url) : ToolInput
{
    public override IReadOnlyList<string> Validate() => VideoUrl.Validate(Url);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DownloadVideoInput(
    [property: JsonRequired] string Url,
    [property: JsonRequired] string DestinationPath,
    [property: JsonRequired] VideoQuality Quality) : ToolInput
{
    public override IReadOnlyList<string> Validate()
    {
        List<string> errors = [.. VideoUrl.Validate(Url), .. FileAccessScope.ValidatePath(DestinationPath)];
        if (!Enum.IsDefined(Quality)) errors.Add("UnsupportedQuality: choose an offered video quality.");
        if (string.IsNullOrWhiteSpace(DestinationPath) || !Path.IsPathFullyQualified(DestinationPath))
            errors.Add("InvalidDestination: select an absolute local output path.");
        else if (!System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(DestinationPath), @"\A[A-Za-z0-9][A-Za-z0-9._ -]{0,100}\.(mp4|webm)\z") ||
                 Path.GetExtension(DestinationPath) != "." + VideoMetadata.Container(Quality))
            errors.Add("InvalidFilename: choose a short ASCII filename matching the selected container.");
        return errors;
    }
}

public sealed record VideoFormatChoice(VideoQuality Quality, string Container, int Height, long? EstimatedSizeBytes, bool RequiresFfmpeg = false);
public sealed record InspectedVideo(string Title, double? DurationSeconds, string Source,
    IReadOnlyList<VideoFormatChoice> Formats, IReadOnlyList<string> Warnings);
public sealed record DownloadedVideo(string Path, string Format, long Bytes, string Status);
public sealed record VideoDownloadProgress(string Stage, long Bytes, long? EstimatedBytes);

/// <summary>Produced by PreviewAsync; display these fields before asking the user to approve.</summary>
public sealed class VideoDownloadPreview
{
    public string Source { get; }
    public string Title { get; }
    public double DurationSeconds { get; }
    public string Filename => Path.GetFileName(DestinationPath);
    public string DestinationPath { get; }
    public VideoFormatChoice Format { get; }
    internal DownloadVideoInput Input { get; }
    internal FileAccessScope Paths { get; }
    internal VideoCandidate Candidate { get; }
    internal DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;

    internal VideoDownloadPreview(DownloadVideoInput input, FileAccessScope paths, InspectedVideo video, VideoCandidate candidate)
    {
        Input = input; Paths = paths; Candidate = candidate;
        Source = video.Source; Title = video.Title; DurationSeconds = video.DurationSeconds!.Value;
        DestinationPath = input.DestinationPath; Format = candidate.Choice(input.Quality);
    }
}

/// <summary>Trusted host consent, never a model argument. Create only after explicit approval of the displayed preview and download rights.</summary>
public sealed class VideoDownloadApproval(VideoDownloadPreview preview)
{
    internal VideoDownloadPreview Preview { get; } = preview ?? throw new ArgumentNullException(nameof(preview));
    private int consumed;

    internal void Consume(DownloadVideoInput input, DateTimeOffset? now = null)
    {
        if (input != Preview.Input) throw new VideoToolException("ApprovalMismatch: approve this exact URL, quality and destination.");
        if ((now ?? DateTimeOffset.UtcNow) - Preview.CreatedAt > TimeSpan.FromMinutes(5))
            throw new VideoToolException("ApprovalExpired: prepare and approve a fresh preview.");
        if (Interlocked.Exchange(ref consumed, 1) != 0)
            throw new VideoToolException("ApprovalConsumed: a new approval is required for every download attempt.");
    }
}

internal sealed class VideoToolException(string message) : Exception(message);
