using System.Windows.Automation;

namespace Pointly.App.Automation;

/// <summary>
/// Focused UI Automation queries rooted at a foreground HWND.
/// Searches are provider-side filtered. D2 returns the best name match;
/// D4/D5 collect a compact actionable-control snapshot and resolve a selected runtime ID.
/// </summary>
public sealed class UiAutomationService
{
    // Request-scoped IDs map to real UIA runtime IDs, which never leave this process.
    public sealed record TutorCandidate(string Id, UiElementInfo Info, int[] RuntimeId);

    public IReadOnlyList<TutorCandidate> CollectTutorCandidates(nint hwnd, CancellationToken cancellationToken,
        bool preferInputControls = false)
    {
        if (Pointly.App.Windows.TargetWindowContext.Capture(hwnd) is null)
            throw new InvalidOperationException("TargetWindowUnavailable");
        AutomationElement root = AutomationElement.FromHandle(hwnd);
        var candidates = new List<TutorCandidate>();
        var runtimeIds = new HashSet<string>(StringComparer.Ordinal);
        ControlType[] types =
        [
            ControlType.TabItem,
            ControlType.Button,
            ControlType.MenuItem,
            ControlType.ListItem,
            ControlType.Hyperlink,
            ControlType.Edit,
            ControlType.ComboBox,
            ControlType.CheckBox,
            ControlType.RadioButton,
            ControlType.TreeItem,
        ];
        if (preferInputControls)
            types = [ControlType.Edit, ControlType.ComboBox, .. types.Where(type =>
                type != ControlType.Edit && type != ControlType.ComboBox)];

        foreach (ControlType type in types)
        {
            AutomationElementCollection matches = root.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, type));
            int examined = 0;
            foreach (AutomationElement element in matches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (examined++ >= MaxCandidatesPerType) break;
                UiElementInfo? info = TrySnapshot(element);
                if (info is null || !IsPlausibleTarget(info) || string.IsNullOrWhiteSpace(info.Name)) continue;
                try
                {
                    int[] runtimeId = element.GetRuntimeId();
                    if (!runtimeIds.Add(string.Join('.', runtimeId))) continue;
                    candidates.Add(new TutorCandidate($"element-{candidates.Count + 1}", info, runtimeId));
                }
                catch (ElementNotAvailableException) { continue; }
                if (candidates.Count == RequestCandidateLimit) return candidates;
            }
        }
        return candidates;
    }

    public UiElementInfo? ResolveTutorTarget(nint hwnd, TutorCandidate candidate, CancellationToken cancellationToken)
    {
        if (Pointly.App.Windows.TargetWindowContext.Capture(hwnd) is null)
            throw new InvalidOperationException("TargetWindowUnavailable");
        AutomationElement root = AutomationElement.FromHandle(hwnd);
        AutomationElementCollection matches = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
        int examined = 0;
        foreach (AutomationElement element in matches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (examined++ >= MaxResolutionCandidates) break;
            try
            {
                if (!element.GetRuntimeId().SequenceEqual(candidate.RuntimeId)) continue;
                UiElementInfo? current = TrySnapshot(element);
                return current is not null && IsPlausibleTarget(current) &&
                    current.Name == candidate.Info.Name && current.AutomationId == candidate.Info.AutomationId
                    ? current : null;
            }
            catch (ElementNotAvailableException) { continue; }
        }
        return null;
    }

    /// <summary>Upper bound on matches examined per query to avoid pathological trees.</summary>
    private const int RequestCandidateLimit = 40;
    private const int MaxCandidatesPerType = 200;
    private const int MaxResolutionCandidates = 2_000;

    /// <summary>
    /// Finds the most plausible visible element named <paramref name="name"/>
    /// under <paramref name="hwnd"/>, optionally restricted to <paramref name="controlType"/>.
    /// </summary>
    /// <remarks>
    /// Heuristic (documented per D2): applications such as Excel may expose
    /// several elements with the same visible name (ribbon tab, collapsed /
    /// overflow copies, quick-access duplicates). Candidates are filtered to
    /// enabled, on-screen elements with non-empty bounds; ties are broken by
    /// largest area, which favors the real ribbon tab over small duplicates.
    /// Returns null when nothing plausible is found.
    /// </remarks>
    public UiElementInfo? FindBest(nint hwnd, string name, ControlType? controlType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        AutomationElement? root;
        try
        {
            root = AutomationElement.FromHandle(hwnd);
        }
        catch (Exception)
        {
            return null;
        }

        if (root is null)
        {
            return null;
        }

        Condition condition = controlType is null
            ? new PropertyCondition(AutomationElement.NameProperty, name)
            : new AndCondition(
                new PropertyCondition(AutomationElement.NameProperty, name),
                new PropertyCondition(AutomationElement.ControlTypeProperty, controlType));

        AutomationElementCollection matches;
        try
        {
            matches = root.FindAll(TreeScope.Descendants, condition);
        }
        catch (Exception)
        {
            // Target window gone or its provider unavailable.
            return null;
        }

        UiElementInfo? best = null;
        double bestArea = -1;
        int examined = 0;

        foreach (AutomationElement element in matches)
        {
            if (examined++ >= MaxCandidatesPerType)
            {
                break;
            }

            UiElementInfo? info = TrySnapshot(element);
            if (info is null || !IsPlausibleTarget(info))
            {
                continue;
            }

            double area = info.BoundingRectangle.Width * info.BoundingRectangle.Height;
            if (area > bestArea)
            {
                best = info;
                bestArea = area;
            }
        }

        return best;
    }

    private static bool IsPlausibleTarget(UiElementInfo info) =>
        info.IsEnabled &&
        !info.IsOffscreen &&
        !info.BoundingRectangle.IsEmpty &&
        info.BoundingRectangle.Width > 0 &&
        info.BoundingRectangle.Height > 0;

    private static UiElementInfo? TrySnapshot(AutomationElement element)
    {
        try
        {
            if (element.Current.ProcessId == Environment.ProcessId) return null;
            return new UiElementInfo(
                element.Current.Name ?? string.Empty,
                element.Current.AutomationId ?? string.Empty,
                element.Current.ControlType.ProgrammaticName ?? string.Empty,
                element.Current.BoundingRectangle,
                element.Current.IsEnabled,
                element.Current.IsOffscreen);
        }
        catch (ElementNotAvailableException)
        {
            // Element vanished between FindAll and property read.
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
