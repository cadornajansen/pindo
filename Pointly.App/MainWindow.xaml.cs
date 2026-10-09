using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Windows;
using Amazon.Runtime;
using Pointly.App.Automation;
using Pointly.App.Capture;
using Pointly.App.Input;
using Pointly.App.Presentation;
using Pointly.App.Shell;
using Pointly.App.Interop;
using Pointly.App.Tutor;
using Pointly.App.Verification;
using Pointly.App.Voice;
using Pointly.App.Vision;
using Pointly.App.Walkthrough;
using Pointly.App.Windows;

namespace Pointly.App;

public partial class MainWindow : Window
{
    private const double MinimumUiaConfidence = 0.65;
    private readonly ForegroundWindowService _foreground = new();
    private readonly UiAutomationService _automation = new();
    private readonly WindowCaptureService _capture = new();
    private readonly AssemblyAiTutorModel _tutorModel = new();
    private readonly TutorService _tutor;
    private readonly IGuiGroundingModel _visionModel;
    private readonly GuiGroundingService _vision;
    private readonly ClickVerificationService _verification;
    private readonly UiAutomationStateVerifier _uiStateVerifier = new();
    private readonly StepVerificationCoordinator _stepVerification;
    private readonly WalkthroughService _walkthrough = new();
    private readonly IVoiceSession _voice;
    private string? _lastVoiceInstruction;
    private string? _visibleFinalTranscript;
    private CancellationTokenSource? _invocation;
    private CancellationTokenSource? _walkthroughCancellation;
    private (long SessionId, int StepIndex)? _activeWalkthroughTarget;
    private TargetWindowContext? _walkthroughContext;
    private readonly TargetContextWatcher _targetWatcher;
    private bool _closed;
    private GlobalHotkey? _hotkey;
    private readonly GuidancePresenter _presenter;
    private TrayShell? _tray;
    private GlobalHotkey? _interruptHotkey;
    private bool _exitRequested;
    private bool _microphoneMuted;
    private ForegroundWindowInfo? _composerTarget;

    public MainWindow()
    {
        InitializeComponent();
        _presenter = new GuidancePresenter(Log);
        _presenter.QuestionSubmitted += OnTypedQuestion;
        _presenter.MicrophoneRequested += OnMicrophoneRequested;
        _presenter.DismissRequested += () => DismissSession("EscapePressed");
        _presenter.CheckRequested += OnCheckGoal;
        _targetWatcher = new TargetContextWatcher(Dispatcher, reason =>
        {
            ReplaceGuidance(reason);
            if (_goal.Active) PauseGoal("The window changed. Return to the target app and select Check again.");
            else _presenter.ShowInstruction("The window changed. Ask again to locate a fresh target.");
            Log($"TargetInvalidated Reason={reason}");
        });
        _tutor = new TutorService(_tutorModel);
        _visionModel = GuiGroundingModelFactory.CreateConfigured(out string? providerWarning);
        _vision = new GuiGroundingService(_visionModel);
        _verification = new ClickVerificationService(Dispatcher);
        _verification.ResultChanged += OnVerificationResult;
        _stepVerification = new StepVerificationCoordinator(Dispatcher, _uiStateVerifier)
        { ContextValid = () => _targetWatcher.IsValid };
        _stepVerification.ResultChanged += OnWalkthroughVerificationResult;
        Action<string> voiceDiagnostics = line => Dispatcher.BeginInvoke(() => Log(line));
        _voice = new VoiceSession(() => new WindowsAudioCapture(voiceDiagnostics), new ElevenLabsSpeechToText(voiceDiagnostics),
            new ElevenLabsTextToSpeech(), new WindowsAudioPlayback(voiceDiagnostics), ProcessVoiceQueryAsync,
            voiceDiagnostics);
        _voice.StateChanged += OnVoiceStateChanged;
        _voice.ActivityChanged += activity => Dispatcher.BeginInvoke(() =>
        {
            if (_closed || !ReferenceEquals(activity, _voice.Activity)) return;
            _presenter.SetBusy(activity.Processing || _invocation is not null || _goalWork is not null);
            _presenter.SetMicrophoneState(activity.MicrophoneOn);
            _presenter.SetState(activity.MicrophoneOn ? "● Microphone on · Listening" :
                activity.Speaking ? "Speaking · Microphone off" : activity.Processing ? "Thinking · Microphone off" :
                activity.Muted ? "Microphone muted" : "Microphone off");
        });
        _voice.PartialTranscript += partial =>
        {
            long identity = _voice.SessionIdentity;
            Dispatcher.BeginInvoke(() =>
            {
                if (identity == _voice.SessionIdentity && _voice.State == VoiceSessionState.Listening && _voice.Activity.MicrophoneOn)
                {
                    StatusText.Text = $"Listening: {partial}";
                    _presenter.ShowPartial(partial);
                }
            });
        };
        _voice.FinalTranscript += transcript => Dispatcher.BeginInvoke(() =>
        {
            if (!_closed) _visibleFinalTranscript = transcript;
        });
        _presenter.Narrate = (text, token) => _voice.SpeakAsync(text, token);
        if (providerWarning is not null) Log(providerWarning);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _tray = new TrayShell(() => OnHotkeyPressed(this, EventArgs.Empty), muted => { _microphoneMuted = muted; _voice.SetMuted(muted); },
            () => OnInterruptPressed(this, EventArgs.Empty), ShowDiagnostics, ExitApplication);
        _hotkey = new GlobalHotkey(this);
        _hotkey.Pressed += OnHotkeyPressed;
        try
        {
            _hotkey.Register();
            _interruptHotkey = new GlobalHotkey(this, 2, NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, "Ctrl+Alt+Space");
            _interruptHotkey.Pressed += OnInterruptPressed;
            _interruptHotkey.Register();
            StatusText.Text = "Hotkey registered: Ctrl+Space";
            HotkeyHint.Text = "Ctrl+Space: open/close chat. Enter: send. Escape: close. Microphone: ask by voice.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Hotkey registration failed: {ex.Message}";
            _tray.Report(StatusText.Text);
        }
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
#if DEBUG
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POINTLY_DEBUG_WALKTHROUGH")))
        {
            OnLegacyHotkeyPressed(sender, e);
            return;
        }
#endif
        if (_presenter.IsVisible) { DismissSession("UserDismissed"); return; }
        // Remember the external window before the editable chat takes keyboard focus.
        _composerTarget = _foreground.TryGetForegroundInfo();
        ReplaceGuidance("ComposerOpened");
        _presenter.SetState("");
        _presenter.SetMicrophoneState(false);
#if DEBUG
        if (Environment.GetEnvironmentVariable("POINTLY_DEBUG_PREVIEW") == "true")
        {
            _presenter.Preview();
            return;
        }
#endif
        _presenter.Summon();
    }

    private bool IsComposerPreview()
    {
#if DEBUG
        return Environment.GetEnvironmentVariable("POINTLY_DEBUG_PREVIEW") == "true";
#else
        return false;
#endif
    }

    private ForegroundWindowInfo? RestoreComposerTarget()
    {
        ForegroundWindowInfo? info = _foreground.TryGetForegroundInfo() ?? _composerTarget;
        if (info?.Context?.IsFresh != true) return null;
        _targetWatcher.Clear();
        _presenter.HideInput();
        if (!NativeMethods.SetForegroundWindow(info.Hwnd)) return null;
        return _foreground.TryGetForegroundInfo()?.Hwnd == info.Hwnd ? info : null;
    }

    private async void OnTypedQuestion(string question)
    {
        if (IsComposerPreview())
        {
            _presenter.ShowInstruction("Preview only. Your question was received; no AI request was sent.");
            return;
        }
        if (_invocation is not null || _goalWork is not null)
        {
            _presenter.SetState("Working… Ctrl+Space cancels");
            return;
        }
        _voice.Cancel("TypedQuestion");
        ForegroundWindowInfo? info = RestoreComposerTarget();
        if (info is null)
        {
            _presenter.ShowInstruction("Open the app you want help with, then close and reopen Pindo.");
            return;
        }
        try
        {
            _presenter.SetState("Thinking… Ctrl+Space cancels");
            _ = await ProcessGoalAsync(info, question, CancellationToken.None);
            if (!_closed && _presenter.IsVisible) _presenter.SetState("");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log($"TypedQuestionFailed Reason={ex.GetType().Name}");
            if (!_closed && _presenter.IsVisible) _presenter.SetState("Couldn't complete the request. Check diagnostics.");
        }
    }

    private void OnMicrophoneRequested()
    {
        if (IsComposerPreview())
        {
            _presenter.SetState("Preview mode · Microphone off");
            return;
        }
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ELEVENLABS_API_KEY")) &&
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ELEVENLABS_API_KEY", EnvironmentVariableTarget.User)))
        {
            _presenter.SetMicrophoneState(false);
            _presenter.SetState("Voice needs an ElevenLabs key. You can still type a question.");
            return;
        }
        ForegroundWindowInfo? info = RestoreComposerTarget();
        if (info is null)
        {
            _presenter.ShowInstruction("Open the app you want help with, then close and reopen Pindo.");
            return;
        }
        ReplaceGuidance("VoiceRequested");
        _presenter.ShowInstruction("");
        if (_voice.IsActive)
        {
            _microphoneMuted = _voice.Activity.MicrophoneOn;
            _voice.SetMuted(_microphoneMuted);
            return;
        }
        _microphoneMuted = false;
        _voice.Summon();
    }

    private async void OnLegacyHotkeyPressed(object? sender, EventArgs e)
    {
        var total = Stopwatch.StartNew();
        bool voiceMode = !string.Equals(Environment.GetEnvironmentVariable("POINTLY_VOICE_MODE"),
            "false", StringComparison.OrdinalIgnoreCase);
#if DEBUG
        bool debugWalkthrough = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POINTLY_DEBUG_WALKTHROUGH"));
        if (Environment.GetEnvironmentVariable("POINTLY_DEBUG_PREVIEW") == "true")
        {
            if (_presenter.IsVisible) DismissSession("PreviewDismissed"); else _presenter.Preview();
            return;
        }
#else
        const bool debugWalkthrough = false;
#endif
        if (voiceMode && !debugWalkthrough)
        {
            if (_presenter.IsVisible) { DismissSession("UserDismissed"); return; }
            ReplaceGuidance("NewSession");
            _presenter.Summon();
            _voice.Summon(_microphoneMuted);
            return;
        }
        if (_invocation is not null)
        {
            _invocation.Cancel();
            CancelWalkthrough("RequestCancelled");
            _verification.Cancel("RequestCancelled");
            Log("Cancellation requested; press Ctrl+Space again once the current request ends.");
            return;
        }

        CancelWalkthrough("NewRequest");
        ForegroundWindowInfo? foreground = _foreground.TryGetForegroundInfo();
#if DEBUG
        string? walkthroughId = Environment.GetEnvironmentVariable("POINTLY_DEBUG_WALKTHROUGH");
        if (foreground?.ProcessName.Equals("EXCEL", StringComparison.OrdinalIgnoreCase) == true &&
            (string.Equals(walkthroughId, DemoWalkthroughs.ExcelPivotTable.Id, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(walkthroughId, DemoWalkthroughs.ExcelSearch.Id, StringComparison.OrdinalIgnoreCase)))
        {
            StartWalkthrough(foreground, string.Equals(walkthroughId, DemoWalkthroughs.ExcelSearch.Id,
                StringComparison.OrdinalIgnoreCase) ? DemoWalkthroughs.ExcelSearch : DemoWalkthroughs.ExcelPivotTable);
            return;
        }
#endif

        using var invocation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        _invocation = invocation;
        _presenter.SetBusy(true);
        Task<WindowCaptureResult?>? captureTask = null;
        try
        {
            _verification.Cancel("NewRequest");
            _presenter.ClearTarget();
            ForegroundWindowInfo? info = foreground;
            Log(info is null ? "ForegroundUnavailable" : $"ForegroundCaptured ProcessId={info.ProcessId}");
            if (info is null) return;

            // Reuse D3's one-shot capture. Its pixels reach a cloud provider
            // only if UIA cannot supply a trustworthy highlight target.
            captureTask = CaptureForegroundAsync(info, invocation.Token);
            await GuideForegroundAsync(info, captureTask, total, invocation.Token);
        }
        catch (Exception ex)
        {
            if (!_closed) Log($"Hotkey failed ({ex.GetType().Name}).");
        }
        finally
        {
            if (captureTask is not null)
            {
                try
                {
                    WindowCaptureResult? capture = await captureTask;
                    if (capture is not null) Array.Clear(capture.Bgra32);
                }
                catch (Exception) { }
            }
            if (ReferenceEquals(_invocation, invocation)) { _invocation = null; _presenter.SetBusy(false); }
        }
    }

    private async Task<string?> ProcessVoiceQueryAsync(string transcript, CancellationToken cancellationToken)
    {
        return await await Dispatcher.InvokeAsync(async () =>
        {
            ForegroundWindowInfo? info = _foreground.TryGetForegroundInfo();
            if (info?.ProcessId == Environment.ProcessId) info = RestoreComposerTarget();
            if (info is null) throw new VoiceException("Processing", "NoForegroundWindow");
            return await ProcessGoalAsync(info, transcript, cancellationToken);
        });
    }

    private async Task<string?> RunVoiceGuidanceAsync(ForegroundWindowInfo info, string query,
        CancellationToken cancellationToken)
    {
        ReplaceGuidance("NewVoiceRequest");
        using var invocation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        invocation.CancelAfter(TimeSpan.FromSeconds(60));
        _invocation = invocation;
        _presenter.SetBusy(true);
        Task<WindowCaptureResult?>? captureTask = null;
        string? instruction = null;
        try
        {
            EnsureForegroundUnchanged(info);
            _verification.Cancel("NewVoiceRequest");
            _presenter.ClearTarget();
            captureTask = CaptureForegroundAsync(info, invocation.Token);
            bool resolved = await GuideForegroundAsync(info, captureTask, Stopwatch.StartNew(),
                invocation.Token, fixedQuery: query, onInstruction: value => instruction = value);
            if (!resolved) throw new VoiceException("Processing", "GuidanceFailed");
            _lastVoiceInstruction = instruction;
            if (_voice.IsActive && instruction is not null)
            {
                SpeakWalkthroughInstruction(instruction, CancellationToken.None);
                return null;
            }
            return instruction;
        }
        finally
        {
            if (captureTask is not null)
            {
                try
                {
                    WindowCaptureResult? capture = await captureTask;
                    if (capture is not null) Array.Clear(capture.Bgra32);
                }
                catch (Exception) { }
            }
            if (ReferenceEquals(_invocation, invocation)) { _invocation = null; _presenter.SetBusy(false); }
        }
    }

    private void OnVoiceStateChanged(VoiceSessionEvent change) => Dispatcher.BeginInvoke(() =>
    {
        if (_closed || (change.State != _voice.State && !(change.State == VoiceSessionState.Failed && !_voice.IsActive))) return;
        if (change.State == VoiceSessionState.Failed && _presenter.IsVisible)
            _presenter.ShowInstruction("Voice unavailable. Check diagnostics and try again.");
        StatusText.Text = change.State switch
        {
            VoiceSessionState.Listening => "Listening... Pause to send. Ctrl+Space dismisses.",
            VoiceSessionState.Finalizing => "Finalizing speech...",
            VoiceSessionState.Processing => $"Processing: {_visibleFinalTranscript}",
            VoiceSessionState.Speaking => "Speaking...",
            VoiceSessionState.Failed => $"Voice failed: {change.Detail}. Try again.",
            VoiceSessionState.Cancelled => "Voice cancelled.",
            VoiceSessionState.Idle when change.Detail == "EmptyTranscript" =>
                "No speech recognized. Check your microphone and try again.",
            VoiceSessionState.Idle when StatusText.Text == "Speaking..." =>
                _walkthrough.Current?.Status == WalkthroughStatus.Running
                    ? $"Step {_walkthrough.Current.StepIndex + 1} of {_walkthrough.Current.Definition.Steps.Count}: " +
                      _walkthrough.Current.CurrentStep!.Instruction
                    : _lastVoiceInstruction ?? "Ready.",
            _ => StatusText.Text
        };
        if (change.State is VoiceSessionState.Speaking or VoiceSessionState.Cancelled or VoiceSessionState.Failed)
            _visibleFinalTranscript = null;
    });

    private void StartWalkthrough(ForegroundWindowInfo info, WalkthroughDefinition definition)
    {
        _verification.Cancel("NewWalkthrough");
        _presenter.ClearTarget();
        WalkthroughSnapshot session = _walkthrough.Start(definition);
        _walkthroughCancellation = new CancellationTokenSource();
        _walkthroughContext = info.Context ?? TargetWindowContext.Capture(info.Hwnd);
        Log($"WalkthroughStarted WalkthroughId={session.Definition.Id} StepCount={session.Definition.Steps.Count}");
        _ = RunWalkthroughStepAsync(session.SessionId, session.StepIndex);
    }

    private async Task RunWalkthroughStepAsync(long sessionId, int stepIndex)
    {
        if (!_walkthrough.IsCurrent(sessionId, stepIndex) || _walkthroughCancellation is null) return;
        using var invocation = CancellationTokenSource.CreateLinkedTokenSource(_walkthroughCancellation.Token);
        invocation.CancelAfter(TimeSpan.FromSeconds(60));
        _invocation = invocation;
        _presenter.SetBusy(true);
        Task<WindowCaptureResult?>? captureTask = null;
        var total = Stopwatch.StartNew();
        WalkthroughSnapshot snapshot = _walkthrough.Current!;
        WalkthroughStep step = snapshot.CurrentStep!;
        var attempt = (sessionId, stepIndex);
        try
        {
            Log($"WalkthroughStepStarted StepId={step.Id} StepIndex={stepIndex + 1}");
            _lastVoiceInstruction = step.Instruction;
            // The hook observes the click before Excel has finished handling it.
            if (stepIndex > 0) await Task.Delay(350, invocation.Token);
            ForegroundWindowInfo? info = _foreground.TryGetForegroundInfo();
            if (info is null || !(_walkthroughContext?.Owns(info.Context) ?? false))
                throw new InvalidOperationException("Walkthrough foreground window changed.");
            if (step.ActionType == WalkthroughActionType.KeyPress)
            {
                EnsureAttemptCurrent(attempt, invocation.Token);
                WatchTarget(info, false);
                _activeWalkthroughTarget = attempt;
                _stepVerification.StartKey(step.ExpectedVirtualKey!.Value, info.Hwnd,
                    step.PostActionState, CurrentWalkthroughForeground, _walkthroughCancellation!.Token);
                StatusText.Text = $"Step {stepIndex + 1} of {snapshot.Definition.Steps.Count}: {step.Instruction}";
                _presenter.ShowInstruction(step.Instruction);
                SpeakWalkthroughInstruction(step.Instruction, _walkthroughCancellation.Token);
                return;
            }
            captureTask = CaptureForegroundAsync(info, invocation.Token);
            bool resolved = await GuideForegroundAsync(info, captureTask, total, invocation.Token,
                step.GroundingQuery, $"Step {stepIndex + 1} of {snapshot.Definition.Steps.Count}: {step.Instruction}", attempt);
            if (!resolved && _walkthrough.IsCurrent(sessionId, stepIndex))
                FailWalkthrough(sessionId, stepIndex, "TargetResolutionFailed");
            else if (resolved && _walkthrough.IsCurrent(sessionId, stepIndex))
                SpeakWalkthroughInstruction(step.Instruction, _walkthroughCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            if (_walkthrough.IsCurrent(sessionId, stepIndex))
                FailWalkthrough(sessionId, stepIndex, "ResolutionTimedOut");
        }
        catch (Exception ex)
        {
            if (_walkthrough.IsCurrent(sessionId, stepIndex))
                FailWalkthrough(sessionId, stepIndex, ex.GetType().Name);
        }
        finally
        {
            if (captureTask is not null)
            {
                try
                {
                    WindowCaptureResult? capture = await captureTask;
                    if (capture is not null) Array.Clear(capture.Bgra32);
                }
                catch (Exception) { }
            }
            if (ReferenceEquals(_invocation, invocation)) { _invocation = null; _presenter.SetBusy(false); }
        }
    }

    private void SpeakWalkthroughInstruction(string instruction, CancellationToken cancellationToken)
    {
        if (!_voice.IsActive) return;
        _ = SpeakWalkthroughInstructionAsync(instruction, cancellationToken);
    }

    private async Task SpeakWalkthroughInstructionAsync(string instruction, CancellationToken cancellationToken)
    {
        try { await _presenter.SpeakOnceAsync(instruction, cancellationToken); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Log($"VoiceSessionFailed Stage=TTS Reason={(ex is VoiceException voice ? voice.Reason : ex.GetType().Name)}"); }
    }

    private void CancelWalkthrough(string reason)
    {
        WalkthroughSnapshot? current = _walkthrough.Current;
        if (current?.Status != WalkthroughStatus.Running) return;
        _targetWatcher.Clear();
        _walkthrough.Cancel(current.SessionId);
        _activeWalkthroughTarget = null;
        _stepVerification.Cancel(reason);
        _walkthroughCancellation?.Cancel();
        _walkthroughCancellation?.Dispose();
        _walkthroughCancellation = null;
        _verification.Cancel(reason);
        _presenter.ClearTarget();
        Log($"WalkthroughCancelled WalkthroughId={current.Definition.Id} Reason={reason}");
    }

    private void FailWalkthrough(long sessionId, int stepIndex, string reason)
    {
        WalkthroughSnapshot? failed = _walkthrough.Fail(sessionId, stepIndex);
        if (failed is null) return;
        _targetWatcher.Clear();
        _activeWalkthroughTarget = null;
        _stepVerification.Cancel("WalkthroughFailed");
        _verification.Cancel("WalkthroughFailed");
        _presenter.ClearTarget();
        _walkthroughCancellation?.Cancel();
        _walkthroughCancellation?.Dispose();
        _walkthroughCancellation = null;
        StatusText.Text = $"Walkthrough stopped at step {stepIndex + 1}. See log and retry.";
        if (_goal.Active) PauseGoal("I could not locate this step. Select Check again to inspect the current screen.");
        Log($"WalkthroughFailed StepId={failed.Definition.Steps[stepIndex].Id} Reason={reason}");
    }

    private nint CurrentWalkthroughForeground()
    {
        ForegroundWindowInfo? current = _foreground.TryGetForegroundInfo();
        return _walkthroughContext?.Owns(current?.Context) == true ? current!.Hwnd : 0;
    }

    private async Task<WindowCaptureResult?> CaptureForegroundAsync(
        ForegroundWindowInfo info,
        CancellationToken cancellationToken)
    {
        try
        {
            using var hidden = _presenter.HideForCapture();
            WindowCaptureResult result = await _capture.CaptureOnceAsync(info.Hwnd, cancellationToken);
            if (cancellationToken.IsCancellationRequested || info.Context?.IsFresh == false)
            {
                Array.Clear(result.Bgra32);
                throw new OperationCanceledException("CaptureContextChanged", cancellationToken);
            }
            bool sizeMatches = result.WindowRect.Width == result.PixelWidth &&
                result.WindowRect.Height == result.PixelHeight;
            Log($"Screenshot acquisition={result.Timings.TotalMs}ms {result.PixelWidth}x{result.PixelHeight} " +
                $"(window {result.WindowRect.Width}x{result.WindowRect.Height}, " +
                $"{(sizeMatches ? "match" : "MISMATCH")}) dpi={result.DpiScale:F2} " +
                $"init={result.Timings.InitMs}ms frame={result.Timings.FrameWaitMs}ms copy={result.Timings.CopyMs}ms.");

            if (CaptureDebugPng.IsEnabled)
            {
                string path = CaptureDebugPng.Save(result);
                Log($"Debug PNG saved: {path}");
            }
            return result;
        }
        catch (Exception ex)
        {
            Log($"CaptureFailed Reason={ex.GetType().Name}");
            return null;
        }
    }

    private async Task<bool> GuideForegroundAsync(
        ForegroundWindowInfo info,
        Task<WindowCaptureResult?> captureTask,
        Stopwatch total,
        CancellationToken cancellationToken,
        string? fixedQuery = null,
        string? fixedInstruction = null,
        (long SessionId, int StepIndex)? walkthroughAttempt = null,
        Action<string>? onInstruction = null)
    {
        var stage = Stopwatch.StartNew();
        string phase = "UIA collection";
        bool presented = false;
        try
        {
            EnsureForegroundUnchanged(info);
            WatchTarget(info, walkthroughAttempt is null || _walkthrough.Current?.CurrentStep?.ActionType == WalkthroughActionType.Click);
            StatusText.Text = "Collecting visible controls...";
            IReadOnlyList<UiAutomationService.TutorCandidate> candidates;
            bool preferInput = walkthroughAttempt is not null &&
                _walkthrough.Current?.CurrentStep?.TargetKind == WalkthroughTargetKind.Input;
            try
            {
                candidates = await UiaWorkScheduler.Shared.RunAsync(
                    () => _automation.CollectTutorCandidates(info.Hwnd, cancellationToken, preferInput),
                    cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                candidates = [];
                Log($"UIA collection unavailable ({ex.GetType().Name}); routing to vision.");
            }
            Log($"UIA collection={stage.ElapsedMilliseconds}ms candidates={candidates.Count}.");
            cancellationToken.ThrowIfCancellationRequested();

            string userQuery = fixedQuery ?? VisionDebugOptions.GroundingQuery ??
                (info.ProcessName.Equals("EXCEL", StringComparison.OrdinalIgnoreCase)
                ? "How do I create a PivotTable?"
                : "Where do I add text?");

            bool requiresUiaText = walkthroughAttempt is not null &&
                _walkthrough.Current?.CurrentStep?.ActionType == WalkthroughActionType.TextEntry;
            bool forceVision = VisionDebugOptions.ForceVision && !requiresUiaText;
            if (VisionDebugOptions.ForceVision && requiresUiaText)
                Log("DEBUG force vision ignored for TextEntry; UIA value verification is required.");

            if (!forceVision && candidates.Count > 0)
            {
                TutorResponse? response = null;
                try
                {
                    response = await RequestTutorStepAsync(
                        info, userQuery, candidates, stage, cancellationToken, walkthroughAttempt is not null);
                }
                catch (HttpRequestException ex)
                {
                    // A tutor transport failure must not prevent the existing visual provider from helping.
                    cancellationToken.ThrowIfCancellationRequested();
                    Log($"UIA tutor unavailable HTTP={(int?)ex.StatusCode}; routing to vision.");
                }
                if (response is not null && response.Confidence >= MinimumUiaConfidence)
                {
                    UiAutomationService.TutorCandidate selected = candidates.Single(
                        candidate => candidate.Id == response.TargetElementId);
                    bool ambiguous = IsAmbiguous(selected, candidates);
                    phase = "target resolution";
                    stage.Restart();
                    UiElementInfo? target;
                    try
                    {
                        target = await UiaWorkScheduler.Shared.RunAsync(
                            () => _automation.ResolveTutorTarget(info.Hwnd, selected, cancellationToken), cancellationToken);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        target = null;
                        Log($"UIA target resolution unavailable ({ex.GetType().Name}).");
                    }
                    Log($"Target resolution={stage.ElapsedMilliseconds}ms ambiguous={ambiguous}.");
                    if (target is not null && !ambiguous)
                    {
                        EnsureAttemptCurrent(walkthroughAttempt, cancellationToken);
                        EnsureForegroundUnchanged(info);
                        WalkthroughStep? walkthroughStep = walkthroughAttempt is not null
                            ? _walkthrough.Current?.CurrentStep : null;
                        if (walkthroughStep?.ActionType == WalkthroughActionType.TextEntry)
                        {
                            string? currentValue = await _uiStateVerifier.ReadTargetValueAsync(
                                info.Hwnd, selected.RuntimeId, cancellationToken);
                            if (currentValue is null)
                                throw new InvalidOperationException("The intended input does not expose a UIA value.");
                            EnsureAttemptCurrent(walkthroughAttempt, cancellationToken);
                            EnsureForegroundUnchanged(info);
                        }
                        _presenter.ShowTarget(target.BoundingRectangle, false, fixedInstruction ?? response.Instruction);
                        StatusText.Text = fixedInstruction ?? response.Instruction;
                        onInstruction?.Invoke(response.Instruction);
                        if (walkthroughStep?.ActionType == WalkthroughActionType.TextEntry)
                        {
                            _activeWalkthroughTarget = walkthroughAttempt;
                            _stepVerification.StartText(info.Hwnd, walkthroughStep.ExpectedText,
                                token => _uiStateVerifier.ReadTargetValueAsync(info.Hwnd, selected.RuntimeId, token,
                                    requireFocus: true),
                                walkthroughStep.PostActionState, CurrentWalkthroughForeground,
                                _walkthroughCancellation!.Token);
                        }
                        else StartClickVerification(info, target.BoundingRectangle, false,
                            cancellationToken, walkthroughAttempt);
                        Log($"Route=UIA cloud-grounding=not-invoked Confidence={response.Confidence:F2}");
                        Log($"Total hotkey-to-highlight={total.ElapsedMilliseconds}ms.");
                        if (walkthroughAttempt is { } attempt)
                            Log($"WalkthroughStepResolved StepId={_walkthrough.Current!.Definition.Steps[attempt.StepIndex].Id} Route=UIA");
                        presented = true;
                        return true;
                    }
                    Log(target is null
                        ? "UIA target could not be resolved; routing to vision."
                        : "UIA target is ambiguous; routing to vision.");
                }
                else if (response is not null)
                {
                    Log($"UIA tutor confidence={response.Confidence:F2} is below {MinimumUiaConfidence:F2}; routing to vision.");
                }
            }
            else if (forceVision)
            {
                Log("Route=vision forced by DEBUG-only POINTLY_DEBUG_FORCE_VISION.");
            }
            else
            {
                Log("No actionable UIA candidates; routing to vision.");
            }

            if (requiresUiaText)
                throw new InvalidOperationException("Text entry requires a readable UIA input target.");
            phase = "vision grounding";
            await GroundWithVisionAsync(info, userQuery, candidates, captureTask, total, cancellationToken,
                fixedInstruction, walkthroughAttempt, onInstruction);
            presented = true;
            return true;
        }
        catch (OperationCanceledException)
        {
            if (MayShowResolutionFailure(walkthroughAttempt, cancellationToken))
                StatusText.Text = "Guidance cancelled or timed out. Press Ctrl+Space to retry.";
            Log($"Guidance cancelled/timed out during {phase}; stage={stage.ElapsedMilliseconds}ms total={total.ElapsedMilliseconds}ms.");
            return false;
        }
        catch (AssemblyAiGatewayException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            string retryMessage = ex.RetryAfter is { } retryAfter
                ? $" Try again in {Math.Max(1, Math.Ceiling(retryAfter.TotalSeconds))} seconds."
                : " Try again shortly.";
            if (MayShowResolutionFailure(walkthroughAttempt, cancellationToken))
                StatusText.Text = "Tutor is temporarily rate limited." + retryMessage;
            Log($"AssemblyAI HTTP 429 during {phase}: ResponseBody=Withheld " +
                $"stage={stage.ElapsedMilliseconds}ms total={total.ElapsedMilliseconds}ms.");
            return false;
        }
        catch (AssemblyAiGatewayException ex)
        {
            if (MayShowResolutionFailure(walkthroughAttempt, cancellationToken))
                StatusText.Text = "Tutor failed. See log and retry.";
            int statusCode = ex.StatusCode is { } code ? (int)code : 0;
            Log($"AssemblyAI HTTP {statusCode} during {phase}: ResponseBody=Withheld " +
                $"stage={stage.ElapsedMilliseconds}ms total={total.ElapsedMilliseconds}ms.");
            return false;
        }
        catch (AmazonServiceException ex)
        {
            if (MayShowResolutionFailure(walkthroughAttempt, cancellationToken))
                StatusText.Text = "Vision grounding failed. Check AWS access and retry.";
            Log($"Bedrock failed during {phase}: status={(int)ex.StatusCode} code={ex.ErrorCode ?? "unknown"} " +
                $"requestId={ex.RequestId ?? "unavailable"} " +
                $"total={total.ElapsedMilliseconds}ms.");
            return false;
        }
        catch (Exception ex)
        {
            string reason = ex is GuiGroundingException grounding ? grounding.Reason.ToString() : ex.GetType().Name;
            if (MayShowResolutionFailure(walkthroughAttempt, cancellationToken))
                StatusText.Text = phase == "vision grounding"
                    ? "Vision grounding failed. See log and retry."
                    : "Tutor failed. See log and retry.";
            Log($"Guidance failed during {phase}: {reason} stage={stage.ElapsedMilliseconds}ms total={total.ElapsedMilliseconds}ms.");
            return false;
        }
        finally
        {
            if (!presented && MayShowResolutionFailure(walkthroughAttempt, cancellationToken))
            {
                _targetWatcher.Clear();
                _presenter.ShowInstruction("I couldn't locate a reliable target. Check diagnostics and try again.");
            }
        }
    }

    private void EnsureAttemptCurrent((long SessionId, int StepIndex)? attempt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (attempt is { } value && !_walkthrough.IsCurrent(value.SessionId, value.StepIndex))
            throw new OperationCanceledException("The walkthrough step was replaced.");
    }

    private bool MayShowResolutionFailure((long SessionId, int StepIndex)? attempt,
        CancellationToken cancellationToken) => !_closed && !cancellationToken.IsCancellationRequested &&
        (attempt is null || _walkthrough.IsCurrent(attempt.Value.SessionId, attempt.Value.StepIndex));

    private async Task<TutorResponse> RequestTutorStepAsync(
        ForegroundWindowInfo info,
        string userQuery,
        IReadOnlyList<UiAutomationService.TutorCandidate> candidates,
        Stopwatch stage,
        CancellationToken cancellationToken,
        bool isWalkthroughStep)
    {
        var request = new TutorRequest(
            userQuery,
            info.ProcessName,
            info.WindowTitle,
            candidates.Select(candidate => new TutorElement(
                candidate.Id,
                candidate.Info.Name,
                candidate.Info.AutomationId,
                candidate.Info.ControlType)).ToArray(),
            isWalkthroughStep
                ? "Predefined walkthrough step. Locate only the target named in the user query; do not select an earlier or later control."
                : "Initial step. Visible actionable UIA controls only; no earlier tutoring step is available.");
        stage.Restart();
        StatusText.Text = "Asking tutor... (Ctrl+Space cancels)";
        TutorResponse response;
        try { response = await _tutor.GetNextStepAsync(request, cancellationToken); }
        catch (HttpRequestException) { response = await _planner.SelectAsync(request, cancellationToken); }
        Log($"Model request={stage.ElapsedMilliseconds}ms.");
        return response;
    }

    private async Task GroundWithVisionAsync(
        ForegroundWindowInfo info,
        string userQuery,
        IReadOnlyList<UiAutomationService.TutorCandidate> candidates,
        Task<WindowCaptureResult?> captureTask,
        Stopwatch hotkeyTotal,
        CancellationToken cancellationToken,
        string? fixedInstruction,
        (long SessionId, int StepIndex)? walkthroughAttempt,
        Action<string>? onInstruction)
    {
        var visionTotal = Stopwatch.StartNew();
        long captureMs = 0;
        long preparationMs = 0;
        long providerMs = 0;
        long conversionMs = 0;
        string targetType = "none";
        string failureReason = GroundingFailureReason.Unknown.ToString();
        bool success = false;
        byte[]? png = null;
        try
        {
            WindowCaptureResult? capture = await captureTask;
            if (capture is null)
                throw new GuiGroundingException("The active window screenshot was unavailable.",
                    GroundingFailureReason.CaptureUnavailable);
            captureMs = capture.Timings.TotalMs;
            cancellationToken.ThrowIfCancellationRequested();

            var stage = Stopwatch.StartNew();
            png = await Task.Run(() => CapturePngEncoder.Encode(capture), cancellationToken);
            preparationMs = stage.ElapsedMilliseconds;
            var request = new GuiGroundingRequest(
                userQuery,
                info.ProcessName,
                info.WindowTitle,
                png,
                capture.PixelWidth,
                capture.PixelHeight,
                candidates.Select(candidate => new VisionUiCandidate(
                    candidate.Info.Name,
                    candidate.Info.AutomationId,
                    candidate.Info.ControlType)).ToArray());

            stage.Restart();
            StatusText.Text = "Locating the control visually... (Ctrl+Space cancels)";
            GuiGroundingResult response;
            try
            {
                response = await _vision.GroundAsync(request, cancellationToken);
            }
            finally
            {
                providerMs = stage.ElapsedMilliseconds;
            }

            stage.Restart();
            VisionCoordinateResult coordinates = response.BoundingBox is { } box
                ? VisionCoordinateConverter.ToPhysicalScreen(box, capture)
                : VisionCoordinateConverter.ToPhysicalScreen(response.Point!, capture);
            targetType = response.BoundingBox is null ? "point" : "box";
            conversionMs = stage.ElapsedMilliseconds;
            Log($"Grounding geometry normalized={FormatGroundingGeometry(response)} " +
                $"screenshotPixels={coordinates.ScreenshotPixels} " +
                $"screenBounds={coordinates.PhysicalScreenBounds}.");
            EnsureAttemptCurrent(walkthroughAttempt, cancellationToken);
            EnsureForegroundUnchanged(info);
            _presenter.ShowTarget(coordinates.PhysicalScreenBounds, response.BoundingBox is null,
                fixedInstruction ?? response.Instruction ?? $"Look for '{response.TargetLabel}'.");
            StatusText.Text = fixedInstruction ?? response.Instruction ?? $"Look for '{response.TargetLabel}'.";
            onInstruction?.Invoke(response.Instruction ?? $"Look for '{response.TargetLabel}'.");
            StartClickVerification(info, coordinates.PhysicalScreenBounds,
                response.BoundingBox is null, cancellationToken, walkthroughAttempt);
            if (walkthroughAttempt is { } attempt)
                Log($"WalkthroughStepResolved StepId={_walkthrough.Current!.Definition.Steps[attempt.StepIndex].Id} Route=VISION Provider={_visionModel.ProviderName}");
            success = true;
            failureReason = "None";
        }
        catch (GuiGroundingException ex)
        {
            failureReason = ex.Reason.ToString();
            Log($"GroundingFailure={failureReason}.");
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            failureReason = GroundingFailureReason.Timeout.ToString();
            throw;
        }
        catch (OperationCanceledException)
        {
            failureReason = GroundingFailureReason.Cancelled.ToString();
            throw;
        }
        catch (AmazonServiceException)
        {
            failureReason = GroundingFailureReason.ProviderHttp.ToString();
            throw;
        }
        catch (System.Net.Http.HttpRequestException)
        {
            failureReason = GroundingFailureReason.Network.ToString();
            throw;
        }
        catch (InvalidOperationException)
        {
            failureReason = GroundingFailureReason.ForegroundChanged.ToString();
            throw;
        }
        finally
        {
            if (png is not null) Array.Clear(png);
            Log($"Grounding Provider={_visionModel.ProviderName} Model={_visionModel.ModelId} " +
                $"ScreenshotCaptureMs={captureMs} ImagePreparationMs={preparationMs} " +
                $"ProviderRequestMs={providerMs} CoordinateConversionMs={conversionMs} " +
                $"TotalMs={visionTotal.ElapsedMilliseconds} HotkeyTotalMs={hotkeyTotal.ElapsedMilliseconds} " +
                $"TargetType={targetType} Success={success.ToString().ToLowerInvariant()} " +
                $"FailureReason={failureReason} Route=VISION");
        }
    }

    private static bool IsAmbiguous(
        UiAutomationService.TutorCandidate selected,
        IReadOnlyList<UiAutomationService.TutorCandidate> candidates) =>
        candidates.Count(candidate =>
            candidate.Info.Name.Equals(selected.Info.Name, StringComparison.OrdinalIgnoreCase) &&
            candidate.Info.ControlType == selected.Info.ControlType &&
            candidate.Info.AutomationId == selected.Info.AutomationId) > 1;

    private void EnsureForegroundUnchanged(ForegroundWindowInfo original)
    {
        ForegroundWindowInfo? current = _foreground.TryGetForegroundInfo();
        if (current?.Hwnd != original.Hwnd || current.ProcessId != original.ProcessId ||
            original.Context?.Matches(current.Context) == false)
        {
            throw new InvalidOperationException("Foreground context changed; invoke Pointly again in the target application.");
        }
    }

    private void WatchTarget(ForegroundWindowInfo info, bool trackLayout = true)
    {
        TargetWindowContext context = info.Context ?? TargetWindowContext.Capture(info.Hwnd)
            ?? throw new InvalidOperationException("TargetContextUnavailable");
        _targetWatcher.Watch(context, trackLayout);
    }

    private void StartClickVerification(ForegroundWindowInfo info, Rect physicalBounds,
        bool isPoint, CancellationToken cancellationToken,
        (long SessionId, int StepIndex)? walkthroughAttempt = null)
    {
        VerifiedClickTarget target = isPoint
            ? VerifiedClickTarget.ForPoint(new ScreenPoint(
                physicalBounds.X + physicalBounds.Width / 2,
                physicalBounds.Y + physicalBounds.Height / 2))
            : VerifiedClickTarget.ForBox(new ScreenRectangle(
                physicalBounds.Left, physicalBounds.Top,
                physicalBounds.Right, physicalBounds.Bottom));
        try
        {
            if (walkthroughAttempt is { } attempt)
            {
                _activeWalkthroughTarget = attempt;
                WalkthroughStep step = _walkthrough.Current!.CurrentStep!;
                _stepVerification.StartClick(target, info.Hwnd, step.PostActionState,
                    CurrentWalkthroughForeground, _walkthroughCancellation!.Token);
            }
            else
            {
                _verification.Cancel("ReplacedByNewTarget");
                _verification.Start(target, info.Hwnd, cancellationToken, () => _targetWatcher.IsValid);
            }
        }
        catch
        {
            _activeWalkthroughTarget = null;
            _presenter.ClearTarget();
            throw;
        }
    }

    private void OnVerificationResult(StepVerificationResult result)
    {
        switch (result.Status)
        {
            case StepVerificationStatus.Waiting:
                Log($"VerificationStarted TargetType={result.TargetType}");
                break;
            case StepVerificationStatus.MissedTarget:
                if (!_closed) StatusText.Text = "That missed the highlighted control. Try again.";
                Log("VerificationClick Result=Miss");
                break;
            case StepVerificationStatus.Completed:
                _targetWatcher.Clear();
                _presenter.ClearTarget();
                _presenter.ShowInstruction("Step completed. You can ask another question.");
                if (!_closed) StatusText.Text = "Step completed.";
                Log("VerificationClick Result=Hit");
                Log($"VerificationCompleted VerificationDurationMs={result.VerificationDurationMs}");
                break;
            case StepVerificationStatus.Cancelled:
                if (result.CancellationReason != "ReplacedByNewTarget") _presenter.ClearTarget();
                Log($"VerificationCancelled Reason={result.CancellationReason}");
                break;
        }
    }

    private void OnWalkthroughVerificationResult(WalkthroughVerificationResult result)
    {
        if (_activeWalkthroughTarget is not { } attempt ||
            !_walkthrough.IsCurrent(attempt.SessionId, attempt.StepIndex)) return;
        WalkthroughSnapshot current = _walkthrough.Current!;
        WalkthroughStep step = current.CurrentStep!;
        if (_goal.Active && step.ExpectedResult is not null && !_acceptingVerifiedGoal &&
            result.Status is WalkthroughVerificationStatus.Completed or WalkthroughVerificationStatus.Failed)
        {
            _ = VerifyGoalStepAsync(true);
            return;
        }
        switch (result.Status)
        {
            case WalkthroughVerificationStatus.Waiting:
                Log($"StepVerificationStarted ActionType={result.ActionType} StepId={step.Id}");
                break;
            case WalkthroughVerificationStatus.Miss:
                _walkthrough.Miss(attempt.SessionId, attempt.StepIndex);
                string missKind = result.ActionType switch
                {
                    WalkthroughActionType.KeyPress => "KeyMiss",
                    WalkthroughActionType.TextEntry => "TextEntryVerification Result=NotVerified",
                    _ => "WalkthroughStepMiss",
                };
                Log($"{missKind} StepId={step.Id}");
                if (step.ActionType == WalkthroughActionType.KeyPress)
                    Log("KeyboardVerification Result=Miss");
                StatusText.Text = $"Step {attempt.StepIndex + 1} of {current.Definition.Steps.Count}: {step.Instruction} Still waiting.";
                break;
            case WalkthroughVerificationStatus.CheckingState:
                _targetWatcher.Clear();
                _presenter.ClearTarget();
                StatusText.Text = "Checking the application state...";
                Log($"UiStateVerification Type={step.PostActionState?.Type} Result=Pending");
                break;
            case WalkthroughVerificationStatus.Completed:
                _targetWatcher.Clear();
                _activeWalkthroughTarget = null;
                _presenter.ClearTarget();
                WalkthroughSnapshot? next = _walkthrough.Hit(attempt.SessionId, attempt.StepIndex);
                if (next is null) return;
                Log($"WalkthroughStepCompleted StepId={step.Id} ActionType={step.ActionType} DurationMs={current.StepDurationMs}");
                if (step.ActionType == WalkthroughActionType.KeyPress)
                {
                    Log("KeyHit");
                    Log("KeyboardVerification Result=Hit");
                }
                if (step.ActionType == WalkthroughActionType.TextEntry) Log("TextEntryVerification Result=Verified");
                if (step.PostActionState is not null)
                    Log($"UiStateVerification Type={step.PostActionState.Type} Result=true DurationMs={result.StateDurationMs}");
                if (next.Status == WalkthroughStatus.Completed)
                {
                    _walkthroughCancellation?.Dispose();
                    _walkthroughCancellation = null;
                    StatusText.Text = "Walkthrough completed.";
                    _presenter.ShowInstruction("Walkthrough completed. You can ask another question.");
                    Log($"WalkthroughCompleted TotalDurationMs={next.TotalDurationMs}");
                }
                else _ = RunWalkthroughStepAsync(next.SessionId, next.StepIndex);
                break;
            case WalkthroughVerificationStatus.Failed:
                if (step.PostActionState is not null)
                    Log($"UiStateVerification Type={step.PostActionState.Type} Result=false DurationMs={result.StateDurationMs}");
                FailWalkthrough(attempt.SessionId, attempt.StepIndex, result.Reason ?? "StateVerificationFailed");
                break;
            case WalkthroughVerificationStatus.Cancelled:
                CancelWalkthrough(result.Reason ?? "VerificationCancelled");
                break;
        }
    }

    private static string FormatGroundingGeometry(GuiGroundingResult response) =>
        response.BoundingBox is { } box
            ? $"box({box.X1:F1},{box.Y1:F1},{box.X2:F1},{box.Y2:F1})"
            : response.Point is { } point
                ? $"point({point.X:F1},{point.Y:F1})"
                : "none";

    private void Log(string line)
    {
        if (_closed) return;
        Debug.WriteLine(line);
        if (LogList.Items.Count >= 300) LogList.Items.RemoveAt(0);
        LogList.Items.Add(line);
        LogList.ScrollIntoView(LogList.Items[^1]);
    }

    private void OnInterruptPressed(object? sender, EventArgs e)
    {
        if (!_presenter.IsVisible) return;
        if (IsComposerPreview()) { _presenter.SetState("Preview mode · Microphone off"); return; }
        if (RestoreComposerTarget() is null)
        {
            _presenter.ShowInstruction("Open the app you want help with, then close and reopen Pindo.");
            return;
        }
        ReplaceGuidance("ExplicitInterrupt");
        _presenter.ShowInstruction("");
        if (_voice.IsActive) _voice.Interrupt(); else _voice.Summon(_microphoneMuted);
    }

    private void ReplaceGuidance(string reason)
    {
        _targetWatcher.Clear();
        _goalWork?.Cancel();
        _invocation?.Cancel();
        _presenter.SetBusy(false);
        CancelWalkthrough(reason);
        _verification.Cancel(reason);
        _stepVerification.Cancel(reason);
        _presenter.ClearTarget();
    }

    internal void DismissSession(string reason)
    {
        _goal.Reset();
        _voice.Cancel(reason);
        ReplaceGuidance(reason);
        _presenter.Dismiss();
        _visibleFinalTranscript = null;
        StatusText.Text = "Pindo dismissed. Microphone off.";
    }

    private void ShowDiagnostics() { Show(); WindowState = WindowState.Normal; Activate(); }
    private void ExitApplication() { _exitRequested = true; Close(); Application.Current.Shutdown(); }
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_exitRequested) { e.Cancel = true; Hide(); }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _voice.Cancel("ApplicationClosing");
        _voice.StateChanged -= OnVoiceStateChanged;
        _voice.Dispose();
        CancelWalkthrough("ApplicationClosing");
        _goal.Reset();
        _goalWork?.Cancel();
        _planner.Dispose();
        _closed = true;
        _invocation?.Cancel();
        _stepVerification.Dispose();
        _stepVerification.ResultChanged -= OnWalkthroughVerificationResult;
        _verification.Dispose();
        _verification.ResultChanged -= OnVerificationResult;
        _tutorModel.Dispose();
        _visionModel.Dispose();
        if (_hotkey is not null)
        {
            _hotkey.Pressed -= OnHotkeyPressed;
            _hotkey.Dispose();
            _hotkey = null;
        }

        _targetWatcher.Dispose();
        _presenter.Dispose();
        _tray?.Dispose();
        _interruptHotkey?.Dispose();
        base.OnClosed(e);
    }
}
