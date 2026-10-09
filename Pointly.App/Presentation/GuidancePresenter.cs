using System.Diagnostics;
using System.Windows;

namespace Pointly.App.Presentation;

public sealed class GuidancePresenter : IDisposable
{
    private readonly PresentationLifetime _lifetime = new();
    private readonly Action<string> _log;
    private BuddySurface? _surface;
    private ChatWindow? _chat;
    private GuidancePresentation? _current;
    private Point _anchor;
    private string _state = "Microphone off";
    private CancellationTokenSource? _narration;
    private long _narratedIdentity;
    private int _captureDepth;
    private bool _microphoneOn;
    public Func<string, CancellationToken, Task>? Narrate { get; set; }
    public event Action<string>? QuestionSubmitted;
    public event Action? MicrophoneRequested;
    public event Action? DismissRequested;
    public bool IsVisible => _lifetime.Visible;

    public GuidancePresenter(Action<string> log) => _log = log;

    public void Summon()
    {
        var timer = Stopwatch.StartNew();
        _anchor = DesktopGeometry.Cursor();
        Show(new(_lifetime.Replace(), "", null));
        _chat?.Present(DesktopGeometry.At(_anchor), focusInput: true);
        _log($"SummonToComposerMs={timer.ElapsedMilliseconds}");
    }

    public void ShowTarget(Rect target, bool point, string instruction, AnnotationStyle? style = null)
    {
        var timer = Stopwatch.StartNew();
        CancelNarration();
        _anchor = new(target.X + target.Width / 2, target.Y + target.Height / 2);
        Show(new(_lifetime.Replace(), instruction, target, point, style ?? (point ? AnnotationStyle.Ring : AnnotationStyle.Rectangle)));
        _log($"TargetReadyToGuidanceVisibleMs={timer.ElapsedMilliseconds}");
    }

    public void ShowInstruction(string instruction)
    {
        if (!_lifetime.Visible) Summon();
        CancelNarration();
        Show(new(_lifetime.Replace(), instruction, null));
    }

    private void Show(GuidancePresentation presentation)
    {
        if (!_lifetime.IsCurrent(presentation.Identity)) return;
        _current = presentation;
        if (_captureDepth > 0) return;
        MonitorGeometry monitor = DesktopGeometry.At(_anchor);
        if (_chat is null)
        {
            _chat = new ChatWindow();
            _chat.QuestionSubmitted += question => QuestionSubmitted?.Invoke(question);
            _chat.MicrophoneRequested += () => MicrophoneRequested?.Invoke();
            _chat.DismissRequested += () => DismissRequested?.Invoke();
        }
        _chat.SetInstruction(presentation.Instruction);
        _chat.SetState(_state);
        _chat.SetMicrophoneState(_microphoneOn);
        _chat.Present(monitor);
        if (_surface is null || _surface.Monitor != monitor)
        {
            _surface?.StopMotion(); _surface?.Close();
            _surface = new BuddySurface(monitor);
        }
        _current = presentation;
        if (presentation.Target is not null) _surface.Render(presentation, _anchor, _state, showBuddy: false);
        else _surface.Hide();
    }

    public void SetState(string state) { _state = state; _chat?.SetState(state); }
    public void ShowPartial(string partial) => _chat?.SetPartial(partial);
    public void SetMicrophoneState(bool isOn) { _microphoneOn = isOn; _chat?.SetMicrophoneState(isOn); }
    public void HideInput() => _chat?.Hide();
    public IDisposable HideForCapture()
    {
        _captureDepth++;
        _surface?.Hide();
        _chat?.Hide();
        return new CaptureLease(this);
    }

    private sealed class CaptureLease(GuidancePresenter owner) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (--owner._captureDepth == 0 && owner._current is { } current && owner._lifetime.IsCurrent(current.Identity))
                owner.Show(current);
        }
    }
    public void ClearTarget()
    {
        CancelNarration();
        _surface?.StopMotion(); _surface?.ClearTarget();
        if (_current is not null) _current = _current with { Target = null };
    }

    public async Task SpeakOnceAsync(string instruction, CancellationToken token)
    {
        if (_current is null || !_lifetime.IsCurrent(_current.Identity) || _narratedIdentity == _current.Identity || Narrate is null) return;
        _narratedIdentity = _current.Identity;
        using var narration = CancellationTokenSource.CreateLinkedTokenSource(token);
        _narration = narration;
        try { await Narrate(instruction, narration.Token); }
        finally { if (ReferenceEquals(_narration, narration)) _narration = null; }
    }

    public void CancelNarration() => _narration?.Cancel();
    public void Dismiss()
    {
        _lifetime.Dismiss(); CancelNarration(); _current = null;
        _surface?.StopMotion(); _surface?.Hide();
        _chat?.Hide();
    }

    public void Preview()
    {
#if DEBUG
        Summon();
        SetState("Preview mode");
#endif
    }
    public void Dispose() { Dismiss(); _surface?.Close(); _surface = null; _chat?.Exit(); _chat = null; }
}
