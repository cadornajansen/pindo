namespace Pointly.App.Vision;

public interface IGuiGroundingModel : IDisposable
{
    string ProviderName { get; }
    string ModelId { get; }

    Task<GuiGroundingResult> GroundAsync(
        GuiGroundingRequest request,
        CancellationToken cancellationToken);
}

public sealed record VisionUiCandidate(
    string Name,
    string AutomationId,
    string ControlType);

public sealed record GuiGroundingRequest(
    string UserQuery,
    string ForegroundProcessName,
    string WindowTitle,
    byte[] ScreenshotPng,
    int ScreenshotWidth,
    int ScreenshotHeight,
    IReadOnlyList<VisionUiCandidate> UiCandidates);

/// <summary>Image-relative coordinates on a 0..1000 scale.</summary>
public sealed record NormalizedBoundingBox(
    double X1,
    double Y1,
    double X2,
    double Y2);

public sealed record NormalizedGroundingPoint(double X, double Y);

public sealed record GuiGroundingResult(
    string? TargetLabel,
    NormalizedGroundingPoint? Point,
    NormalizedBoundingBox? BoundingBox,
    double? Confidence,
    string? Description,
    string? Instruction = null);

public enum GroundingFailureReason
{
    InvalidRequest,
    InvalidResponse,
    NoTarget,
    LowConfidence,
    Configuration,
    MissingOpenRouterApiKey,
    Authentication,
    RateLimited,
    ProviderHttp,
    Network,
    CaptureUnavailable,
    ForegroundChanged,
    Cancelled,
    Timeout,
    Unknown,
}

public sealed class GuiGroundingException(
    string message,
    GroundingFailureReason reason = GroundingFailureReason.InvalidResponse) : Exception(message)
{
    public GroundingFailureReason Reason { get; } = reason;
}
