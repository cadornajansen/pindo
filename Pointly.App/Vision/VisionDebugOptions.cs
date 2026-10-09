namespace Pointly.App.Vision;

internal static class VisionDebugOptions
{
    public static string? GroundingQuery
    {
        get
        {
#if DEBUG
            string? value = Environment.GetEnvironmentVariable("POINTLY_DEBUG_GROUNDING_QUERY");
            return ForceVision && !string.IsNullOrWhiteSpace(value) && value.Length <= 500
                ? value
                : null;
#else
            return null;
#endif
        }
    }

    public static bool ForceVision
    {
        get
        {
#if DEBUG
            return Environment.GetEnvironmentVariable("POINTLY_DEBUG_FORCE_VISION") is string value &&
                (value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                 value.Equals("true", StringComparison.OrdinalIgnoreCase));
#else
            return false;
#endif
        }
    }
}
