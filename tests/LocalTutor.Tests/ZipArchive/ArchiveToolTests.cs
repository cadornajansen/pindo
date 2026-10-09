using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using LocalTutor.Tools.ZipArchive;
using Zip = System.IO.Compression.ZipArchive;

namespace LocalTutor.Tests.ZipArchive;

public sealed class ArchiveToolTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "localtutor-zip-tests-" + Guid.NewGuid().ToString("N"));
    public ArchiveToolTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, recursive: true);
    private string At(string name) => Path.GetFullPath(Path.Combine(root, name));
    private ArchiveAccessScope Scope(string[] inputs, string output) => new(root, inputs, [output]);

    private void Write(string name, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(At(name))!);
        File.WriteAllText(At(name), text);
    }

    private void MakeZip(params (string Name, string Text, int Attributes)[] entries)
    {
        using FileStream file = File.Create(At("input.zip"));
        using Zip zip = new(file, ZipArchiveMode.Create);
        foreach (var item in entries)
        {
            var entry = zip.CreateEntry(item.Name, CompressionLevel.NoCompression);
            entry.ExternalAttributes = item.Attributes;
            using Stream stream = entry.Open();
            stream.Write(Encoding.UTF8.GetBytes(item.Text));
        }
    }

    private async Task<ArchivePreview> CreatePreview(CreateZipInput input, TimeProvider? clock = null)
    {
        var result = await CreateZipTool.PreviewAsync(Scope(input.InputPaths, input.OutputPath), input, clock: clock);
        Assert.True(result.Success, result.Error);
        return result.Value!;
    }

    private async Task<ArchivePreview> ExtractPreview(ExtractZipInput? input = null, TimeProvider? clock = null)
    {
        input ??= new("input.zip", "out");
        var result = await ExtractZipTool.PreviewAsync(Scope([input.ArchivePath], input.OutputDirectory), input, clock: clock);
        Assert.True(result.Success, result.Error);
        return result.Value!;
    }

    private void NoPartials() => Assert.Empty(Directory.EnumerateFileSystemEntries(root, ".localtutor-zip-*"));

    [Fact]
    public async Task NestedFoldersAndEmptyDirectoriesRoundTripWithoutChangingOriginals()
    {
        Write("lesson/nested/notes.txt", "Private synthetic lesson notes");
        Directory.CreateDirectory(At("lesson/empty"));
        CreateZipInput input = new(["lesson"], "lesson.zip");
        using ArchivePreview preview = await CreatePreview(input);
        Assert.Equal(4, preview.EntryCount);
        Assert.Equal(new FileInfo(At("lesson/nested/notes.txt")).Length, preview.TotalInputBytes);
        Assert.Contains(preview.Entries, e => e.Name == "lesson/empty/" && e.IsDirectory);
        Assert.Empty(preview.Conflicts);
        Assert.False(File.Exists(At("lesson.zip")));
        Assert.Equal("archive.create_zip", new CreateZipTool(new(preview)).Id);
        var created = await new CreateZipTool(new(preview)).ExecuteAsync(input);
        Assert.True(created.Success, created.Error);
        Assert.Equal(new FileInfo(At("lesson.zip")).Length, created.Value!.ArchiveBytes);
        ExtractZipInput extract = new("lesson.zip", "unpacked");
        using ArchivePreview tree = await ExtractPreview(extract);
        var extracted = await new ExtractZipTool(new(tree)).ExecuteAsync(extract);
        Assert.True(extracted.Success, extracted.Error);
        Assert.Equal(1, extracted.Value!.FileCount);
        Assert.Equal(3, extracted.Value.DirectoryCount);
        Assert.Equal(4, extracted.Value.ExtractedPaths.Count);
        Assert.True(Directory.Exists(At("unpacked/lesson/empty")));
        Assert.Equal(File.ReadAllText(At("lesson/nested/notes.txt")), File.ReadAllText(At("unpacked/lesson/nested/notes.txt")));
        NoPartials();
    }

    [Fact]
    public async Task EmptyZipAndEmptySelectedFolderAreSupported()
    {
        MakeZip();
        using ArchivePreview preview = await ExtractPreview();
        Assert.Empty(preview.Entries);
        var result = await new ExtractZipTool(new(preview)).ExecuteAsync(new("input.zip", "out"));
        Assert.True(result.Success, result.Error);
        Assert.Equal(0, result.Value!.FileCount);
        Assert.True(Directory.Exists(At("out")));
        Directory.CreateDirectory(At("empty"));
        CreateZipInput input = new(["empty"], "empty.zip");
        using ArchivePreview create = await CreatePreview(input);
        Assert.Single(create.Entries);
        Assert.True((await new CreateZipTool(new(create)).ExecuteAsync(input)).Success);
    }

    [Fact]
    public async Task ExplicitExclusionsAndOutputInsideSelectionArePreviewed()
    {
        Write("lesson/keep.txt", "keep");
        Write("lesson/private/exclude.txt", "exclude");
        CreateZipInput input = new(["lesson"], "lesson/result.zip", ["lesson/private"]);
        using ArchivePreview preview = await CreatePreview(input);
        Assert.Equal(new[] { At("lesson/private"), At("lesson/result.zip") }, preview.Exclusions);
        Assert.Equal(new[] { "lesson/", "lesson/keep.txt" }, preview.Entries.Select(e => e.Name));
        Assert.True((await new CreateZipTool(new(preview)).ExecuteAsync(input)).Success);
        Assert.Equal("exclude", File.ReadAllText(At("lesson/private/exclude.txt")));
        NoPartials();
    }

    [Fact]
    public async Task ImplicitOutputDirectoriesAppearInPreviewAndResults()
    {
        MakeZip(("one/two/file.txt", "data", 0));
        using ArchivePreview preview = await ExtractPreview();
        Assert.Contains(preview.Entries, e => e.Name == "one/");
        Assert.Contains(preview.Entries, e => e.Name == "one/two/");
        var result = await new ExtractZipTool(new(preview)).ExecuteAsync(new("input.zip", "out"));
        Assert.True(result.Success, result.Error);
        Assert.Equal(2, result.Value!.DirectoryCount);
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("folder/../../escape.txt")]
    [InlineData("/absolute.txt")]
    [InlineData("C:/drive.txt")]
    [InlineData("C:relative.txt")]
    [InlineData("\\\\server\\share\\file.txt")]
    [InlineData("folder\\file.txt")]
    [InlineData("folder//file.txt")]
    [InlineData("./file.txt")]
    [InlineData("file.txt:stream")]
    [InlineData("folder/file.txt.")]
    [InlineData("folder/file.txt ")]
    [InlineData("NUL.txt")]
    [InlineData("COM¹.txt")]
    [InlineData("LONGFI~1.TXT")]
    [InlineData("line\nname")]
    [InlineData("e\u0301.txt")]
    [InlineData("bad\0name.txt")]
    public async Task TraversalAndAmbiguousWindowsNamesAreRejectedBeforeWriting(string name)
    {
        MakeZip((name, "payload", 0));
        var result = await ExtractZipTool.PreviewAsync(Scope(["input.zip"], "out"), new("input.zip", "out"));
        Assert.False(result.Success);
        Assert.Contains("UnsafeEntryName", result.Error);
        Assert.False(Directory.Exists(At("out")));
        NoPartials();
    }

    [Theory]
    [InlineData("same.txt", "same.txt")]
    [InlineData("same.txt", "SAME.TXT")]
    [InlineData("folder", "folder/file.txt")]
    [InlineData("Folder/a.txt", "folder/b.txt")]
    [InlineData("folder/", "folder/")]
    public async Task DuplicateAndCollidingTreesAreRejected(string first, string second)
    {
        MakeZip((first, first.EndsWith('/') ? "" : "a", 0), (second, second.EndsWith('/') ? "" : "b", 0));
        var result = await ExtractZipTool.PreviewAsync(Scope(["input.zip"], "out"), new("input.zip", "out"));
        Assert.False(result.Success);
        Assert.Contains("EntryConflict", result.Error);
    }

    [Theory]
    [InlineData(unchecked((int)0xa1ff0000))]
    [InlineData(unchecked((int)0x11ff0000))]
    [InlineData((int)FileAttributes.ReparsePoint)]
    [InlineData((int)FileAttributes.Device)]
    public async Task LinkAndSpecialEntryMetadataAreRejected(int attributes)
    {
        MakeZip(("entry", "target", attributes));
        var result = await ExtractZipTool.PreviewAsync(Scope(["input.zip"], "out"), new("input.zip", "out"));
        Assert.False(result.Success);
        Assert.Contains("LinkedEntry", result.Error);
    }

    [Fact]
    public async Task CreationRejectsOverlappingSelectionsAndBasenameCollisions()
    {
        Write("a/same.txt", "a"); Write("b/same.txt", "b");
        foreach (string[] selection in new[] { new[] { "a", "a/same.txt" }, new[] { "a/same.txt", "b/same.txt" } })
        {
            var result = await CreateZipTool.PreviewAsync(Scope(selection, "out.zip"), new(selection, "out.zip"));
            Assert.False(result.Success);
            Assert.Contains("EntryConflict", result.Error);
        }
    }

    [Fact]
    public async Task ExistingOutputsArePreviewedAsConflictsAndNeverOverwritten()
    {
        Write("notes.txt", "notes"); Write("existing.zip", "sentinel");
        CreateZipInput input = new(["notes.txt"], "existing.zip");
        using ArchivePreview preview = await CreatePreview(input);
        Assert.Equal(new[] { At("existing.zip") }, preview.Conflicts);
        Assert.Contains("OutputConflict", (await new CreateZipTool(new(preview)).ExecuteAsync(input)).Error);
        Assert.Equal("sentinel", File.ReadAllText(At("existing.zip")));
        MakeZip(("file.txt", "payload", 0)); Write("out/file.txt", "original");
        using ArchivePreview extract = await ExtractPreview();
        Assert.Single(extract.Conflicts);
        Assert.Contains("OutputConflict", (await new ExtractZipTool(new(extract)).ExecuteAsync(new("input.zip", "out"))).Error);
        Assert.Equal("original", File.ReadAllText(At("out/file.txt")));
        NoPartials();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConflictsCreatedAtPublicationDoNotDestroyUserFiles(bool extract)
    {
        MakeZip(("file.txt", "payload", 0)); Write("notes.txt", "notes");
        using ArchivePreview preview = extract ? await ExtractPreview() : await CreatePreview(new(["notes.txt"], "out.zip"));
        var progress = new InlineProgress(p => { if (p.Stage == "Publishing") Write(extract ? "out/sentinel" : "out.zip", "keep"); });
        if (extract)
            Assert.False((await new ExtractZipTool(new(preview), progress).ExecuteAsync(new("input.zip", "out"))).Success);
        else
            Assert.False((await new CreateZipTool(new(preview), progress).ExecuteAsync(new(["notes.txt"], "out.zip"))).Success);
        Assert.Equal("keep", File.ReadAllText(At(extract ? "out/sentinel" : "out.zip")));
        NoPartials();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDuringWritingCleansOnlyPartialOutputs(bool extract)
    {
        MakeZip(("file.txt", new string('x', 100000), 0)); Write("notes.txt", new string('x', 100000));
        using ArchivePreview preview = extract ? await ExtractPreview() : await CreatePreview(new(["notes.txt"], "out.zip"));
        using CancellationTokenSource canceled = new();
        var progress = new InlineProgress(p => { if (p.ProcessedBytes > 0) canceled.Cancel(); });
        if (extract)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ExtractZipTool(new(preview), progress).ExecuteAsync(new("input.zip", "out"), canceled.Token));
        else
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new CreateZipTool(new(preview), progress).ExecuteAsync(new(["notes.txt"], "out.zip"), canceled.Token));
        Assert.False(Directory.Exists(At("out"))); Assert.False(File.Exists(At("out.zip")));
        Assert.True(File.Exists(At("notes.txt"))); Assert.True(File.Exists(At("input.zip")));
        NoPartials();
    }

    [Fact]
    public async Task PreCanceledCallsAndPreviewCancellationDoNotWrite()
    {
        Write("notes.txt", "notes"); MakeZip(("file.txt", "data", 0));
        using CancellationTokenSource canceled = new(); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateZipTool.PreviewAsync(Scope(["notes.txt"], "out.zip"), new(["notes.txt"], "out.zip"), canceled.Token));
        using ArchivePreview preview = await ExtractPreview();
        var tool = new ExtractZipTool(new(preview));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tool.ExecuteAsync(new("input.zip", "out"), canceled.Token));
        Assert.True((await tool.ExecuteAsync(new("input.zip", "out"))).Success);
        using CancellationTokenSource during = new();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateZipTool.PreviewAsync(Scope(["notes.txt"], "out.zip"), new(["notes.txt"], "out.zip"), during.Token,
            new InlineProgress(_ => during.Cancel())));
        NoPartials();
    }

    [Fact]
    public async Task ChangedSourcesAndAddedFilesRequireNewPreview()
    {
        Write("lesson/file.txt", "old");
        CreateZipInput input = new(["lesson"], "out.zip");
        using (ArchivePreview preview = await CreatePreview(input))
        {
            Write("lesson/file.txt", "new");
            Assert.Contains("SourceChanged", (await new CreateZipTool(new(preview)).ExecuteAsync(input)).Error);
        }
        using (ArchivePreview preview = await CreatePreview(input))
        {
            var progress = new InlineProgress(p => { if (p.Stage == "Publishing") Write("lesson/added.txt", "added"); });
            Assert.Contains("SourceChanged", (await new CreateZipTool(new(preview), progress).ExecuteAsync(input)).Error);
        }
        MakeZip(("a.txt", "old", 0));
        using ArchivePreview extract = await ExtractPreview();
        MakeZip(("a.txt", "new", 0));
        Assert.Contains("SourceChanged", (await new ExtractZipTool(new(extract)).ExecuteAsync(new("input.zip", "out"))).Error);
        NoPartials();
    }

    [Fact]
    public async Task ApprovalsAreBoundSingleUseExpiringAndDisposable()
    {
        MakeZip(("a.txt", "data", 0));
        var time = new ManualClock();
        using ArchivePreview preview = await ExtractPreview(clock: time);
        Assert.Equal("archive.extract_zip", new ExtractZipTool(new(preview)).Id);
        Assert.Contains("ApprovalMismatch", (await new ExtractZipTool(new(preview)).ExecuteAsync(new("input.zip", "different"))).Error);
        time.Advance(TimeSpan.FromMinutes(5));
        Assert.Contains("ApprovalExpired", (await new ExtractZipTool(new(preview)).ExecuteAsync(new("input.zip", "out"))).Error);
        using ArchivePreview fresh = await ExtractPreview();
        Assert.True((await new ExtractZipTool(new(fresh)).ExecuteAsync(new("input.zip", "out"))).Success);
        Assert.Contains("ApprovalConsumed", (await new ExtractZipTool(new(fresh)).ExecuteAsync(new("input.zip", "out"))).Error);
        using ArchivePreview disposed = await ExtractPreview(new("input.zip", "other")); disposed.Dispose();
        Assert.Contains("ApprovalConsumed", (await new ExtractZipTool(new(disposed)).ExecuteAsync(new("input.zip", "other"))).Error);
        Write("notes.txt", "notes"); CreateZipInput input = new(["notes.txt"], "out.zip");
        using ArchivePreview create = await CreatePreview(input);
        input.InputPaths[0] = "input.zip";
        Assert.Contains("ApprovalMismatch", (await new CreateZipTool(new(create)).ExecuteAsync(input)).Error);
    }

    [Fact]
    public async Task ExactApprovalWorkspaceBoundariesAndMalformedArgumentsAreEnforced()
    {
        Write("notes.txt", "notes"); Write("other.txt", "other");
        var scope = Scope(["notes.txt"], "out.zip");
        var denied = await CreateZipTool.PreviewAsync(scope, new(["other.txt"], "out.zip"));
        Assert.Contains("InputNotApproved", denied.Error);
        Assert.Contains("OutputNotApproved", (await CreateZipTool.PreviewAsync(scope, new(["notes.txt"], "other.zip"))).Error);
        Assert.ThrowsAny<Exception>(() => Scope([root + "-neighbor/file.txt"], "out.zip"));
        Assert.ThrowsAny<Exception>(() => Scope(["../escape.txt"], "out.zip"));
        Assert.ThrowsAny<Exception>(() => new ArchiveAccessScope(Path.GetPathRoot(root)!, [], []));
        Assert.False((await CreateZipTool.PreviewAsync(scope, new([], "out.zip"))).Success);
        Assert.False((await ExtractZipTool.PreviewAsync(scope, new("notes.txt", "out"))).Success);
        Assert.False((await CreateZipTool.PreviewAsync(scope, new(["notes.txt"], "out.zip", ["other.txt"]))).Success);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateZipInput>("{\"InputPaths\":[\"notes.txt\"],\"OutputPath\":\"out.zip\",\"Approved\":true}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ExtractZipInput>("{\"ArchivePath\":\"input.zip\"}"));
    }

    [Fact]
    public async Task SymlinkInputsAndOutputParentsAreRejected()
    {
        if (OperatingSystem.IsWindows()) return; // Windows junction/ACL checks require a qualified Windows device.
        Write("real/file.txt", "data");
        Directory.CreateSymbolicLink(At("linked"), At("real"));
        Assert.ThrowsAny<Exception>(() => Scope(["linked/file.txt"], "out.zip"));
        var result = await CreateZipTool.PreviewAsync(Scope(["real"], "out.zip"), new(["real"], "out.zip"));
        using ArchivePreview preview = result.Value!;
        File.CreateSymbolicLink(At("real/link.txt"), At("real/file.txt"));
        Assert.Contains("LinkedPath", (await new CreateZipTool(new(preview)).ExecuteAsync(new(["real"], "out.zip"))).Error);
        Assert.ThrowsAny<Exception>(() => Scope(["real/file.txt"], "linked/output.zip"));
        NoPartials();
    }

    [Theory]
    [InlineData(0)] // expanded file limit
    [InlineData(1)] // high ratio
    [InlineData(2)] // forged entry count before allocation
    [InlineData(3)] // ZIP64 sentinel
    [InlineData(4)] // unsupported encryption
    public async Task ArchiveBombLikeMetadataAndUnsupportedZipFeaturesAreRejected(int kind)
    {
        MakeZip(("file.txt", "x", 0));
        byte[] bytes = File.ReadAllBytes(At("input.zip"));
        int central = FindSignature(bytes, 0x02014b50), end = FindSignature(bytes, 0x06054b50);
        if (kind == 0) Put32(bytes, central + 24, (uint)ArchiveLimits.MaxFileBytes + 1);
        if (kind == 1) Put32(bytes, central + 24, 10 * 1024 * 1024);
        if (kind == 2) { Put16(bytes, end + 8, 4097); Put16(bytes, end + 10, 4097); }
        if (kind == 3) Put32(bytes, central + 24, uint.MaxValue);
        if (kind == 4) Put16(bytes, central + 8, 1);
        File.WriteAllBytes(At("input.zip"), bytes);
        var result = await ExtractZipTool.PreviewAsync(Scope(["input.zip"], "out"), new("input.zip", "out"));
        Assert.False(result.Success);
        Assert.Contains(kind <= 2 ? "ResourceLimit" : "UnsupportedZip", result.Error);
        Assert.False(Directory.Exists(At("out")));
    }

    [Fact]
    public async Task ActualEntryLimitAndHighCompressionAreRejected()
    {
        using (FileStream file = File.Create(At("input.zip")))
        using (Zip zip = new(file, ZipArchiveMode.Create))
            for (int i = 0; i <= ArchiveLimits.MaxEntries; i++) zip.CreateEntry($"file{i}.txt");
        Assert.Contains("ResourceLimit", (await ExtractZipTool.PreviewAsync(Scope(["input.zip"], "out"), new("input.zip", "out"))).Error);
        using (FileStream file = File.Create(At("input.zip")))
        using (Zip zip = new(file, ZipArchiveMode.Create))
        using (Stream entry = zip.CreateEntry("huge.txt", CompressionLevel.Optimal).Open()) entry.Write(new byte[2 * 1024 * 1024]);
        Assert.Contains("ResourceLimit", (await ExtractZipTool.PreviewAsync(Scope(["input.zip"], "out"), new("input.zip", "out"))).Error);
    }

    [Fact]
    public async Task OrdinaryDeflatedZipWithCommentAndUtf8NamesExtracts()
    {
        using (FileStream file = File.Create(At("input.zip")))
        using (Zip zip = new(file, ZipArchiveMode.Create))
        {
            zip.Comment = "Synthetic local fixture";
            using Stream entry = zip.CreateEntry("aralin/édukasyon.txt", CompressionLevel.Optimal).Open();
            entry.Write(Encoding.UTF8.GetBytes("Teacher-selected synthetic lesson"));
        }
        using ArchivePreview preview = await ExtractPreview();
        var result = await new ExtractZipTool(new(preview)).ExecuteAsync(new("input.zip", "out"));
        Assert.True(result.Success, result.Error);
        Assert.Equal("Teacher-selected synthetic lesson", File.ReadAllText(At("out/aralin/édukasyon.txt")));
    }

    [Fact]
    public async Task TotalExpandedBudgetAndTreeDepthAreEnforced()
    {
        MakeZip(("a", "x", 0), ("b", "x", 0), ("c", "x", 0));
        byte[] bytes = File.ReadAllBytes(At("input.zip"));
        for (int i = 0; i <= bytes.Length - 46; i++)
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i, 4)) == 0x02014b50)
            {
                Put32(bytes, i + 20, 100 * 1024 * 1024);
                Put32(bytes, i + 24, 100 * 1024 * 1024);
            }
        File.WriteAllBytes(At("input.zip"), bytes);
        Assert.Contains("ResourceLimit", (await ExtractZipTool.PreviewAsync(Scope(["input.zip"], "out"), new("input.zip", "out"))).Error);
        MakeZip((string.Join('/', Enumerable.Repeat("folder", ArchiveLimits.MaxDepth)) + "/file.txt", "x", 0));
        Assert.False((await ExtractZipTool.PreviewAsync(Scope(["input.zip"], "out"), new("input.zip", "out"))).Success);
    }

    [Fact]
    public async Task HugeInputAndArchiveFilesAreRejectedWithoutReadingTheirPayloads()
    {
        using (FileStream file = File.Create(At("huge.txt"))) file.SetLength(ArchiveLimits.MaxFileBytes + 1);
        Assert.Contains("ResourceLimit", (await CreateZipTool.PreviewAsync(Scope(["huge.txt"], "out.zip"), new(["huge.txt"], "out.zip"))).Error);
        using (FileStream file = File.Create(At("input.zip"))) file.SetLength(ArchiveLimits.MaxArchiveBytes + 1);
        Assert.Contains("ResourceLimit", (await ExtractZipTool.PreviewAsync(Scope(["input.zip"], "out"), new("input.zip", "out"))).Error);
        NoPartials();
    }

    [Fact]
    public async Task ZipReplacedAfterExtractionAndCallbackFailuresCleanTheStagedTree()
    {
        MakeZip(("a", "old", 0));
        using ArchivePreview preview = await ExtractPreview();
        var progress = new InlineProgress(p =>
        {
            if (p.Stage != "Publishing") return;
            File.Move(At("input.zip"), At("old.zip"));
            MakeZip(("a", "new", 0));
        });
        var result = await new ExtractZipTool(new(preview), progress).ExecuteAsync(new("input.zip", "out"));
        Assert.False(result.Success);
        Assert.False(Directory.Exists(At("out")));
        NoPartials();
        using ArchivePreview fresh = await ExtractPreview();
        var broken = new InlineProgress(p => { if (p.ProcessedBytes > 0) throw new InvalidOperationException("host callback failed"); });
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ExtractZipTool(new(fresh), broken).ExecuteAsync(new("input.zip", "out")));
        NoPartials();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CorruptPayloadAndFalseExpandedSizesAreCleanedDuringExtraction(bool falseSize)
    {
        MakeZip(("file.txt", "payload", 0));
        byte[] bytes = File.ReadAllBytes(At("input.zip"));
        if (falseSize) Put32(bytes, FindSignature(bytes, 0x02014b50) + 24, 1);
        else bytes[30 + Encoding.UTF8.GetByteCount("file.txt")] ^= 1;
        File.WriteAllBytes(At("input.zip"), bytes);
        using ArchivePreview preview = await ExtractPreview();
        var result = await new ExtractZipTool(new(preview)).ExecuteAsync(new("input.zip", "out"));
        Assert.False(result.Success);
        Assert.False(Directory.Exists(At("out")));
        NoPartials();
    }

    [Theory]
    [InlineData("not a zip")]
    [InlineData("PK\u0003\u0004truncated")]
    public async Task MalformedArchivesReturnPrivateErrors(string data)
    {
        Write("input.zip", data);
        var result = await ExtractZipTool.PreviewAsync(Scope(["input.zip"], "out"), new("input.zip", "out"));
        Assert.False(result.Success); Assert.DoesNotContain(root, result.Error);
    }

    private static int FindSignature(byte[] bytes, uint signature)
    {
        for (int i = 0; i <= bytes.Length - 4; i++)
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i, 4)) == signature) return i;
        throw new InvalidOperationException();
    }
    private static void Put16(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, 2), value);
    private static void Put32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
    private sealed class InlineProgress(Action<ArchiveProgress> callback) : IProgress<ArchiveProgress>
    { public void Report(ArchiveProgress value) => callback(value); }
    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset time = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => time;
        public void Advance(TimeSpan interval) => time += interval;
    }
}
