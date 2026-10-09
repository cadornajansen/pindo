using LocalTutor.Tools.FileOrganization;

namespace LocalTutor.Tests.FileOrganization;

public sealed class OrganizationApplyTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "localtutor-apply-" + Guid.NewGuid().ToString("N"));
    public OrganizationApplyTests() { Directory.CreateDirectory(root); Directory.CreateDirectory(At("sorted")); }
    public void Dispose() => Directory.Delete(root, recursive: true);
    private string At(string name) => Path.Combine(root, name);
    private async Task<OrganizationPlan> Preview(TimeProvider? clock = null)
    {
        File.WriteAllText(At("a.txt"), "alpha"); File.WriteAllText(At("b.txt"), "bravo");
        var scope = new OrganizationAccessScope(root, ["a.txt", "b.txt"], ["sorted"]);
        var result = await new OrganizePreviewTool(scope, clock).ExecuteAsync(new(["a.txt", "b.txt"], [new(OrganizationRuleKind.Extension, "sorted", Extension: ".txt")]));
        Assert.True(result.Success, result.Error);
        return result.Value!;
    }

    [Fact]
    public async Task ApplyMovesOnlyReviewedFilesAndProvidesReversedHashBoundLog()
    {
        using var plan = await Preview(); File.WriteAllText(At("unrelated.txt"), "leave alone");
        var tool = new OrganizeApplyTool(new(plan)); Assert.Equal("file.organize_apply", tool.Id);
        var result = await tool.ExecuteAsync(new(plan.PlanId));
        Assert.True(result.Success, result.Error);
        Assert.Equal(2, result.Value!.Results.Count(r => r.Status == "Moved"));
        Assert.False(File.Exists(At("a.txt"))); Assert.False(File.Exists(At("b.txt")));
        Assert.Equal("alpha", File.ReadAllText(At("sorted/a.txt"))); Assert.Equal("bravo", File.ReadAllText(At("sorted/b.txt")));
        Assert.Equal("leave alone", File.ReadAllText(At("unrelated.txt")));
        Assert.Equal(At("sorted/b.txt"), result.Value.UndoPlan[0].SourcePath);
        Assert.Equal(At("b.txt"), result.Value.UndoPlan[0].DestinationPath);
        Assert.All(result.Value.UndoPlan, m => Assert.Equal(64, m.Sha256.Length));
        Assert.Equal("ApprovalConsumed", (await tool.ExecuteAsync(new(plan.PlanId))).Error);
    }

    [Fact]
    public async Task ChangedBytesWithRestoredLengthAndTimestampInvalidateEntirePlan()
    {
        using var plan = await Preview(); var time = File.GetLastWriteTimeUtc(At("b.txt"));
        File.WriteAllText(At("b.txt"), "wrong"); File.SetLastWriteTimeUtc(At("b.txt"), time);
        var result = await new OrganizeApplyTool(new(plan)).ExecuteAsync(new(plan.PlanId));
        Assert.False(result.Success); Assert.Equal("StalePlan", result.Error);
        Assert.Empty(result.Value!.UndoPlan); Assert.All(result.Value.Results, r => Assert.Equal("NotMoved", r.Status));
        Assert.True(File.Exists(At("a.txt"))); Assert.Empty(Directory.GetFiles(At("sorted")));
    }

    [Theory]
    [InlineData("OutputConflict")]
    [InlineData("FileUnavailable")]
    [InlineData("DestinationMissing")]
    public async Task PreflightFailureMovesNothing(string failure)
    {
        using var plan = await Preview();
        if (failure == "OutputConflict") File.WriteAllText(At("sorted/B.TXT"), "preserved");
        if (failure == "FileUnavailable") File.Delete(At("b.txt"));
        if (failure == "DestinationMissing") Directory.Delete(At("sorted"));
        var result = await new OrganizeApplyTool(new(plan)).ExecuteAsync(new(plan.PlanId));
        Assert.False(result.Success); Assert.Equal(failure, result.Error);
        Assert.Empty(result.Value!.UndoPlan); Assert.True(File.Exists(At("a.txt")));
        if (failure == "OutputConflict") Assert.Equal("preserved", File.ReadAllText(At("sorted/B.TXT")));
    }

    [Fact]
    public async Task ExpiredDisposedMismatchedAndUnconfirmedPlansCannotMoveFiles()
    {
        var clock = new TestClock(); using var plan = await Preview(clock);
        var tool = new OrganizeApplyTool(new(plan));
        Assert.Equal("ApprovalMismatch", (await tool.ExecuteAsync(new(Guid.NewGuid().ToString("N")))).Error);
        clock.Now += TimeSpan.FromMinutes(5);
        Assert.Equal("ApprovalExpired", (await tool.ExecuteAsync(new(plan.PlanId))).Error);
        using var disposed = await Preview(); disposed.Dispose();
        Assert.Equal("ApprovalConsumed", (await new OrganizeApplyTool(new(disposed)).ExecuteAsync(new(disposed.PlanId))).Error);
        Assert.True(File.Exists(At("a.txt"))); Assert.Empty(Directory.GetFiles(At("sorted")));
        Assert.Throws<ArgumentNullException>(() => new OrganizeApplyTool(null!));
    }

    [Fact]
    public async Task PlanWithDisplayedCollisionCannotBeApplied()
    {
        File.WriteAllText(At("sorted/A.TXT"), "existing"); using var plan = await Preview();
        Assert.Single(plan.Conflicts);
        var result = await new OrganizeApplyTool(new(plan)).ExecuteAsync(new(plan.PlanId));
        Assert.Equal("OutputConflict", result.Error); Assert.Empty(result.Value!.UndoPlan);
        Assert.Equal("existing", File.ReadAllText(At("sorted/A.TXT")));
    }

    [Theory]
    [InlineData("collision", "OutputConflict")]
    [InlineData("changed", "StalePlan")]
    [InlineData("locked", "FileUnavailable")]
    [InlineData("canceled", "Canceled")]
    [InlineData("callback", "ProgressCallbackFailed")]
    [InlineData("disposed", "ApprovalConsumed")]
    [InlineData("expired", "ApprovalExpired")]
    public async Task PartialFailurePreservesCompletedMoveAndReportsUntouchedFile(string mode, string expected)
    {
        var clock = new TestClock(); using var plan = await Preview(clock); using CancellationTokenSource cts = new(); FileStream? locked = null;
        try
        {
            var progress = new InlineProgress(_ =>
            {
                if (mode == "collision") File.WriteAllText(At("sorted/B.TXT"), "existing");
                if (mode == "changed") File.WriteAllText(At("b.txt"), "changed");
                if (mode == "locked") locked = new(At("b.txt"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                if (mode == "canceled") cts.Cancel();
                if (mode == "callback") throw new InvalidOperationException("bad observer");
                if (mode == "disposed") plan.Dispose();
                if (mode == "expired") clock.Now += TimeSpan.FromMinutes(5);
            });
            var result = await new OrganizeApplyTool(new(plan), progress).ExecuteAsync(new(plan.PlanId), cts.Token);
            Assert.False(result.Success); Assert.Equal(expected, result.Error);
            Assert.Equal("Moved", result.Value!.Results[0].Status); Assert.NotEqual("Moved", result.Value.Results[1].Status);
            Assert.Single(result.Value.UndoPlan); Assert.Equal(At("a.txt"), result.Value.UndoPlan[0].DestinationPath);
            Assert.Equal("alpha", File.ReadAllText(At("sorted/a.txt"))); Assert.True(File.Exists(At("b.txt")));
            if (mode == "collision") Assert.Equal("existing", File.ReadAllText(At("sorted/B.TXT")));
        }
        finally { locked?.Dispose(); }
    }

    [Fact]
    public async Task PreCancellationDoesNotConsumePlanAndConcurrentApplyMovesAtMostOnce()
    {
        using var plan = await Preview(); var tool = new OrganizeApplyTool(new(plan));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tool.ExecuteAsync(new(plan.PlanId), new(true)));
        var results = await Task.WhenAll(tool.ExecuteAsync(new(plan.PlanId)), tool.ExecuteAsync(new(plan.PlanId)));
        Assert.Single(results, r => r.Success); Assert.Single(results, r => r.Error == "ApprovalConsumed");
        Assert.Equal("alpha", File.ReadAllText(At("sorted/a.txt")));
    }

    [Fact]
    public async Task DestinationReplacedWithLinkAfterPreviewIsRejected()
    {
        if (OperatingSystem.IsWindows()) return;
        using var plan = await Preview(); Directory.Delete(At("sorted"));
        Directory.CreateDirectory(At("other")); Directory.CreateSymbolicLink(At("sorted"), At("other"));
        var result = await new OrganizeApplyTool(new(plan)).ExecuteAsync(new(plan.PlanId));
        Assert.False(result.Success); Assert.Equal("LinkedOrSpecialPath", result.Error);
        Assert.Empty(result.Value!.UndoPlan); Assert.Empty(Directory.GetFiles(At("other"))); Assert.True(File.Exists(At("a.txt")));
    }
}

internal sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
