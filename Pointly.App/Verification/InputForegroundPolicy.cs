using Pointly.App.Interop;

namespace Pointly.App.Verification;

public enum InputForegroundDisposition { Observe, Ignore, Cancel }

/// <summary>A focus change pauses keyboard verification; a destroyed target window cancels it.</summary>
public static class InputForegroundPolicy
{
    public static InputForegroundDisposition Decide(bool targetWindowExists,
        uint expectedProcessId, uint foregroundProcessId)
    {
        if (!targetWindowExists) return InputForegroundDisposition.Cancel;
        return expectedProcessId != 0 && expectedProcessId == foregroundProcessId
            ? InputForegroundDisposition.Observe
            : InputForegroundDisposition.Ignore;
    }

    internal static InputForegroundDisposition Capture(nint targetHwnd, uint expectedProcessId,
        nint foregroundHwnd)
    {
        bool exists = NativeMethods.IsWindow(targetHwnd);
        if (exists && targetHwnd != foregroundHwnd) return InputForegroundDisposition.Ignore;
        NativeMethods.GetWindowThreadProcessId(foregroundHwnd, out uint foregroundProcessId);
        return Decide(exists, expectedProcessId, foregroundProcessId);
    }
}
