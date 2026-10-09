using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Pointly.App.Shell;

public sealed class TrayShell : IDisposable
{
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _menu = new();
    private readonly Icon _icon;
    public TrayShell(Action toggle, Action<bool> mute, Action interrupt, Action diagnostics, Action exit)
    {
        _icon = CreateIcon();
        _menu.Items.Add("Summon / dismiss Pindo", null, (_, _) => toggle());
        var muted = new ToolStripMenuItem("Microphone muted") { CheckOnClick = true };
        muted.CheckedChanged += (_, _) => mute(muted.Checked);
        _menu.Items.Add(muted);
        _menu.Items.Add("Interrupt and listen", null, (_, _) => interrupt());
        _menu.Items.Add("Development diagnostics", null, (_, _) => diagnostics());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Exit Pindo", null, (_, _) => exit());
        _tray = new NotifyIcon { Icon = _icon, Text = "Pindo — Ctrl+Space", ContextMenuStrip = _menu, Visible = true };
        _tray.DoubleClick += (_, _) => toggle();
    }
    public void Report(string text)
    {
        _tray.BalloonTipTitle = "Pindo"; _tray.BalloonTipText = text;
        _tray.ShowBalloonTip(6000);
    }
    private static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        using var fill = new SolidBrush(Color.FromArgb(117, 230, 203));
        using var outline = new Pen(Color.FromArgb(21, 37, 54), 2);
        Point[] points = [new(4, 3), new(27, 5), new(23, 24), new(25, 30), new(15, 26), new(4, 26)];
        graphics.FillPolygon(fill, points); graphics.DrawPolygon(outline, points);
        graphics.FillEllipse(Brushes.MidnightBlue, 10, 12, 3, 4); graphics.FillEllipse(Brushes.MidnightBlue, 20, 12, 3, 4);
        nint handle = bitmap.GetHicon();
        try { using Icon source = Icon.FromHandle(handle); return (Icon)source.Clone(); }
        finally { DestroyIcon(handle); }
    }
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
    public void Dispose() { _tray.Visible = false; _tray.Dispose(); _menu.Dispose(); _icon.Dispose(); }
}
