using System.Text.Json;
using Pointly.App.Walkthrough;

namespace Pointly.App.Tutor;

public sealed record PlannedStep(string Instruction, string Target, WalkthroughActionType Action,
    string ExpectedResult, string? Text = null, int? VirtualKey = null);
public sealed record ToolProposal(string ToolId, JsonElement Arguments);
public sealed record PlannerOutcome(string Kind, string Message, PlannedStep[] Steps, ToolProposal? Tool = null)
{
    public void Validate()
    {
        if (Kind is not ("clarification" or "plan" or "tool" or "complete") ||
            string.IsNullOrWhiteSpace(Message) || Message.Length > 2000 || Steps is null || Steps.Length > 20)
            throw new InvalidOperationException("The planner returned an invalid response.");
        if (Kind == "plan" && (Steps.Length == 0 || Steps.Any(step =>
                string.IsNullOrWhiteSpace(step.Instruction) || step.Instruction.Length > 1000 ||
                string.IsNullOrWhiteSpace(step.ExpectedResult) || step.ExpectedResult.Length > 1000 ||
                !Enum.IsDefined(step.Action) ||
                (step.Action != WalkthroughActionType.KeyPress && string.IsNullOrWhiteSpace(step.Target)) ||
                (step.Action == WalkthroughActionType.TextEntry && string.IsNullOrEmpty(step.Text)) ||
                (step.Action == WalkthroughActionType.KeyPress && step.VirtualKey is not (> 0 and <= 255)))))
            throw new InvalidOperationException("The plan contains an incomplete step.");
        if (Kind == "tool" && (Tool is null || string.IsNullOrWhiteSpace(Tool.ToolId) || Tool.Arguments.ValueKind != JsonValueKind.Object))
            throw new InvalidOperationException("The tool proposal is incomplete.");
    }
}

public sealed record GoalVerification(bool Matched, string Evidence);

public sealed class GoalSession
{
    public long Identity { get; private set; }
    public string Goal { get; private set; } = "";
    public bool AwaitingClarification { get; set; }
    public bool Active => Goal.Length > 0;
    public List<string> Conversation { get; } = [];
    public List<string> Completed { get; } = [];
    public int Replans { get; set; }

    public void Accept(string question)
    {
        if (!AwaitingClarification) { Reset(); Goal = question; }
        Conversation.Add(question);
        AwaitingClarification = false;
    }

    public bool IsCurrent(long identity) => Active && Identity == identity;
    public void Reset()
    {
        Identity++;
        Goal = "";
        AwaitingClarification = false;
        Conversation.Clear();
        Completed.Clear();
        Replans = 0;
    }
}
