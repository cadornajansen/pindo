using LocalTutor.Core.Tools;

namespace LocalTutor.Tools.FileOrganization;

public sealed class FindDuplicatesTool(OrganizationAccessScope scope, IProgress<OrganizationProgress>? progress = null)
    : LocalTool<FindDuplicatesInput, FoundDuplicates>
{
    private readonly OrganizationAccessScope scope = scope ?? throw new ArgumentNullException(nameof(scope));
    public override string Id => "file.find_duplicates";
    public override string Description => "Find exact duplicate bytes using size and SHA-256 in selected local lesson roots. Reports skipped files, follows no links, and deletes nothing.";

    protected override Task<ToolResult<FoundDuplicates>> ExecuteValidatedAsync(FindDuplicatesInput input, CancellationToken cancellationToken)
    {
        string[] roots = input.Roots.ToArray();
        return OrganizationIO.RunAsync<FoundDuplicates>(async token =>
        {
            string[] selected = roots.Select(scope.DuplicateRoot).ToArray();
            if (selected.Distinct(StringComparer.OrdinalIgnoreCase).Count() != selected.Length) throw new OrganizationException("DuplicateSelection");
            List<SkippedFile> skipped = [];
            List<(string Path, long Bytes)> files = [];
            HashSet<string> seen = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            Stack<(string Directory, int Depth)> pending = new(selected.Select(r => (r, 0)));
            long total = 0;
            int entries = 0;
            while (pending.TryPop(out var item))
            {
                token.ThrowIfCancellationRequested();
                if (!seen.Add(item.Directory)) continue;
                try
                {
                    scope.Canonicalize(item.Directory);
                    foreach (string path in OrganizationIO.Entries(item.Directory, token).Order(StringComparer.Ordinal))
                    {
                        if (++entries > OrganizationLimits.MaxEntries) throw new OrganizationException("ResourceLimit");
                        try
                        {
                            scope.Canonicalize(path);
                            FileAttributes attributes = File.GetAttributes(path);
                            if ((attributes & FileAttributes.Directory) != 0)
                            {
                                if (item.Depth >= OrganizationLimits.MaxDepth) skipped.Add(new(path, "DepthLimit"));
                                else pending.Push((path, item.Depth + 1));
                                continue;
                            }
                            if (!seen.Add(path)) continue;
                            OrganizationIO.CheckRegularFile(path);
                            long bytes = new FileInfo(path).Length;
                            if (bytes > OrganizationLimits.MaxFileBytes) { skipped.Add(new(path, "FileSizeLimit")); continue; }
                            if (files.Count >= OrganizationLimits.MaxFiles || bytes > OrganizationLimits.MaxTotalBytes - total)
                                throw new OrganizationException("ResourceLimit");
                            total += bytes;
                            files.Add((path, bytes));
                        }
                        catch (OrganizationException e) when (OrganizationIO.Code(e) != "ResourceLimit") { skipped.Add(new(path, OrganizationIO.Code(e))); }
                        catch (Exception e) when (OrganizationIO.IsFileError(e) && e is not OrganizationException) { skipped.Add(new(path, OrganizationIO.Code(e))); }
                    }
                }
                catch (OrganizationException e) when (OrganizationIO.Code(e) != "ResourceLimit") { skipped.Add(new(item.Directory, OrganizationIO.Code(e))); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { skipped.Add(new(item.Directory, "FileUnavailable")); }
            }
            List<(string Path, FileSnapshot File)> hashed = [];
            var candidates = files.GroupBy(f => f.Bytes).Where(g => g.Count() > 1).SelectMany(g => g).ToArray();
            foreach (var file in candidates)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    scope.Canonicalize(file.Path);
                    FileSnapshot snapshot = await OrganizationIO.SnapshotAsync(file.Path, token);
                    if (snapshot.Bytes != file.Bytes) throw new OrganizationException("SourceChanged");
                    hashed.Add((file.Path, snapshot));
                }
                catch (Exception e) when (OrganizationIO.IsFileError(e)) { skipped.Add(new(file.Path, OrganizationIO.Code(e))); }
                progress?.Report(new("Hash", hashed.Count + skipped.Count, candidates.Length));
            }
            // Rehash duplicate candidates before reporting: a file changed during the scan must not remain in a group.
            List<DuplicateGroup> groups = [];
            foreach (var group in hashed.GroupBy(f => (f.File.Bytes, f.File.Sha256)).Where(g => g.Count() > 1))
            {
                List<string> verified = [];
                foreach (var file in group)
                {
                    try
                    {
                        scope.Canonicalize(file.Path);
                        if (await OrganizationIO.SnapshotAsync(file.Path, token) != file.File) throw new OrganizationException("SourceChanged");
                        verified.Add(file.Path);
                    }
                    catch (Exception e) when (OrganizationIO.IsFileError(e)) { skipped.Add(new(file.Path, OrganizationIO.Code(e))); }
                }
                if (verified.Count > 1) groups.Add(new(group.Key.Bytes, group.Key.Sha256, Array.AsReadOnly(verified.Order(StringComparer.Ordinal).ToArray())));
            }
            token.ThrowIfCancellationRequested();
            return new(true, new(groups.AsReadOnly(), skipped.AsReadOnly(), files.Count));
        }, cancellationToken);
    }
}
