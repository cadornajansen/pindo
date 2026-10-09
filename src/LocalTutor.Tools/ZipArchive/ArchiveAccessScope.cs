using System.Text;
using LocalTutor.Tools.FileInspectionAndConversion;

namespace LocalTutor.Tools.ZipArchive;

/// <summary>Trusted exact selections inside one user-approved workspace. Selecting a directory approves its ordinary descendants.</summary>
public sealed class ArchiveAccessScope
{
    private readonly string workspace;
    private readonly HashSet<string> inputs;
    private readonly HashSet<string> outputs;
    private static StringComparison Comparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static StringComparer Comparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public ArchiveAccessScope(string workspacePath, IEnumerable<string> approvedInputs, IEnumerable<string> approvedOutputs)
    {
        ArgumentNullException.ThrowIfNull(approvedInputs);
        ArgumentNullException.ThrowIfNull(approvedOutputs);
        ValidateLocal(workspacePath);
        if (!Path.IsPathFullyQualified(workspacePath)) throw new ArgumentException("Select an absolute local workspace.");
        workspace = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspacePath));
        if (workspace == Path.GetPathRoot(workspace)) throw new ArgumentException("A volume root cannot be approved.");
        CheckPath(workspace);
        if (!Directory.Exists(workspace)) throw new ArgumentException("The workspace must exist.");
        inputs = new(approvedInputs.Select(Canonicalize), Comparer);
        outputs = new(approvedOutputs.Select(Canonicalize), Comparer);
        foreach (string path in inputs.Concat(outputs)) CheckPath(path);
    }

    internal string Canonicalize(string path)
    {
        ValidateLocal(path);
        string full = Path.GetFullPath(path, workspace);
        if (!full.StartsWith(workspace + Path.DirectorySeparatorChar, Comparison)) throw new ArchiveException("PathOutsideWorkspace");
        return full;
    }

    internal string Input(string path, bool fileOnly = false)
    {
        string full = Canonicalize(path);
        if (!inputs.Contains(full)) throw new ArchiveException("InputNotApproved");
        CheckPath(full);
        FileAttributes attributes = File.GetAttributes(full);
        if ((attributes & FileAttributes.Device) != 0 || fileOnly && (attributes & FileAttributes.Directory) != 0)
            throw new ArchiveException("UnsupportedInput");
        return full;
    }

    internal string Output(string path, bool allowConflict = false)
    {
        string full = Canonicalize(path);
        if (!outputs.Contains(full)) throw new ArchiveException("OutputNotApproved");
        CheckPath(full);
        if (!allowConflict && Exists(full)) throw new ArchiveException("OutputConflict");
        if (!Directory.Exists(Path.GetDirectoryName(full))) throw new ArchiveException("OutputParentMissing");
        return full;
    }

    internal static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
    internal static bool Contains(string parent, string child) => child.Equals(parent, Comparison) || child.StartsWith(parent + Path.DirectorySeparatorChar, Comparison);

    private static void ValidateLocal(string path)
    {
        if (FileAccessScope.ValidatePath(path).Count != 0 ||
            !OperatingSystem.IsWindows() && path.Contains('\\') ||
            Path.IsPathRooted(path) && !Path.IsPathFullyQualified(path)) throw new ArchiveException("InvalidPath");
        // Apply Windows component rules on every platform, including superscript device aliases and short-name aliases.
        foreach (string part in path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries))
            if (!(part.Length == 2 && part[1] == ':' && OperatingSystem.IsWindows())) ValidateComponent(part);
    }

    internal static void ValidateComponent(string name)
    {
        string stem = name.Split('.')[0].ToUpperInvariant();
        if (name.Length is 0 or > 255 || name is "." or ".." || name.EndsWith('.') || name.EndsWith(' ') ||
            name.Any(char.IsControl) || name.IndexOfAny([':', '\\', '/', '*', '?', '"', '<', '>', '|', '~']) >= 0 ||
            !name.IsNormalized(NormalizationForm.FormC) || stem is "CON" or "PRN" or "AUX" or "NUL" ||
            stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
            "0123456789¹²³".Contains(stem[3])) throw new ArchiveException("UnsafeEntryName");
    }

    internal static string EntryName(string name, bool directory)
    {
        if (name.Length > 1024 || name.StartsWith('/') || name.StartsWith('\\') || name.Contains('\\')) throw new ArchiveException("UnsafeEntryName");
        string trimmed = directory ? name.TrimEnd('/') : name;
        string[] parts = trimmed.Split('/');
        if (parts.Length > ArchiveLimits.MaxDepth || directory && name != trimmed + "/") throw new ArchiveException("UnsafeEntryName");
        foreach (string part in parts) ValidateComponent(part);
        return trimmed;
    }

    internal static void CheckPath(string full)
    {
        if (OperatingSystem.IsWindows() && new DriveInfo(Path.GetPathRoot(full)!).DriveType == DriveType.Network)
            throw new ArchiveException("NetworkPath");
        string[] protectedRoots = OperatingSystem.IsWindows()
            ? [Environment.GetFolderPath(Environment.SpecialFolder.Windows), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)]
            : ["/etc", "/proc", "/sys", "/dev", "/usr", "/bin", "/sbin", "/boot", "/lib", "/lib64", "/root"];
        foreach (string root in protectedRoots.Where(r => r.Length > 0))
            if (Contains(Path.TrimEndingDirectorySeparator(root), full)) throw new ArchiveException("ProtectedPath");
        for (string? current = full; current is not null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0) throw new ArchiveException("LinkedPath");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }
}
