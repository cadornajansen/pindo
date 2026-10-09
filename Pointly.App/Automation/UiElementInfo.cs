using System.Windows;

namespace Pointly.App.Automation;

/// <summary>
/// Minimal typed snapshot of a UI Automation element.
/// <see cref="BoundingRectangle"/> is in physical screen pixels, per the UIA contract.
/// </summary>
public sealed record UiElementInfo(
    string Name,
    string AutomationId,
    string ControlType,
    Rect BoundingRectangle,
    bool IsEnabled,
    bool IsOffscreen);
