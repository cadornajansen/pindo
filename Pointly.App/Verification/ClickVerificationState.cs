namespace Pointly.App.Verification;

/// <summary>Pure one-target state machine; a new session ID invalidates queued old clicks.</summary>
public sealed class ClickVerificationState
{
    private long _nextSessionId;
    private VerifiedClickTarget? _target;

    public long ActiveSessionId { get; private set; }

    public long Start(VerifiedClickTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        _target = target;
        ActiveSessionId = ++_nextSessionId;
        return ActiveSessionId;
    }

    public StepVerificationStatus? RecordClick(long sessionId, ScreenPoint click)
    {
        if (sessionId == 0 || sessionId != ActiveSessionId || _target is null) return null;
        if (!_target.Contains(click)) return StepVerificationStatus.MissedTarget;

        _target = null;
        ActiveSessionId = 0;
        return StepVerificationStatus.Completed;
    }

    public bool Cancel(long sessionId)
    {
        if (sessionId == 0 || sessionId != ActiveSessionId) return false;
        _target = null;
        ActiveSessionId = 0;
        return true;
    }
}
