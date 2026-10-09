using LocalTutor.Core.Tools;

namespace LocalTutor.Tools.FileOrganization;

public sealed class OrganizePreviewTool(OrganizationAccessScope scope, TimeProvider? clock = null,
    IProgress<OrganizationProgress>? progress = null) : LocalTool<OrganizePreviewInput, OrganizationPlan>
{
    private readonly OrganizationAccessScope scope = scope ?? throw new ArgumentNullException(nameof(scope));
    public override string Id => "file.organize_preview";
    public override string Description => "Preview explicit selected file moves into approved existing lesson folders. Reports conflicts and skips; moves nothing.";

    protected override Task<ToolResult<OrganizationPlan>> ExecuteValidatedAsync(OrganizePreviewInput input, CancellationToken cancellationToken)
    {
        string[] paths = input.InputPaths.ToArray();
        OrganizationRule[] rules = input.Rules.ToArray();
        return OrganizationIO.RunAsync<OrganizationPlan>(async token =>
        {
            string[] sources = paths.Select(scope.SelectedFile).ToArray();
            if (sources.Distinct(StringComparer.OrdinalIgnoreCase).Count() != sources.Length) throw new OrganizationException("DuplicateSelection");
            rules = rules.Select(r => r with { DestinationDirectory = scope.Destination(r.DestinationDirectory) }).ToArray();
            List<PlannedMove> moves = [];
            List<OrganizationConflict> conflicts = [];
            List<SkippedFile> skipped = [];
            long total = 0;
            foreach (string source in sources)
            {
                token.ThrowIfCancellationRequested();
                FileSnapshot snapshot;
                try { snapshot = await OrganizationIO.SnapshotAsync(source, token); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                { skipped.Add(new(source, "FileUnavailable")); continue; }
                total += snapshot.Bytes;
                if (total > OrganizationLimits.MaxTotalBytes) throw new OrganizationException("ResourceLimit");
                OrganizationRule[] matching = rules.Where(r => r.Matches(source, snapshot.ModifiedUtc)).ToArray();
                if (matching.Length != 1) { skipped.Add(new(source, matching.Length == 0 ? "NoMatchingRule" : "AmbiguousRules")); continue; }
                OrganizationRule rule = matching[0];
                string destination = scope.Canonicalize(Path.Combine(rule.DestinationDirectory, Path.GetFileName(source)));
                if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase)) { skipped.Add(new(source, "AlreadyOrganized")); continue; }
                string description = rule.Kind switch
                {
                    OrganizationRuleKind.Extension => "Extension: " + rule.Extension,
                    OrganizationRuleKind.ModifiedDate => $"Modified UTC: {rule.FromDate:yyyy-MM-dd} through {rule.ThroughDate:yyyy-MM-dd}",
                    _ => "Lesson: " + rule.LessonLabel
                };
                moves.Add(new(new(source, destination, snapshot.Bytes, description), snapshot));
                if (OrganizationIO.DestinationExists(destination, token)) conflicts.Add(new(source, destination, "OutputConflict"));
                try { OrganizationIO.CheckVolume(source, destination); }
                catch (OrganizationException e) { conflicts.Add(new(source, destination, OrganizationIO.Code(e))); }
                progress?.Report(new("Preview", moves.Count + skipped.Count, sources.Length));
            }
            foreach (var group in moves.GroupBy(m => m.Details.DestinationPath, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                conflicts.AddRange(group.Select(m => new OrganizationConflict(m.Details.SourcePath, m.Details.DestinationPath, "PlannedNameCollision")));
            token.ThrowIfCancellationRequested();
            return new(true, new OrganizationPlan(scope, moves.ToArray(), conflicts.ToArray(), skipped.ToArray(), clock ?? TimeProvider.System));
        }, cancellationToken);
    }
}
