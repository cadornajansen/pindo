using System.Text.Json.Serialization;
using LocalTutor.Core.Tools;

namespace LocalTutor.Tools.FileOrganization;

[JsonConverter(typeof(JsonStringEnumConverter<OrganizationRuleKind>))]
public enum OrganizationRuleKind { Extension, ModifiedDate, LessonLabel }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OrganizationRule(
    [property: JsonRequired] OrganizationRuleKind Kind,
    [property: JsonRequired] string DestinationDirectory,
    string? Extension = null, DateOnly? FromDate = null, DateOnly? ThroughDate = null, string? LessonLabel = null)
{
    internal bool Matches(string path, DateTime modifiedUtc) => Kind switch
    {
        OrganizationRuleKind.Extension => string.Equals(Path.GetExtension(path), Extension, StringComparison.OrdinalIgnoreCase),
        OrganizationRuleKind.ModifiedDate => DateOnly.FromDateTime(modifiedUtc) >= FromDate && DateOnly.FromDateTime(modifiedUtc) <= ThroughDate,
        OrganizationRuleKind.LessonLabel => true,
        _ => false
    };
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OrganizePreviewInput(
    [property: JsonRequired] string[] InputPaths,
    [property: JsonRequired] OrganizationRule[] Rules) : ToolInput
{
    public override IReadOnlyList<string> Validate()
    {
        List<string> errors = [.. OrganizationAccessScope.ValidatePaths(InputPaths, OrganizationLimits.MaxFiles)];
        if (Rules is null || Rules.Length is 0 or > 16) errors.Add("InvalidInput: select one to sixteen explicit rules.");
        else foreach (OrganizationRule? rule in Rules)
        {
            if (rule is null) { errors.Add("InvalidRule: a rule is required."); continue; }
            errors.AddRange(OrganizationAccessScope.ValidatePaths([rule.DestinationDirectory], 1));
            bool valid = rule.Kind switch
            {
                OrganizationRuleKind.Extension => rule.Extension is { Length: > 1 and <= 16 } && rule.Extension[0] == '.' &&
                    rule.Extension[1..].All(char.IsAsciiLetterOrDigit) && rule.FromDate is null && rule.ThroughDate is null && rule.LessonLabel is null,
                OrganizationRuleKind.ModifiedDate => rule.FromDate is not null && rule.ThroughDate >= rule.FromDate &&
                    rule.Extension is null && rule.LessonLabel is null,
                OrganizationRuleKind.LessonLabel => !string.IsNullOrWhiteSpace(rule.LessonLabel) && rule.LessonLabel.Length <= 100 &&
                    !rule.LessonLabel.Any(char.IsControl) && rule.Extension is null && rule.FromDate is null && rule.ThroughDate is null,
                _ => false
            };
            if (!valid) errors.Add("InvalidRule: use an extension, inclusive UTC modification dates, or an explicit lesson label.");
        }
        return errors;
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OrganizeApplyInput([property: JsonRequired] string PlanId) : ToolInput
{
    public override IReadOnlyList<string> Validate() => Guid.TryParseExact(PlanId, "N", out _) ? [] : ["InvalidPlanId: select a reviewed plan."];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FindDuplicatesInput([property: JsonRequired] string[] Roots) : ToolInput
{
    public override IReadOnlyList<string> Validate() => OrganizationAccessScope.ValidatePaths(Roots, 8);
}

public static class OrganizationLimits
{
    public const int MaxFiles = 128;
    public const int MaxEntries = 1024;
    public const int MaxDepth = 8;
    public const long MaxFileBytes = 64L * 1024 * 1024;
    public const long MaxTotalBytes = 256L * 1024 * 1024;
    public static TimeSpan OperationTimeout => TimeSpan.FromMinutes(2);
}

public sealed record ProposedMove(string SourcePath, string DestinationPath, long SizeBytes, string RuleDescription);
public sealed record OrganizationConflict(string SourcePath, string DestinationPath, string Code);
public sealed record SkippedFile(string Path, string Code);
public sealed record OrganizationProgress(string Stage, int CompletedFiles, int TotalFiles);
public sealed record MoveResult(string SourcePath, string DestinationPath, string Status, string? Error = null);
public sealed record UndoMove(string SourcePath, string DestinationPath, long SizeBytes, string Sha256);
public sealed record AppliedOrganization(string PlanId, IReadOnlyList<MoveResult> Results, IReadOnlyList<UndoMove> UndoPlan);
public sealed record DuplicateGroup(long SizeBytes, string Sha256, IReadOnlyList<string> Paths);
public sealed record FoundDuplicates(IReadOnlyList<DuplicateGroup> Groups, IReadOnlyList<SkippedFile> SkippedFiles, int ScannedFiles);

/// <summary>Host-owned, immutable preview. Display every move, conflict and skip before requesting consent.</summary>
public sealed class OrganizationPlan : IDisposable
{
    private int consumed;
    private readonly TimeProvider clock;
    internal OrganizationAccessScope Scope { get; }
    internal PlannedMove[] Snapshot { get; }
    public string PlanId { get; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset ExpiresAt { get; }
    public IReadOnlyList<ProposedMove> Moves { get; }
    public IReadOnlyList<OrganizationConflict> Conflicts { get; }
    public IReadOnlyList<SkippedFile> SkippedFiles { get; }

    internal OrganizationPlan(OrganizationAccessScope scope, PlannedMove[] moves, OrganizationConflict[] conflicts,
        SkippedFile[] skipped, TimeProvider clock)
    {
        Scope = scope; Snapshot = moves; this.clock = clock;
        Moves = Array.AsReadOnly(moves.Select(m => m.Details).ToArray());
        Conflicts = Array.AsReadOnly(conflicts); SkippedFiles = Array.AsReadOnly(skipped);
        ExpiresAt = clock.GetUtcNow().AddMinutes(5);
    }

    internal void Consume(string id)
    {
        if (id != PlanId) throw new OrganizationException("ApprovalMismatch");
        if (Interlocked.CompareExchange(ref consumed, 1, 0) != 0) throw new OrganizationException("ApprovalConsumed");
        CheckFresh();
        if (Conflicts.Count != 0) throw new OrganizationException("OutputConflict");
    }

    internal void CheckFresh()
    {
        if (Volatile.Read(ref consumed) == 2) throw new OrganizationException("ApprovalConsumed");
        if (clock.GetUtcNow() >= ExpiresAt) throw new OrganizationException("ApprovalExpired");
    }

    public void Dispose() => Interlocked.Exchange(ref consumed, 2);
}

/// <summary>Construct only in trusted host code after explicit user confirmation; never deserialize model output as approval.</summary>
public sealed class OrganizationApproval(OrganizationPlan plan)
{
    internal OrganizationPlan Plan { get; } = plan ?? throw new ArgumentNullException(nameof(plan));
}

internal sealed record FileSnapshot(long Bytes, DateTime ModifiedUtc, DateTime CreatedUtc, string Sha256);
internal sealed record PlannedMove(ProposedMove Details, FileSnapshot File);
internal sealed class OrganizationException(string code) : Exception(code + ": request a fresh preview or a safe local selection.");
