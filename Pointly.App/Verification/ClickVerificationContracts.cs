namespace Pointly.App.Verification;

public enum StepVerificationStatus
{
    Waiting,
    Completed,
    MissedTarget,
    Cancelled,
}

public enum ClickTargetType
{
    Point,
    Box,
}

/// <summary>Physical screen pixels; negative coordinates are valid on secondary monitors.</summary>
public readonly record struct ScreenPoint(double X, double Y);

/// <summary>Immutable physical screen bounds, with inclusive hit-test edges.</summary>
public readonly record struct ScreenRectangle(double Left, double Top, double Right, double Bottom)
{
    public bool Contains(ScreenPoint click) =>
        click.X >= Left && click.X <= Right && click.Y >= Top && click.Y <= Bottom;
}

public sealed class VerifiedClickTarget
{
    public const double DefaultPointToleranceRadiusPx = 30;

    public ClickTargetType Type { get; }
    public ScreenPoint? Point { get; }
    public ScreenRectangle? Rectangle { get; }
    public double PointToleranceRadiusPx { get; }

    private VerifiedClickTarget(ClickTargetType type, ScreenPoint? point,
        ScreenRectangle? rectangle, double pointToleranceRadiusPx)
    {
        Type = type;
        Point = point;
        Rectangle = rectangle;
        PointToleranceRadiusPx = pointToleranceRadiusPx;
    }

    public static VerifiedClickTarget ForBox(ScreenRectangle rectangle)
    {
        if (!double.IsFinite(rectangle.Left) || !double.IsFinite(rectangle.Top) ||
            !double.IsFinite(rectangle.Right) || !double.IsFinite(rectangle.Bottom) ||
            rectangle.Right <= rectangle.Left || rectangle.Bottom <= rectangle.Top)
            throw new ArgumentOutOfRangeException(nameof(rectangle), "Physical box bounds must be finite and positive.");

        return new VerifiedClickTarget(ClickTargetType.Box, null, rectangle, 0);
    }

    public static VerifiedClickTarget ForPoint(ScreenPoint point,
        double toleranceRadiusPx = DefaultPointToleranceRadiusPx)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
            throw new ArgumentOutOfRangeException(nameof(point), "Physical point must be finite.");
        if (!double.IsFinite(toleranceRadiusPx) || toleranceRadiusPx <= 0)
            throw new ArgumentOutOfRangeException(nameof(toleranceRadiusPx));

        return new VerifiedClickTarget(ClickTargetType.Point, point, null, toleranceRadiusPx);
    }

    public bool Contains(ScreenPoint click)
    {
        if (!double.IsFinite(click.X) || !double.IsFinite(click.Y)) return false;
        if (Rectangle is { } box) return box.Contains(click);

        ScreenPoint point = Point!.Value;
        double dx = click.X - point.X;
        double dy = click.Y - point.Y;
        return dx * dx + dy * dy <= PointToleranceRadiusPx * PointToleranceRadiusPx;
    }
}

public sealed record StepVerificationResult(
    StepVerificationStatus Status,
    ClickTargetType TargetType,
    long VerificationDurationMs,
    string? CancellationReason = null);
