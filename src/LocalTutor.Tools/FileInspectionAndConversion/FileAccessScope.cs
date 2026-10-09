namespace LocalTutor.Tools.FileInspectionAndConversion;

/// <summary>
/// Constructed by the trusted host after user approval, never deserialized from model arguments.
/// Approvals grant exact paths inside one selected workspace; outputs must be new files.
/// </summary>
public sealed class FileAccessScope
{
    private readonly string workspace;
    private readonly HashSet<string> inputs;
    private readonly HashSet<string> outputs;
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public FileAccessScope(string workspacePath, IEnumerable<string> approvedInputs, IEnumerable<string>? approvedOutputs = null)
    {
        ArgumentNullException.ThrowIfNull(approvedInputs);
        if (ValidatePath(workspacePath).Count != 0 || !Path.IsPathFullyQualified(workspacePath))
            throw new ArgumentException("An absolute user-selected workspace is required.");
        workspace = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspacePath));
        if (workspace == Path.GetPathRoot(workspace)) throw new ArgumentException("A filesystem root cannot be the workspace.");
        CheckPath(workspace);
        if (!Directory.Exists(workspace)) throw new ArgumentException("The selected workspace must already exist.");
        inputs = new HashSet<string>(approvedInputs.Select(Canonicalize), PathComparer);
        outputs = new HashSet<string>((approvedOutputs ?? []).Select(Canonicalize), PathComparer);
    }

    public static IReadOnlyList<string> ValidatePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 1024) return ["A local path of at most 1024 characters is required."];
        if (path.Any(char.IsControl) || path.StartsWith('\\') || path.StartsWith("//", StringComparison.Ordinal))
            return ["Network, device, and control-character paths are unsupported."];
        string[] parts = path.Split(['/', '\\']);
        if (parts.Any(p => p is ".." or "." || p.EndsWith(' ') || p.EndsWith('.')))
            return ["Traversal and ambiguous path components are unsupported."];
        foreach (string part in parts)
        {
            string name = part.Split('.')[0].ToUpperInvariant();
            if (name is "CON" or "PRN" or "AUX" or "NUL" ||
                name.Length == 4 && (name.StartsWith("COM", StringComparison.Ordinal) || name.StartsWith("LPT", StringComparison.Ordinal)) && name[3] is >= '0' and <= '9')
                return ["Reserved device names are unsupported."];
        }
        // Reject alternate data streams on Windows and Windows-style paths on other systems.
        if (path.IndexOf(':', OperatingSystem.IsWindows() && path.Length >= 2 && path[1] == ':' ? 2 : 0) >= 0)
            return ["Alternate streams and nonlocal paths are unsupported."];
        if (path.IndexOfAny(['*', '?', '"', '<', '>', '|']) >= 0) return ["Wildcard and invalid path characters are unsupported."];
        return [];
    }

    internal string ResolveInput(string path)
    {
        string full = Canonicalize(path);
        if (!inputs.Contains(full)) throw new FileToolException("InputNotApproved: select and approve this exact local file.");
        CheckPath(full);
        if (!File.Exists(full)) throw new FileToolException("InputMissing: the approved file does not exist or is not a regular file.");
        FileAttributes attributes = File.GetAttributes(full);
        if ((attributes & (FileAttributes.Directory | FileAttributes.Device)) != 0)
            throw new FileToolException("UnsupportedInput: a regular file is required.");
        return full;
    }

    internal string ResolveOutput(string path)
    {
        string full = Canonicalize(path);
        if (!outputs.Contains(full)) throw new FileToolException("OutputNotApproved: approve the exact new output name.");
        CheckPath(full);
        if (File.Exists(full) || Directory.Exists(full)) throw new FileToolException("OutputConflict: choose a new filename; overwrite is disabled.");
        if (!Directory.Exists(Path.GetDirectoryName(full))) throw new FileToolException("OutputDirectoryMissing: select an existing output directory.");
        return full;
    }

    private string Canonicalize(string path)
    {
        if (ValidatePath(path).Count != 0) throw new FileToolException("InvalidPath: select an unambiguous local path.");
        string full = Path.GetFullPath(path, workspace);
        if (!full.StartsWith(workspace + Path.DirectorySeparatorChar, PathComparison))
            throw new FileToolException("PathOutsideWorkspace: the file must be inside the approved workspace.");
        return full;
    }

    private static void CheckPath(string full)
    {
        string[] protectedRoots = OperatingSystem.IsWindows()
            ? [Environment.GetFolderPath(Environment.SpecialFolder.Windows), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)]
            : ["/etc", "/proc", "/sys", "/dev", "/usr", "/bin", "/sbin", "/boot", "/lib", "/lib64", "/root"];
        foreach (string root in protectedRoots.Where(r => !string.IsNullOrEmpty(r)))
            if (full.Equals(root, PathComparison) || full.StartsWith(root + Path.DirectorySeparatorChar, PathComparison))
                throw new FileToolException("ProtectedPath: system locations cannot be selected.");
        for (string? current = full; current is not null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new FileToolException("LinkedPath: symbolic links and reparse points are unsupported.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }
}
