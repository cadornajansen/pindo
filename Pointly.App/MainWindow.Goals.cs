using System.Windows;
using Pointly.App.Automation;
using Pointly.App.Capture;
using Pointly.App.Interop;
using Pointly.App.Tutor;
using Pointly.App.Verification;
using Pointly.App.Walkthrough;
using Pointly.App.Windows;

namespace Pointly.App;

public partial class MainWindow
{
    private readonly OpenRouterPlanner _planner = new();
    private readonly GoalSession _goal = new();
    private TargetWindowContext? _goalContext;
    private CancellationTokenSource? _goalWork;
    private bool _acceptingVerifiedGoal;
    private bool _checkingGoal;
    private LowLevelKeyboardHook? _visualTextHook;
    private System.Windows.Threading.DispatcherTimer? _visualTextDelay;

    private void StopVisualTextObservation()
    {
        _visualTextDelay?.Stop();
        _visualTextDelay = null;
        _visualTextHook?.Dispose();
        _visualTextHook = null;
    }

    private void ObserveVisualText(ForegroundWindowInfo info)
    {
        StopVisualTextObservation();
        _visualTextDelay = new() { Interval = TimeSpan.FromMilliseconds(1200) };
        _visualTextDelay.Tick += async (_, _) =>
        {
            _visualTextDelay?.Stop();
            if (_goalWork is null && _invocation is null) await VerifyGoalStepAsync(false);
        };
        _visualTextHook = new LowLevelKeyboardHook((_, hwnd) =>
        {
            if (hwnd != info.Hwnd) return;
            Dispatcher.BeginInvoke(() => { _visualTextDelay?.Stop(); _visualTextDelay?.Start(); });
        });
        _visualTextHook.Start();
    }

    private async Task<string?> ProcessGoalAsync(ForegroundWindowInfo info, string question, CancellationToken token)
    {
        ReplaceGuidance("NewGoalTurn");
        _goal.Accept(question);
        _goalContext = info.Context;
        _composerTarget = info;
        await PlanGoalAsync(info, token);
        return null;
    }

    private async Task<(byte[] Png, TutorElement[] Elements)> ObserveGoalAsync(ForegroundWindowInfo info, CancellationToken token)
    {
        EnsureForegroundUnchanged(info);
        WindowCaptureResult? capture = await CaptureForegroundAsync(info, token);
        if (capture is null) throw new InvalidOperationException("The target screen could not be captured.");
        try
        {
            IReadOnlyList<UiAutomationService.TutorCandidate> candidates;
            try { candidates = await UiaWorkScheduler.Shared.RunAsync(
                () => _automation.CollectTutorCandidates(info.Hwnd, token, true), token); }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { candidates = []; }
            token.ThrowIfCancellationRequested();
            EnsureForegroundUnchanged(info);
            return (CapturePngEncoder.Encode(capture), candidates.Select(candidate => new TutorElement(
                candidate.Id, candidate.Info.Name, candidate.Info.AutomationId, candidate.Info.ControlType)).ToArray());
        }
        finally { Array.Clear(capture.Bgra32); }
    }

    private async Task PlanGoalAsync(ForegroundWindowInfo info, CancellationToken token)
    {
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(token);
        operation.CancelAfter(TimeSpan.FromSeconds(60));
        _goalWork = operation;
        long identity = _goal.Identity;
        _presenter.SetBusy(true);
        _presenter.SetState("Planning your next steps…");
        byte[]? png = null;
        try
        {
            var observation = await ObserveGoalAsync(info, operation.Token);
            png = observation.Png;
            PlannerOutcome outcome = await _planner.PlanAsync(new
            {
                goal = _goal.Goal, conversation = _goal.Conversation.ToArray(), completed = _goal.Completed.ToArray(),
                application = info.ProcessName, elements = observation.Elements,
                tools = ToolCatalogForPlanner(), selection = ToolSelectionForPlanner()
            }, png, operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            if (!_goal.IsCurrent(identity)) return;
            EnsureForegroundUnchanged(info);
            _presenter.SetBusy(false);
            switch (outcome.Kind)
            {
                case "clarification":
                    _goal.AwaitingClarification = true;
                    _goal.Conversation.Add("Pindo: " + outcome.Message);
                    _presenter.SetCanCheck(false);
                    _presenter.ShowInstruction(outcome.Message);
                    await NarrateGoalAsync(outcome.Message, operation.Token);
                    break;
                case "plan":
                    var steps = outcome.Steps.Select((step, index) => new WalkthroughStep(
                        $"goal-{identity}-{index}", step.Instruction, step.Target, step.Action,
                        step.VirtualKey, step.Text, TargetKind: step.Action == WalkthroughActionType.TextEntry
                            ? WalkthroughTargetKind.Input : WalkthroughTargetKind.Actionable,
                        ExpectedResult: step.ExpectedResult)).ToArray();
                    _presenter.SetCanCheck(true);
                    StartWalkthrough(info, new WalkthroughDefinition($"goal-{identity}", _goal.Goal, steps));
                    break;
                case "tool":
                    await PresentToolProposalAsync(outcome.Tool!, CancellationToken.None);
                    break;
                case "complete":
                    // A planner conclusion alone cannot complete an unverified goal.
                    GoalVerification verified = await _planner.VerifyAsync(new { goal = _goal.Goal, expected = _goal.Goal,
                        elements = observation.Elements, finalStep = true }, png, operation.Token);
                    operation.Token.ThrowIfCancellationRequested();
                    if (!_goal.IsCurrent(identity)) return;
                    _presenter.ShowInstruction(verified.Matched ? outcome.Message : "I can't verify that yet. Describe what you see or select Check again.");
                    _presenter.SetCanCheck(!verified.Matched);
                    if (verified.Matched) _goal.Reset(); else _goal.AwaitingClarification = true;
                    break;
            }
        }
        catch (OperationCanceledException) { if (_goal.IsCurrent(identity)) PauseGoal("Request stopped. Select Check again to retry."); }
        catch (Exception ex)
        {
            Log($"GoalPlanningFailed Reason={ex.GetType().Name}");
            if (ex is InvalidOperationException) Log($"GoalPlanningDetail={ex.Message}");
            if (_goal.IsCurrent(identity)) PauseGoal("Couldn't plan the next step. Select Check again to retry.");
        }
        finally
        {
            if (png is not null) Array.Clear(png);
            if (ReferenceEquals(_goalWork, operation)) { _goalWork = null; _presenter.SetBusy(_invocation is not null); }
        }
    }

    private void PauseGoal(string message)
    {
        _presenter.ClearTarget();
        _presenter.SetBusy(false);
        _presenter.SetCanCheck(true);
        _presenter.ShowInstruction(message);
    }

    private async void OnCheckGoal()
    {
        if (!_goal.Active || _goalWork is not null || _invocation is not null) return;
        _targetWatcher.Clear();
        ForegroundWindowInfo? current = _foreground.TryGetForegroundInfo();
        if (current?.ProcessId == Environment.ProcessId || current is null)
        {
            if (_goalContext is { } context) NativeMethods.SetForegroundWindow(context.Hwnd);
            current = _foreground.TryGetForegroundInfo();
        }
        if (_goalContext?.Owns(current?.Context) != true)
        {
            PauseGoal("Return to the original application, then select Check again.");
            return;
        }
        _goalContext = current!.Context;
        _composerTarget = current;
        if (_walkthrough.Current?.Status == WalkthroughStatus.Running)
            await VerifyGoalStepAsync(false);
        else await PlanGoalAsync(current, CancellationToken.None);
    }

    private async Task VerifyGoalStepAsync(bool actionObserved)
    {
        if (_checkingGoal || _walkthrough.Current is not { CurrentStep: { } step } snapshot || !_goal.Active) return;
        _checkingGoal = true;
        long identity = _goal.Identity;
        StopVisualTextObservation();
        _activeWalkthroughTarget = null;
        _targetWatcher.Clear();
        _stepVerification.Cancel("CheckingGoalState");
        _presenter.ClearTarget();
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(_walkthroughCancellation?.Token ?? CancellationToken.None);
        operation.CancelAfter(TimeSpan.FromSeconds(60));
        _goalWork = operation;
        _presenter.SetBusy(true);
        _presenter.SetState("Checking the result…");
        byte[]? png = null;
        try
        {
            await Task.Delay(500, operation.Token);
            ForegroundWindowInfo? current = _foreground.TryGetForegroundInfo();
            if (_goalContext?.Owns(current?.Context) != true) { PauseGoal("Return to the original app and select Check again."); return; }
            var observation = await ObserveGoalAsync(current!, operation.Token);
            png = observation.Png;
            bool finalStep = snapshot.StepIndex == snapshot.Definition.Steps.Count - 1;
            GoalVerification result = await _planner.VerifyAsync(new
            {
                goal = _goal.Goal, clarification = _goal.Conversation.ToArray(), expected = step.ExpectedResult,
                completed = _goal.Completed.ToArray(), elements = observation.Elements, finalStep
            }, png, operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            if (!_goal.IsCurrent(identity) || !_walkthrough.IsCurrent(snapshot.SessionId, snapshot.StepIndex)) return;
            EnsureForegroundUnchanged(current!);
            _presenter.SetBusy(false);
            if (result.Matched)
            {
                _goal.Completed.Add(step.Instruction + " — " + result.Evidence);
                _goal.Replans = 0;
                _presenter.ShowInstruction("✓ Step verified");
                await Task.Delay(350, operation.Token);
                _activeWalkthroughTarget = (snapshot.SessionId, snapshot.StepIndex);
                _acceptingVerifiedGoal = true;
                try { OnWalkthroughVerificationResult(new(WalkthroughVerificationStatus.Completed, step.ActionType, 0)); }
                finally { _acceptingVerifiedGoal = false; }
                if (finalStep) { _goal.Reset(); _presenter.SetCanCheck(false); }
            }
            else if (actionObserved && _goal.Replans++ < 2)
            {
                _goal.Conversation.Add("Observed mismatch: " + result.Evidence);
                CancelWalkthrough("ReplanRemainingSteps");
                await PlanGoalAsync(current!, CancellationToken.None);
            }
            else
            {
                _goal.AwaitingClarification = true;
                PauseGoal("Not verified: " + result.Evidence + " Select Check again after the action, or tell me what changed.");
                if (step.ActionType == WalkthroughActionType.TextEntry) ObserveVisualText(current!);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log($"GoalVerificationFailed Reason={ex.GetType().Name}");
            if (_goal.IsCurrent(identity)) PauseGoal("Couldn't verify the result. Select Check again to retry.");
        }
        finally
        {
            if (png is not null) Array.Clear(png);
            _checkingGoal = false;
            if (ReferenceEquals(_goalWork, operation)) { _goalWork = null; _presenter.SetBusy(_invocation is not null); }
        }
    }

    private Task NarrateGoalAsync(string message, CancellationToken token)
    {
        // The persistent voice loop processes this turn before draining its narration queue.
        // Awaiting that queue here would deadlock a spoken clarification.
        if (_voice.IsActive) _ = SpeakWalkthroughInstructionAsync(message, token);
        return Task.CompletedTask;
    }

}
