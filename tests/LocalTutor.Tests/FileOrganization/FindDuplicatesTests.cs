using LocalTutor.Tools.FileOrganization;

namespace LocalTutor.Tests.FileOrganization;

public sealed class FindDuplicatesTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "localtutor-duplicates-" + Guid.NewGuid().ToString("N"));
    public FindDuplicatesTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, recursive: true);
    private string At(string name) => Path.GetFullPath(Path.Combine(root, name));
    private void Write(string name, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(At(name))!); File.WriteAllText(At(name), content);
    }
    private FindDuplicatesTool Tool(IProgress<OrganizationProgress>? progress = null) => new(new(root, [], [], [root]), progress);

    [Fact]
    public async Task SizeAndSha256GroupIdenticalBytesRegardlessOfNameOrExtension()
    {
        Write("a.txt", "lesson"); Write("nested/b.png", "lesson"); Write("c.txt", "LESSON");
        Write("empty-one.txt", ""); Write("empty-two.bin", ""); Write("different-size.txt", "longer lesson");
        var tool = Tool(); Assert.Equal("file.find_duplicates", tool.Id);
        var result = await tool.ExecuteAsync(new([root]));
        Assert.True(result.Success, result.Error); Assert.Equal(6, result.Value!.ScannedFiles);
        Assert.Equal(2, result.Value.Groups.Count); Assert.Empty(result.Value.SkippedFiles);
        var group = Assert.Single(result.Value.Groups, g => g.SizeBytes == 6);
        Assert.Equal(new[] { At("a.txt"), At("nested/b.png") }, group.Paths);
        Assert.Equal(64, group.Sha256.Length); Assert.Equal(6, Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length);
        Assert.Equal("LESSON", File.ReadAllText(At("c.txt")));
    }

    [Fact]
    public async Task OverlappingSelectedRootsDoNotDuplicatePaths()
    {
        Write("a.txt", "lesson"); Write("nested/b.txt", "lesson");
        var scope = new OrganizationAccessScope(root, [], [], [root, "nested"]);
        var result = await new FindDuplicatesTool(scope).ExecuteAsync(new([root, "nested"]));
        Assert.True(result.Success, result.Error); Assert.Equal(2, result.Value!.ScannedFiles);
        Assert.Equal(2, Assert.Single(result.Value.Groups).Paths.Count);
    }

    [Fact]
    public async Task LockedChangedLinkedAndProtectedFilesAreExcludedWithoutDeletion()
    {
        Write("a.txt", "lesson"); Write("b.txt", "lesson"); Write("c.txt", "lesson"); Write(".git/config", "lesson");
        using var locked = new FileStream(At("b.txt"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        if (!OperatingSystem.IsWindows()) File.CreateSymbolicLink(At("linked.txt"), At("a.txt"));
        var result = await Tool().ExecuteAsync(new([root]));
        Assert.True(result.Success, result.Error);
        Assert.Equal(2, Assert.Single(result.Value!.Groups).Paths.Count);
        Assert.Contains(result.Value.SkippedFiles, s => s.Path == At("b.txt") && s.Code == "FileUnavailable");
        Assert.Contains(result.Value.SkippedFiles, s => s.Code == "ProtectedPath");
        if (!OperatingSystem.IsWindows()) Assert.Contains(result.Value.SkippedFiles, s => s.Code == "LinkedOrSpecialPath");
        Assert.True(File.Exists(At("b.txt"))); Assert.True(File.Exists(At(".git/config")));
    }

    [Fact]
    public async Task ContentChangedDuringHashingIsRemovedBeforeReportingGroups()
    {
        Write("a.txt", "lesson"); Write("b.txt", "lesson");
        var result = await Tool(new InlineProgress(_ => File.WriteAllText(At("a.txt"), "CHANGED"))).ExecuteAsync(new([root]));
        Assert.True(result.Success, result.Error); Assert.Empty(result.Value!.Groups);
        Assert.Contains(result.Value.SkippedFiles, s => s.Path == At("a.txt") && s.Code == "SourceChanged");
    }

    [Fact]
    public async Task SelectionValidationCancellationAndResourceLimitsAreBounded()
    {
        Write("a.txt", "lesson"); Write("b.txt", "lesson"); Directory.CreateDirectory(At("unapproved"));
        Assert.Equal("RootNotApproved", (await Tool().ExecuteAsync(new(["unapproved"]))).Error);
        Assert.False((await Tool().ExecuteAsync(new([]))).Success);
        Assert.False((await Tool().ExecuteAsync(new(["../outside"]))).Success);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Tool().ExecuteAsync(new([root]), new(true)));
        using CancellationTokenSource cts = new();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Tool(new InlineProgress(_ => cts.Cancel())).ExecuteAsync(new([root]), cts.Token));
        Assert.Equal("lesson", File.ReadAllText(At("a.txt")));
        for (int i = 0; i < OrganizationLimits.MaxFiles; i++) Write($"file-{i}.txt", "data");
        Assert.Equal("ResourceLimit", (await Tool().ExecuteAsync(new([root]))).Error);
    }

    [Fact]
    public async Task DepthAndPerFileSizeLimitsReportSkippedPaths()
    {
        string nested = "deep";
        for (int i = 0; i < OrganizationLimits.MaxDepth + 1; i++) nested = Path.Combine(nested, "next");
        Write(Path.Combine(nested, "a.txt"), "lesson"); Write("huge.txt", "");
        using (var file = File.OpenWrite(At("huge.txt"))) file.SetLength(OrganizationLimits.MaxFileBytes + 1);
        var result = await Tool().ExecuteAsync(new([root]));
        Assert.True(result.Success, result.Error); Assert.Empty(result.Value!.Groups);
        Assert.Contains(result.Value.SkippedFiles, s => s.Code == "DepthLimit");
        Assert.Contains(result.Value.SkippedFiles, s => s.Code == "FileSizeLimit");
    }
}
