using System.Text.Json;
using LocalTutor.Tools.FileOrganization;

namespace LocalTutor.Tests.FileOrganization;

public sealed class OrganizationPreviewTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "localtutor-organize-" + Guid.NewGuid().ToString("N"));
    public OrganizationPreviewTests() { Directory.CreateDirectory(root); Directory.CreateDirectory(At("sorted")); }
    public void Dispose() => Directory.Delete(root, recursive: true);
    private string At(string name) => Path.Combine(root, name);
    private void Write(string name, string text = "synthetic lesson")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(At(name))!);
        File.WriteAllText(At(name), text);
    }
    private OrganizePreviewTool Tool(string[] paths, string[]? destinations = null) => new(new(root, paths, destinations ?? ["sorted"]));
    private static OrganizationRule Extension(string folder = "sorted") => new(OrganizationRuleKind.Extension, folder, Extension: ".txt");

    [Fact]
    public async Task PreviewReportsEveryMoveAndSkipWithoutChangingFiles()
    {
        Write("lesson/a.txt"); Write("lesson/b.pdf");
        string[] files = ["lesson/a.txt", "lesson/b.pdf", "lesson/missing.txt"];
        var tool = Tool(files);
        Assert.Equal("file.organize_preview", tool.Id);
        var result = await tool.ExecuteAsync(new(files, [Extension()]));
        Assert.True(result.Success, result.Error);
        using var plan = result.Value!;
        Assert.True(Guid.TryParseExact(plan.PlanId, "N", out _));
        Assert.Equal(At("lesson/a.txt"), Assert.Single(plan.Moves).SourcePath);
        Assert.Equal(At("sorted/a.txt"), plan.Moves[0].DestinationPath);
        Assert.Contains(plan.SkippedFiles, s => s.Path == At("lesson/b.pdf") && s.Code == "NoMatchingRule");
        Assert.Contains(plan.SkippedFiles, s => s.Code == "FileUnavailable");
        Assert.Empty(plan.Conflicts);
        Assert.True(File.Exists(At("lesson/a.txt")));
        Assert.Empty(Directory.GetFiles(At("sorted")));
    }

    [Fact]
    public async Task ExistingAndPlannedWindowsNameCollisionsAreDisplayed()
    {
        Write("one/A.txt"); Write("two/a.txt"); Write("sorted/a.TXT", "original");
        var result = await Tool(["one/A.txt", "two/a.txt"]).ExecuteAsync(new(["one/A.txt", "two/a.txt"], [Extension()]));
        Assert.True(result.Success, result.Error);
        using var plan = result.Value!;
        Assert.Equal(2, plan.Moves.Count);
        Assert.Equal(2, plan.Conflicts.Count(c => c.Code == "OutputConflict"));
        Assert.Equal(2, plan.Conflicts.Count(c => c.Code == "PlannedNameCollision"));
        Assert.Equal("original", File.ReadAllText(At("sorted/a.TXT")));
    }

    [Fact]
    public async Task DateAndExplicitLessonRulesWorkAndOverlapsAreSkipped()
    {
        Write("a.txt");
        File.SetLastWriteTimeUtc(At("a.txt"), new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc));
        OrganizationRule date = new(OrganizationRuleKind.ModifiedDate, "sorted", FromDate: new(2026, 10, 9), ThroughDate: new(2026, 10, 9));
        OrganizationRule label = new(OrganizationRuleKind.LessonLabel, "sorted", LessonLabel: "Water cycle");
        foreach (var rule in new[] { date, label })
        {
            var result = await Tool(["a.txt"]).ExecuteAsync(new(["a.txt"], [rule]));
            Assert.True(result.Success, result.Error);
            using var plan = result.Value!;
            Assert.Single(plan.Moves);
        }
        var overlap = await Tool(["a.txt"]).ExecuteAsync(new(["a.txt"], [date, label]));
        using var ambiguous = overlap.Value!;
        Assert.Empty(ambiguous.Moves);
        Assert.Equal("AmbiguousRules", Assert.Single(ambiguous.SkippedFiles).Code);
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("sorted/../a.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("a.txt:stream")]
    [InlineData("NUL.txt")]
    [InlineData("COM¹.txt")]
    [InlineData("a?.txt")]
    [InlineData("a.txt.")]
    [InlineData("//server/share/a.txt")]
    [InlineData(".git/config")]
    public void UnsafeSelectionsAreRejected(string path) => Assert.ThrowsAny<Exception>(() => Tool([path]));

    [Fact]
    public async Task UnauthorizedPathsAndFolderMovesAreRejected()
    {
        Write("a.txt"); Write("b.txt"); Directory.CreateDirectory(At("other"));
        Assert.False((await Tool(["a.txt"]).ExecuteAsync(new(["b.txt"], [Extension()]))).Success);
        Assert.False((await Tool(["a.txt"]).ExecuteAsync(new(["a.txt"], [Extension("other")]))).Success);
        Assert.False((await Tool(["sorted"]).ExecuteAsync(new(["sorted"], [Extension()]))).Success);
        Assert.False((await Tool(["a.txt"]).ExecuteAsync(new(["a.txt", "a.txt"], [Extension()]))).Success);
    }

    [Fact]
    public async Task SymlinksIncludingDestinationAncestorsAreRejected()
    {
        if (OperatingSystem.IsWindows()) return; // Creating Windows links requires device-specific privileges.
        Write("a.txt");
        File.CreateSymbolicLink(At("link.txt"), At("a.txt"));
        Directory.CreateSymbolicLink(At("linked-folder"), At("sorted"));
        Assert.ThrowsAny<Exception>(() => Tool(["link.txt"]));
        Assert.ThrowsAny<Exception>(() => Tool(["a.txt"], ["linked-folder"]));
        File.Delete(At("link.txt")); Directory.Delete(At("linked-folder"));
        var scope = new OrganizationAccessScope(root, ["a.txt"], ["sorted"]);
        File.Delete(At("a.txt")); File.CreateSymbolicLink(At("a.txt"), At("sorted"));
        Assert.False((await new OrganizePreviewTool(scope).ExecuteAsync(new(["a.txt"], [Extension()]))).Success);
    }

    [Fact]
    public async Task InvalidRulesStrictJsonCancellationAndFileLimitsAreEnforced()
    {
        Write("a.txt"); var tool = Tool(["a.txt"]);
        Assert.False((await tool.ExecuteAsync(new([], [Extension()]))).Success);
        Assert.False((await tool.ExecuteAsync(new(["a.txt"], []))).Success);
        Assert.False((await tool.ExecuteAsync(new(["a.txt"], [new((OrganizationRuleKind)99, "sorted")]))).Success);
        Assert.False((await tool.ExecuteAsync(new(["a.txt"], [new(OrganizationRuleKind.ModifiedDate, "sorted", FromDate: new(2026, 10, 9))]))).Success);
        Assert.False((await tool.ExecuteAsync(new(["a.txt"], [new(OrganizationRuleKind.LessonLabel, "sorted", LessonLabel: " ")]))).Success);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<OrganizePreviewInput>("{\"InputPaths\":[\"a.txt\"],\"Rules\":[],\"recursive\":true}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<OrganizeApplyInput>("{}"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tool.ExecuteAsync(new(["a.txt"], [Extension()]), new(true)));
        using (var file = File.OpenWrite(At("a.txt"))) file.SetLength(OrganizationLimits.MaxFileBytes + 1);
        Assert.Equal("ResourceLimit", (await tool.ExecuteAsync(new(["a.txt"], [Extension()]))).Error);
    }

    [Fact]
    public async Task LockedFileIsReportedAndCancellationDuringPreviewWritesNothing()
    {
        Write("a.txt");
        using (var locked = new FileStream(At("a.txt"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var result = await Tool(["a.txt"]).ExecuteAsync(new(["a.txt"], [Extension()]));
            Assert.True(result.Success, result.Error);
            using var plan = result.Value!;
            Assert.Equal("FileUnavailable", Assert.Single(plan.SkippedFiles).Code);
        }
        using CancellationTokenSource cts = new();
        var scope = new OrganizationAccessScope(root, ["a.txt"], ["sorted"]);
        var cancelTool = new OrganizePreviewTool(scope, progress: new InlineProgress(_ => cts.Cancel()));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelTool.ExecuteAsync(new(["a.txt"], [Extension()]), cts.Token));
        Assert.True(File.Exists(At("a.txt")));
        Assert.Empty(Directory.GetFiles(At("sorted")));
    }
}

internal sealed class InlineProgress(Action<OrganizationProgress> report) : IProgress<OrganizationProgress>
{
    public void Report(OrganizationProgress value) => report(value);
}
