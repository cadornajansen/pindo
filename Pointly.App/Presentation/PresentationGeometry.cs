using System.Windows;

namespace Pointly.App.Presentation;

public enum AnnotationStyle { Pointer, Ring, Rectangle, Underline, Stroke, Spotlight }
public sealed record GuidancePresentation(long Identity, string Instruction, Rect? Target,
    bool IsPoint = false, AnnotationStyle Style = AnnotationStyle.Pointer);
public sealed record BuddyPlacement(Rect Buddy, Rect Bubble);

public static class PresentationGeometry
{
    public static Point ToDip(Point physical, Rect monitor, double scale) =>
        new((physical.X - monitor.X) / scale, (physical.Y - monitor.Y) / scale);

    public static BuddyPlacement Place(Rect workArea, Rect target, double scale)
    {
        double buddy = 48 * scale, gap = 16 * scale;
        double width = Math.Min(264 * scale, workArea.Width - 16 * scale);
        double height = Math.Min(108 * scale, workArea.Height - 16 * scale);
        var options = new[]
        {
            new Rect(target.Right + gap, target.Top, width + buddy + gap, Math.Max(height, buddy)),
            new Rect(target.Left - width - buddy - 2 * gap, target.Top, width + buddy + gap, Math.Max(height, buddy)),
            new Rect(target.Left, target.Bottom + gap, width + buddy + gap, Math.Max(height, buddy)),
            new Rect(target.Left, target.Top - height - gap, width + buddy + gap, Math.Max(height, buddy))
        };
        Rect chosen = options.Select(rect => Clamp(rect, workArea))
            .OrderBy(rect => IntersectionArea(rect, target)).First();
        return new(new Rect(chosen.X, chosen.Y, buddy, buddy),
            new Rect(chosen.X + buddy + gap, chosen.Y, width, height));
    }

    public static Rect Clamp(Rect value, Rect work)
    {
        double width = Math.Min(value.Width, work.Width), height = Math.Min(value.Height, work.Height);
        return new Rect(Math.Clamp(value.X, work.Left, work.Right - width),
            Math.Clamp(value.Y, work.Top, work.Bottom - height), width, height);
    }

    private static double IntersectionArea(Rect left, Rect right)
    {
        left.Intersect(right);
        return left.IsEmpty ? 0 : left.Width * left.Height;
    }
}

public sealed class PresentationLifetime
{
    public long Identity { get; private set; }
    public bool Visible { get; private set; }
    public long Replace() { Visible = true; return ++Identity; }
    public bool IsCurrent(long identity) => Visible && Identity == identity;
    public void Dismiss() { Visible = false; ++Identity; }
}
