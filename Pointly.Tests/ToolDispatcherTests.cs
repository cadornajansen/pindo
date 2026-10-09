using System.IO;
using System.Text.Json;
using Pointly.App.Tools;
using Pointly.App.Tutor;

namespace Pointly.Tests;

public sealed class ToolDispatcherTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pindo-dispatch-tests-" + Guid.NewGuid().ToString("N"));
    private readonly LocalToolDispatcher _dispatcher = new(new(null, null, null, null));
    public ToolDispatcherTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);
    private static ToolProposal Proposal(string id, object arguments) => new(id, JsonSerializer.SerializeToElement(arguments));

    [Fact]
    public void UnknownToolsAndModelApprovalFieldsAreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => LocalToolDispatcher.Parse(Proposal("pdf.optimize", new { })));
        Assert.Throws<JsonException>(() => LocalToolDispatcher.Parse(Proposal("file.inspect", new { inputPath = "a.txt", approval = true })));
    }

    [Fact]
    public async Task ReviewCannotExecuteTwiceOrAfterCancellation()
    {
        int writes = 0;
        using var review = new ToolReview("Test", new { output = "test" }, _ => Task.FromResult<object>(++writes));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => review.RunAsync(cancelled.Token));
        Assert.Equal(0, writes);
        await review.RunAsync(CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => review.RunAsync(CancellationToken.None));
        Assert.Equal(1, writes);
    }

    [Fact]
    public async Task ZipPreviewWritesNothingAndRejectsChangedSource()
    {
        string source = Path.Combine(_root, "lesson.txt"), output = Path.Combine(_root, "lesson.zip");
        await File.WriteAllTextAsync(source, "original");
        using ToolReview review = await _dispatcher.PrepareAsync(Proposal("archive.create_zip", new { inputPaths = new[] { source }, outputPath = output }),
            new(_root, [source], [], []), CancellationToken.None);
        Assert.False(File.Exists(output));
        await File.WriteAllTextAsync(source, "changed");
        await Assert.ThrowsAsync<InvalidOperationException>(() => review.RunAsync(CancellationToken.None));
        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task ZipRunsOnlyForSelectedFilesAndPreservesOriginal()
    {
        string source = Path.Combine(_root, "lesson.txt"), output = Path.Combine(_root, "lesson.zip");
        await File.WriteAllTextAsync(source, "original");
        ToolProposal proposal = Proposal("archive.create_zip", new { inputPaths = new[] { source }, outputPath = output });
        await Assert.ThrowsAsync<InvalidOperationException>(() => _dispatcher.PrepareAsync(proposal, new(_root, [], [], []), CancellationToken.None));
        using ToolReview review = await _dispatcher.PrepareAsync(proposal, new(_root, [source], [], []), CancellationToken.None);
        await review.RunAsync(CancellationToken.None);
        Assert.True(File.Exists(output));
        Assert.Equal("original", await File.ReadAllTextAsync(source));
        using ToolReview conflict = await _dispatcher.PrepareAsync(proposal, new(_root, [source], [], []), CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => conflict.RunAsync(CancellationToken.None));
    }
}
