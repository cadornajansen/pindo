using LocalTutor.Core.Tools;

namespace LocalTutor.Tools.FileOrganization;

public sealed class OrganizeApplyTool(OrganizationApproval approval, IProgress<OrganizationProgress>? progress = null)
    : LocalTool<OrganizeApplyInput, AppliedOrganization>
{
    private readonly OrganizationApproval approval = approval ?? throw new ArgumentNullException(nameof(approval));
    public override string Id => "file.organize_apply";
    public override string Description => "Apply only an explicitly confirmed, unchanged file-move preview. Refuses overwrites and cross-volume moves; returns each outcome and a reverse move log.";

    protected override Task<ToolResult<AppliedOrganization>> ExecuteValidatedAsync(OrganizeApplyInput input, CancellationToken cancellationToken) =>
        OrganizationIO.RunAsync<AppliedOrganization>(async token =>
        {
            OrganizationPlan plan = approval.Plan;
            List<MoveResult> results = [];
            List<UndoMove> undo = [];
            string? failure = null;
            try
            {
                plan.Consume(input.PlanId);
                // Validate the entire approved plan before the first move, then recheck immediately before each rename.
                foreach (PlannedMove move in plan.Snapshot) await RecheckAsync(plan, move, token);
                for (int i = 0; i < plan.Snapshot.Length; i++)
                {
                    PlannedMove move = plan.Snapshot[i];
                    try
                    {
                        await RecheckAsync(plan, move, token);
                        token.ThrowIfCancellationRequested();
                        OrganizationIO.Move(move.Details.SourcePath, move.Details.DestinationPath);
                    }
                    catch (Exception e) when (OrganizationIO.IsFileError(e))
                    {
                        failure = OrganizationIO.Code(e);
                        results.Add(new(move.Details.SourcePath, move.Details.DestinationPath, "Failed", failure));
                        break;
                    }
                    results.Add(new(move.Details.SourcePath, move.Details.DestinationPath, "Moved"));
                    undo.Insert(0, new(move.Details.DestinationPath, move.Details.SourcePath, move.File.Bytes, move.File.Sha256));
                    try { progress?.Report(new("Apply", i + 1, plan.Snapshot.Length)); }
                    catch (Exception) { failure = "ProgressCallbackFailed"; break; }
                }
                if (failure is null) token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) { failure = cancellationToken.IsCancellationRequested ? "Canceled" : "TimedOut"; }
            catch (Exception e) when (OrganizationIO.IsFileError(e)) { failure = OrganizationIO.Code(e); }
            for (int i = results.Count; i < plan.Snapshot.Length; i++)
            {
                ProposedMove move = plan.Snapshot[i].Details;
                results.Add(new(move.SourcePath, move.DestinationPath, "NotMoved", failure));
            }
            return new(failure is null, new(plan.PlanId, results.AsReadOnly(), undo.AsReadOnly()), failure);
        }, cancellationToken);

    private static async Task RecheckAsync(OrganizationPlan plan, PlannedMove move, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        plan.CheckFresh();
        string source = plan.Scope.SelectedFile(move.Details.SourcePath);
        string directory = plan.Scope.Destination(Path.GetDirectoryName(move.Details.DestinationPath)!);
        string destination = plan.Scope.Canonicalize(Path.Combine(directory, Path.GetFileName(source)));
        if (destination != move.Details.DestinationPath) throw new OrganizationException("StalePlan");
        if (OrganizationIO.DestinationExists(destination, token)) throw new OrganizationException("OutputConflict");
        OrganizationIO.CheckVolume(source, destination);
        if (await OrganizationIO.SnapshotAsync(source, token) != move.File) throw new OrganizationException("StalePlan");
        plan.CheckFresh();
    }
}
