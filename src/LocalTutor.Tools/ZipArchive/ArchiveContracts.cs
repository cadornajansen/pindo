using System.Text.Json;
using System.Text.Json.Serialization;
using LocalTutor.Core.Tools;
using LocalTutor.Tools.FileInspectionAndConversion;

namespace LocalTutor.Tools.ZipArchive;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateZipInput(
    [property: JsonRequired] string[] InputPaths,
    [property: JsonRequired] string OutputPath,
    string[]? ExcludedPaths = null) : ToolInput
{
    public override IReadOnlyList<string> Validate()
    {
        List<string> errors = [.. FileAccessScope.ValidatePath(OutputPath)];
        if (!string.Equals(Path.GetExtension(OutputPath), ".zip", StringComparison.OrdinalIgnoreCase))
            errors.Add("UnsupportedFormat: choose a .zip output.");
        if (InputPaths is null || InputPaths.Length is 0 or > ArchiveLimits.MaxEntries)
            errors.Add("InvalidInput: select between one and 4096 files or directories.");
        else foreach (string path in InputPaths) errors.AddRange(FileAccessScope.ValidatePath(path));
        if (ExcludedPaths is { Length: > ArchiveLimits.MaxEntries }) errors.Add("ResourceLimit: too many exclusions.");
        else foreach (string path in ExcludedPaths ?? []) errors.AddRange(FileAccessScope.ValidatePath(path));
        return errors;
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ExtractZipInput(
    [property: JsonRequired] string ArchivePath,
    [property: JsonRequired] string OutputDirectory) : ToolInput
{
    public override IReadOnlyList<string> Validate()
    {
        List<string> errors = [.. FileAccessScope.ValidatePath(ArchivePath), .. FileAccessScope.ValidatePath(OutputDirectory)];
        if (!string.Equals(Path.GetExtension(ArchivePath), ".zip", StringComparison.OrdinalIgnoreCase))
            errors.Add("UnsupportedFormat: select a .zip archive.");
        return errors;
    }
}

public static class ArchiveLimits
{
    public const int MaxEntries = 4096;
    public const int MaxDepth = 32;
    public const long MaxFileBytes = 128L * 1024 * 1024;
    public const long MaxTotalBytes = 256L * 1024 * 1024;
    public const long MaxArchiveBytes = 256L * 1024 * 1024;
    public const int MaxExpansionRatio = 200;
    public static TimeSpan OperationTimeout => TimeSpan.FromMinutes(2);
}

public sealed record ArchiveEntryPreview(string Name, bool IsDirectory, long Bytes);
public sealed record ArchiveProgress(string Stage, int CompletedEntries, int TotalEntries, long ProcessedBytes);
public sealed record CreatedZip(string ArchivePath, long ArchiveBytes, int EntryCount, long InputBytes);
public sealed record ExtractedZip(string OutputDirectory, IReadOnlyList<string> ExtractedPaths, int FileCount, int DirectoryCount, long ExpandedBytes);

/// <summary>Host state. Display every entry, exclusion, conflict and size before requesting explicit consent.</summary>
public sealed class ArchivePreview : IDisposable
{
    private int consumed;
    private readonly TimeProvider clock;
    internal ArchiveAccessScope Scope { get; }
    internal string InputSignature { get; }
    internal string ToolId { get; }
    internal ArchivePlan Plan { get; }
    public string OutputPath => Plan.Output;
    public IReadOnlyList<ArchiveEntryPreview> Entries { get; }
    public int EntryCount => Entries.Count;
    public long TotalInputBytes => Plan.TotalBytes;
    public IReadOnlyList<string> Exclusions { get; }
    public IReadOnlyList<string> Conflicts { get; }
    public DateTimeOffset ExpiresAt { get; }

    internal ArchivePreview(string id, ToolInput input, ArchiveAccessScope scope, ArchivePlan plan, TimeProvider clock)
    {
        ToolId = id; InputSignature = Signature(input); Scope = scope; Plan = plan; this.clock = clock;
        Entries = Array.AsReadOnly(plan.Entries.Select(e => new ArchiveEntryPreview(e.Name, e.IsDirectory, e.Bytes)).ToArray());
        Exclusions = Array.AsReadOnly(plan.Exclusions.ToArray());
        Conflicts = Array.AsReadOnly(plan.Conflicts.ToArray());
        ExpiresAt = clock.GetUtcNow().AddMinutes(5);
    }

    internal void Consume(string id, ToolInput input)
    {
        if (ToolId != id || InputSignature != Signature(input)) throw new ArchiveException("ApprovalMismatch");
        if (Interlocked.Exchange(ref consumed, 1) != 0) throw new ArchiveException("ApprovalConsumed");
        if (clock.GetUtcNow() >= ExpiresAt) throw new ArchiveException("ApprovalExpired");
        if (Conflicts.Count != 0) throw new ArchiveException("OutputConflict");
    }

    internal static string Signature(ToolInput input) => JsonSerializer.Serialize(input, input.GetType());
    public void Dispose() => Interlocked.Exchange(ref consumed, 1);
}

/// <summary>Construct only in the trusted host after the user explicitly approves the displayed preview. Never deserialize from model output.</summary>
public sealed class ArchiveApproval(ArchivePreview preview)
{
    internal ArchivePreview Preview { get; } = preview ?? throw new ArgumentNullException(nameof(preview));
}

internal sealed class ArchiveException(string code) : Exception(code + ": prepare a fresh preview or choose a safe local selection.");
internal sealed record PlannedEntry(string Name, bool IsDirectory, long Bytes, string? Source = null, byte[]? Hash = null, uint Crc = 0);
internal sealed record ArchivePlan(string Output, PlannedEntry[] Entries, long TotalBytes, string[] Exclusions, string[] Conflicts, byte[]? ArchiveHash = null);
