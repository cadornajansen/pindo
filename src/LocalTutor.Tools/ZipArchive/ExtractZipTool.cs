using System.IO.Compression;
using LocalTutor.Core.Tools;
using Zip = System.IO.Compression.ZipArchive;

namespace LocalTutor.Tools.ZipArchive;

public sealed class ExtractZipTool(ArchiveApproval approval, IProgress<ArchiveProgress>? progress = null)
    : LocalTool<ExtractZipInput, ExtractedZip>
{
    public override string Id => "archive.extract_zip";
    public override string Description => "Inspect one local ZIP, then extract its explicitly approved preview tree into a new selected directory. Reject unsafe names, links, collisions and excessive expansion. Never overwrite existing files or directories.";

    public static Task<ToolResult<ArchivePreview>> PreviewAsync(ArchiveAccessScope scope, ExtractZipInput input,
        CancellationToken cancellationToken = default, IProgress<ArchiveProgress>? progress = null, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        var errors = input.Validate();
        if (errors.Count > 0) return Task.FromResult(new ToolResult<ArchivePreview>(false, null, string.Join("; ", errors)));
        return ArchiveIO.RunAsync(async token =>
        {
            progress?.Report(new("Inspecting", 0, 0, 0));
            ArchivePlan plan = await PlanAsync(scope, input, token);
            token.ThrowIfCancellationRequested();
            return new ArchivePreview("archive.extract_zip", input, scope, plan, clock ?? TimeProvider.System);
        }, cancellationToken);
    }

    protected override Task<ToolResult<ExtractedZip>> ExecuteValidatedAsync(ExtractZipInput input, CancellationToken cancellationToken) =>
        ArchiveIO.RunAsync(token => ExtractAsync(input, token), cancellationToken);

    private async Task<ExtractedZip> ExtractAsync(ExtractZipInput input, CancellationToken token)
    {
        ArchivePreview preview = approval.Preview;
        preview.Consume(Id, input);
        string? temporary = null;
        try
        {
            string sourcePath = preview.Scope.Input(input.ArchivePath, fileOnly: true);
            await using FileStream source = ArchiveIO.OpenRead(sourcePath, ArchiveLimits.MaxArchiveBytes);
            ArchivePlan current = await PlanAsync(preview.Scope, input, token, source);
            if (!ArchiveIO.SamePlan(preview.Plan, current)) throw new ArchiveException("SourceChanged");
            string output = preview.Scope.Output(input.OutputDirectory);
            progress?.Report(new("Extracting", 0, current.Entries.Length, 0));
            output = preview.Scope.Output(input.OutputDirectory);
            token.ThrowIfCancellationRequested();
            string stage = Path.Combine(Path.GetDirectoryName(output)!, ".localtutor-zip-" + Guid.NewGuid().ToString("N") + ".partial");
            Directory.CreateDirectory(stage);
            temporary = stage;
            source.Position = 0;
            using (Zip archive = new(source, ZipArchiveMode.Read, leaveOpen: true))
            {
                int completed = 0;
                long total = 0;
                foreach (PlannedEntry item in current.Entries)
                {
                    token.ThrowIfCancellationRequested();
                    string destination = Destination(stage, item.Name, item.IsDirectory);
                    ArchiveAccessScope.CheckPath(destination);
                    if (item.IsDirectory) Directory.CreateDirectory(destination);
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                        // Checked immediately before opening; never create links or apply entry permissions/attributes.
                        ArchiveAccessScope.CheckPath(destination);
                        await using FileStream file = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous);
                        await using Stream data = archive.GetEntry(item.Name)!.Open();
                        var copied = await ArchiveIO.CopyAsync(data, file, item.Bytes, token,
                            bytes => progress?.Report(new("Extracting", completed, current.Entries.Length, total + bytes)));
                        if (copied.Bytes != item.Bytes || copied.Crc != item.Crc) throw new ArchiveException("InvalidZip");
                        total += copied.Bytes;
                    }
                    completed++;
                    progress?.Report(new("Extracting", completed, current.Entries.Length, total));
                }
            }
            progress?.Report(new("Publishing", current.Entries.Length, current.Entries.Length, current.TotalBytes));
            ArchivePlan latest = await PlanAsync(preview.Scope, input, token);
            if (!ArchiveIO.SamePlan(current, latest)) throw new ArchiveException("SourceChanged");
            output = preview.Scope.Output(input.OutputDirectory);
            string[] paths = current.Entries.Select(e => Destination(output, e.Name, e.IsDirectory)).ToArray();
            token.ThrowIfCancellationRequested();
            // Publishing the entire new tree is the commit point; no existing directory is merged or overwritten.
            Directory.Move(temporary, output);
            temporary = null;
            return new(output, Array.AsReadOnly(paths), current.Entries.Count(e => !e.IsDirectory), current.Entries.Count(e => e.IsDirectory), current.TotalBytes);
        }
        finally { ArchiveIO.Cleanup(temporary, directory: true, token); }
    }

    private static async Task<ArchivePlan> PlanAsync(ArchiveAccessScope scope, ExtractZipInput input, CancellationToken token, FileStream? existing = null)
    {
        string sourcePath = scope.Input(input.ArchivePath, fileOnly: true);
        string output = scope.Output(input.OutputDirectory, allowConflict: true);
        FileStream stream = existing ?? ArchiveIO.OpenRead(sourcePath, ArchiveLimits.MaxArchiveBytes);
        try
        {
            if (stream.Length > ArchiveLimits.MaxArchiveBytes) throw new ArchiveException("ResourceLimit");
            stream.Position = 0;
            var read = await ArchiveIO.CopyAsync(stream, null, ArchiveLimits.MaxArchiveBytes, token);
            uint[] crcs = ArchiveIO.InspectCentralDirectory(stream, token);
            using Zip archive = new(stream, ZipArchiveMode.Read, leaveOpen: true);
            ArchivePlan plan = ArchiveIO.ReadEntries(archive, crcs, output, read.Hash, token);
            foreach (PlannedEntry item in plan.Entries) Destination(output, item.Name, item.IsDirectory);
            return plan;
        }
        finally { if (existing is null) await stream.DisposeAsync(); }
    }

    private static string Destination(string root, string name, bool directory)
    {
        string entry = ArchiveAccessScope.EntryName(name, directory);
        string full = Path.GetFullPath(Path.Combine(root, entry.Replace('/', Path.DirectorySeparatorChar)));
        if (!ArchiveAccessScope.Contains(root, full) || full == root || full.Length > 1024) throw new ArchiveException("UnsafeEntryName");
        return full;
    }
}
