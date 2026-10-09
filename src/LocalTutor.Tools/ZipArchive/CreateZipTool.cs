using System.IO.Compression;
using System.Security.Cryptography;
using LocalTutor.Core.Tools;
using Zip = System.IO.Compression.ZipArchive;

namespace LocalTutor.Tools.ZipArchive;

public sealed class CreateZipTool(ArchiveApproval approval, IProgress<ArchiveProgress>? progress = null)
    : LocalTool<CreateZipInput, CreatedZip>
{
    public override string Id => "archive.create_zip";
    public override string Description => "Create one local ZIP from explicitly selected files and folders after reviewing and approving its entries, sizes, exclusions and new output path. Preserve every original; never overwrite.";

    public static Task<ToolResult<ArchivePreview>> PreviewAsync(ArchiveAccessScope scope, CreateZipInput input,
        CancellationToken cancellationToken = default, IProgress<ArchiveProgress>? progress = null, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        var errors = input.Validate();
        if (errors.Count > 0) return Task.FromResult(new ToolResult<ArchivePreview>(false, null, string.Join("; ", errors)));
        // Freeze mutable model argument arrays before sending work to a background thread.
        input = input with { InputPaths = input.InputPaths.ToArray(), ExcludedPaths = input.ExcludedPaths?.ToArray() };
        return ArchiveIO.RunAsync(async token => new ArchivePreview("archive.create_zip", input, scope,
            await PlanAsync(scope, input, token, progress), clock ?? TimeProvider.System), cancellationToken);
    }

    protected override Task<ToolResult<CreatedZip>> ExecuteValidatedAsync(CreateZipInput input, CancellationToken cancellationToken)
    {
        input = input with { InputPaths = input.InputPaths.ToArray(), ExcludedPaths = input.ExcludedPaths?.ToArray() };
        return ArchiveIO.RunAsync(token => CreateAsync(input, token), cancellationToken);
    }

    private async Task<CreatedZip> CreateAsync(CreateZipInput input, CancellationToken token)
    {
        ArchivePreview preview = approval.Preview;
        preview.Consume(Id, input);
        string? temporary = null;
        try
        {
            ArchivePlan current = await PlanAsync(preview.Scope, input, token);
            if (!ArchiveIO.SamePlan(preview.Plan, current)) throw new ArchiveException("SourceChanged");
            string output = preview.Scope.Output(input.OutputPath);
            progress?.Report(new("Writing", 0, current.Entries.Length, 0));
            output = preview.Scope.Output(input.OutputPath);
            token.ThrowIfCancellationRequested();
            string path = Path.Combine(Path.GetDirectoryName(output)!, ".localtutor-zip-" + Guid.NewGuid().ToString("N") + ".partial");
            long total = 0;
            int completed = 0;
            await using (FileStream target = new(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.Asynchronous))
            {
                temporary = path;
                using (Zip archive = new(target, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (PlannedEntry item in current.Entries)
                    {
                        token.ThrowIfCancellationRequested();
                        // Stored ZIPs avoid producing highly compressible archives that our extraction limits would reject.
                        var entry = archive.CreateEntry(item.Name, CompressionLevel.NoCompression);
                        entry.ExternalAttributes = item.IsDirectory ? (int)FileAttributes.Directory : 0;
                        if (!item.IsDirectory)
                        {
                            await using FileStream source = ArchiveIO.OpenRead(item.Source!, ArchiveLimits.MaxFileBytes);
                            await using Stream destination = entry.Open();
                            var copied = await ArchiveIO.CopyAsync(source, destination, item.Bytes, token,
                                bytes => progress?.Report(new("Writing", completed, current.Entries.Length, total + bytes)));
                            if (copied.Bytes != item.Bytes || !CryptographicOperations.FixedTimeEquals(item.Hash!, copied.Hash))
                                throw new ArchiveException("SourceChanged");
                            total += copied.Bytes;
                        }
                        completed++;
                        progress?.Report(new("Writing", completed, current.Entries.Length, total));
                    }
                }
                await target.FlushAsync(token);
                if (target.Length > ArchiveLimits.MaxArchiveBytes) throw new ArchiveException("ResourceLimit");
                uint[] crcs = ArchiveIO.InspectCentralDirectory(target, token);
                using Zip verified = new(target, ZipArchiveMode.Read, leaveOpen: true);
                ArchivePlan written = ArchiveIO.ReadEntries(verified, crcs, output, [], token);
                if (written.TotalBytes != current.TotalBytes || written.Entries.Length != current.Entries.Length ||
                    written.Entries.Zip(current.Entries).Any(pair => pair.First.Name != pair.Second.Name || pair.First.Crc != pair.Second.Crc))
                    throw new ArchiveException("InvalidOutput");
            }
            progress?.Report(new("Publishing", completed, current.Entries.Length, total));
            // Re-enumerate to catch added/removed files and same-size edits after preview and during copying.
            // The temporary sibling is an internal exclusion; it is never included in the public selection.
            ArchivePlan latest = await PlanAsync(preview.Scope, input, token, internalExclusion: temporary);
            if (!ArchiveIO.SamePlan(preview.Plan, latest)) throw new ArchiveException("SourceChanged");
            output = preview.Scope.Output(input.OutputPath);
            long size = new FileInfo(temporary).Length;
            token.ThrowIfCancellationRequested();
            File.Move(temporary, output, overwrite: false);
            temporary = null;
            return new(output, size, completed, total);
        }
        finally { ArchiveIO.Cleanup(temporary, directory: false, token); }
    }

    private static async Task<ArchivePlan> PlanAsync(ArchiveAccessScope scope, CreateZipInput input, CancellationToken token,
        IProgress<ArchiveProgress>? progress = null, string? internalExclusion = null)
    {
        string output = scope.Output(input.OutputPath, allowConflict: true);
        string[] selections = input.InputPaths.Select(path => scope.Input(path)).Order(StringComparer.Ordinal).ToArray();
        for (int i = 0; i < selections.Length; i++)
            for (int j = 0; j < i; j++)
                if (ArchiveAccessScope.Contains(selections[i], selections[j]) || ArchiveAccessScope.Contains(selections[j], selections[i]))
                    throw new ArchiveException("EntryConflict");
        if (selections.Any(s => s == output)) throw new ArchiveException("OutputConflict");
        List<string> exclusions = (input.ExcludedPaths ?? []).Select(scope.Canonicalize).Distinct(StringComparer.Ordinal).ToList();
        if (exclusions.Any(e => !selections.Any(s => Directory.Exists(s) && ArchiveAccessScope.Contains(s, e)) || selections.Contains(e)))
            throw new ArchiveException("InvalidExclusion");
        if (selections.Any(s => Directory.Exists(s) && ArchiveAccessScope.Contains(s, output))) exclusions.Add(output);
        exclusions = exclusions.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        List<PlannedEntry> entries = [];
        Dictionary<string, (string Spelling, bool Directory, bool Explicit)> tree = new(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (string source in selections) await VisitAsync(source, Path.GetFileName(source), 1);
        token.ThrowIfCancellationRequested();
        return new(output, entries.OrderBy(e => e.Name, StringComparer.Ordinal).ToArray(), total, exclusions.ToArray(),
            ArchiveAccessScope.Exists(output) ? [output] : []);

        async Task VisitAsync(string source, string name, int depth)
        {
            token.ThrowIfCancellationRequested();
            if (source == internalExclusion || exclusions.Any(e => ArchiveAccessScope.Contains(e, source))) return;
            ArchiveAccessScope.CheckPath(source);
            if (depth > ArchiveLimits.MaxDepth || entries.Count >= ArchiveLimits.MaxEntries) throw new ArchiveException("ResourceLimit");
            bool directory = Directory.Exists(source);
            string entryName = directory ? name + "/" : name;
            ArchiveAccessScope.EntryName(entryName, directory);
            ArchiveIO.AddToTree(tree, name, directory);
            if (directory)
            {
                entries.Add(new(entryName, true, 0, source));
                foreach (string child in Directory.EnumerateFileSystemEntries(source))
                    await VisitAsync(child, name + "/" + Path.GetFileName(child), depth + 1);
            }
            else
            {
                await using FileStream file = ArchiveIO.OpenRead(source, ArchiveLimits.MaxFileBytes);
                var read = await ArchiveIO.CopyAsync(file, null, Math.Min(ArchiveLimits.MaxFileBytes, ArchiveLimits.MaxTotalBytes - total), token);
                total += read.Bytes;
                entries.Add(new(entryName, false, read.Bytes, source, read.Hash, read.Crc));
            }
            progress?.Report(new("Inspecting", entries.Count, 0, total));
        }
    }
}
