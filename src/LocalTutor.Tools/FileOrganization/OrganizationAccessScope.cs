using System.Text;
using LocalTutor.Tools.FileInspectionAndConversion;

namespace LocalTutor.Tools.FileOrganization;

/// <summary>Trusted host selections. Files and destination folders are exact; duplicate roots permit bounded read-only descendants.</summary>
public sealed class OrganizationAccessScope
{
    private readonly string workspace;
    private readonly HashSet<string> files;
    private readonly HashSet<string> destinations;
    private readonly HashSet<string> roots;
    private static StringComparer Comparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static StringComparison Comparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public OrganizationAccessScope(string workspacePath, IEnumerable<string> approvedFiles,
        IEnumerable<string> approvedDestinationDirectories, IEnumerable<string>? approvedDuplicateRoots = null)
    {
        if (ValidatePaths([workspacePath], 1).Count != 0 || !Path.IsPathFullyQualified(workspacePath))
            throw new ArgumentException("Select an absolute local workspace.");
        workspace = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspacePath));
        if (workspace == Path.GetPathRoot(workspace)) throw new ArgumentException("A volume root cannot be the workspace.");
        CheckPath(workspace);
        if (!Directory.Exists(workspace)) throw new ArgumentException("Select an existing workspace.");
        files = Select(approvedFiles, OrganizationLimits.MaxFiles);
        destinations = Select(approvedDestinationDirectories, 16);
        roots = Select(approvedDuplicateRoots ?? [], 8);
    }

    private HashSet<string> Select(IEnumerable<string> paths, int maximum)
    {
        ArgumentNullException.ThrowIfNull(paths);
        string[] bounded = paths.Take(maximum + 1).ToArray();
        if (bounded.Length > maximum) throw new OrganizationException("ResourceLimit");
        return new(bounded.Select(Canonicalize), Comparer);
    }

    internal static IReadOnlyList<string> ValidatePaths(string[]? paths, int maximum)
    {
        if (paths is null || paths.Length == 0 || paths.Length > maximum) return ["InvalidInput: choose a bounded nonempty selection."];
        List<string> errors = [];
        foreach (string? path in paths)
        {
            if (FileAccessScope.ValidatePath(path).Count != 0 || path is null ||
                !OperatingSystem.IsWindows() && path.Contains('\\') || Path.IsPathRooted(path) && !Path.IsPathFullyQualified(path))
            { errors.Add("InvalidPath: select an unambiguous local path."); continue; }
            foreach (string part in path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (OperatingSystem.IsWindows() && part.Length == 2 && part[1] == ':') continue;
                string stem = part.Split('.')[0].ToUpperInvariant();
                if (part.Length > 255 || part.Contains('~') || !part.IsNormalized(NormalizationForm.FormC) ||
                    stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && "¹²³".Contains(stem[3]))
                    errors.Add("InvalidPath: Windows aliases and ambiguous names are unsupported.");
            }
        }
        return errors;
    }

    internal string Canonicalize(string path)
    {
        if (ValidatePaths([path], 1).Count != 0) throw new OrganizationException("InvalidPath");
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path, workspace));
        if (!Contains(workspace, full)) throw new OrganizationException("PathOutsideWorkspace");
        CheckPath(full);
        return full;
    }

    internal string SelectedFile(string path)
    {
        string full = Canonicalize(path);
        if (!files.Contains(full)) throw new OrganizationException("InputNotApproved");
        if (Directory.Exists(full)) throw new OrganizationException("FileSelectionRequired");
        return full;
    }

    internal string Destination(string path)
    {
        string full = Canonicalize(path);
        if (!destinations.Contains(full)) throw new OrganizationException("DestinationNotApproved");
        if (!Directory.Exists(full)) throw new OrganizationException("DestinationMissing");
        return full;
    }

    internal string DuplicateRoot(string path)
    {
        string full = Canonicalize(path);
        if (!roots.Contains(full)) throw new OrganizationException("RootNotApproved");
        if (!Directory.Exists(full)) throw new OrganizationException("RootMissing");
        return full;
    }

    internal static bool Contains(string parent, string child) => child.Equals(parent, Comparison) || child.StartsWith(parent + Path.DirectorySeparatorChar, Comparison);

    internal static void CheckPath(string full)
    {
        if (OperatingSystem.IsWindows() && new DriveInfo(Path.GetPathRoot(full)!).DriveType == DriveType.Network)
            throw new OrganizationException("NetworkPath");
        string[] protectedRoots = OperatingSystem.IsWindows()
            ? [Environment.GetFolderPath(Environment.SpecialFolder.Windows), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)]
            : ["/etc", "/proc", "/sys", "/dev", "/usr", "/bin", "/sbin", "/boot", "/lib", "/lib64", "/root"];
        if (protectedRoots.Where(r => r.Length > 0).Any(r => Contains(Path.TrimEndingDirectorySeparator(r), full)) ||
            full.Split(Path.DirectorySeparatorChar).Any(p => p.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                p.Equals(".ssh", StringComparison.OrdinalIgnoreCase) || p.Equals(".aws", StringComparison.OrdinalIgnoreCase)))
            throw new OrganizationException("ProtectedPath");
        for (string? current = full; current is not null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0)
                    throw new OrganizationException("LinkedOrSpecialPath");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }
}
