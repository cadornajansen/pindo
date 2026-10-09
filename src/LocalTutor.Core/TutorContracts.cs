namespace LocalTutor.Core;

/// <summary>
/// Bounds in physical screen pixels. Left and Top may be negative on secondary monitors.
/// </summary>
public sealed record ScreenRectangle(double Left, double Top, double Width, double Height);

public sealed record UiElementSnapshot(
    string Id,
    string Name,
    string ControlType,
    ScreenRectangle Bounds,
    bool? IsEnabled = null);

public sealed record TutorRequest(
    string Instruction,
    string ActiveApplicationName,
    IReadOnlyList<UiElementSnapshot> Elements);

public sealed record TutorResponse(
    string? TargetElementId,
    string Instruction,
    bool Success,
    string? Error = null);

public interface ILocalTutorService
{
    Task<TutorResponse> GetNextStepAsync(
        TutorRequest request,
        CancellationToken cancellationToken = default);
}
