using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Pointly.App.Tools;

namespace Pointly.App.Presentation;

public sealed class ToolReviewWindow : Window
{
    public ToolReviewWindow(string title, string details, bool allowRun)
    {
        Title = title;
        Width = 650; Height = 540; MinWidth = 420; MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(38, 38, 38));
        Foreground = Brushes.WhiteSmoke;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 14;
        Topmost = true;
        var layout = new DockPanel { Margin = new Thickness(22) };
        var heading = new TextBlock { Text = title, FontSize = 22, Margin = new Thickness(0, 0, 0, 16) };
        DockPanel.SetDock(heading, Dock.Top); layout.Children.Add(heading);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancel = new Button { Content = allowRun ? "Cancel" : "Close", IsCancel = true, Padding = new Thickness(16, 6, 16, 6) };
        cancel.Click += (_, _) => { DialogResult = false; };
        actions.Children.Add(cancel);
        if (allowRun)
        {
            var run = new Button { Content = "Run", Padding = new Thickness(22, 6, 22, 6), Margin = new Thickness(12, 0, 0, 0) };
            System.Windows.Automation.AutomationProperties.SetName(run, "Run reviewed operation");
            run.Click += (_, _) => { DialogResult = true; };
            actions.Children.Add(run);
        }
        DockPanel.SetDock(actions, Dock.Bottom); layout.Children.Add(actions);
        layout.Children.Add(new TextBox { Text = details, IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Brushes.Transparent,
            Foreground = Brushes.WhiteSmoke, BorderThickness = new Thickness(0), Padding = new Thickness(0, 0, 12, 0) });
        Content = layout;
    }
}
