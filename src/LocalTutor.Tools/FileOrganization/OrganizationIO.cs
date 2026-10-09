using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using LocalTutor.Core.Tools;
using Microsoft.Win32.SafeHandles;

namespace LocalTutor.Tools.FileOrganization;

internal static class OrganizationIO
{
    internal static async Task<ToolResult<T>> RunAsync<T>(Func<CancellationToken, Task<ToolResult<T>>> operation, CancellationToken userToken)
    {
        userToken.ThrowIfCancellationRequested();
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(userToken);
        deadline.CancelAfter(OrganizationLimits.OperationTimeout);
        try { return await Task.Run(() => operation(deadline.Token), deadline.Token); }
        catch (OperationCanceledException) when (!userToken.IsCancellationRequested) { return new(false, default, "TimedOut"); }
        catch (Exception e) when (IsFileError(e)) { return new(false, default, Code(e)); }
    }

    internal static bool IsFileError(Exception e) => e is OrganizationException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or DllNotFoundException or EntryPointNotFoundException;
    internal static string Code(Exception e) => e switch
    {
        OrganizationException => e.Message.Split(':')[0],
        DllNotFoundException or EntryPointNotFoundException => "NativeFileOperationUnavailable",
        _ => "FileUnavailable"
    };

    internal static async Task<FileSnapshot> SnapshotAsync(string path, CancellationToken token)
    {
        OrganizationAccessScope.CheckPath(path);
        FileInfo before = new(path);
        if (!before.Exists) throw new FileNotFoundException("The selected file is unavailable.");
        if ((before.Attributes & (FileAttributes.Directory | FileAttributes.Device | FileAttributes.ReparsePoint)) != 0)
            throw new OrganizationException("RegularFileRequired");
        CheckRegularFile(path);
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        long length = stream.Length;
        if (length > OrganizationLimits.MaxFileBytes) throw new OrganizationException("ResourceLimit");
        DateTime modified = before.LastWriteTimeUtc, created = before.CreationTimeUtc;
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[65536];
        long readBytes = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            int read = await stream.ReadAsync(buffer, token);
            if (read == 0) break;
            readBytes += read;
            if (readBytes > OrganizationLimits.MaxFileBytes) throw new OrganizationException("ResourceLimit");
            hash.AppendData(buffer, 0, read);
        }
        token.ThrowIfCancellationRequested();
        before.Refresh();
        OrganizationAccessScope.CheckPath(path);
        if (readBytes != length || before.Length != length || before.LastWriteTimeUtc != modified || before.CreationTimeUtc != created)
            throw new OrganizationException("SourceChanged");
        return new(length, modified, created, Convert.ToHexString(hash.GetHashAndReset()));
    }

    internal static void CheckRegularFile(string path)
    {
        if (!OperatingSystem.IsLinux()) return;
        // Probe without blocking on a FIFO and without following the final link before using the managed sharing checks.
        int descriptor = OpenNonBlocking(path, 0x800 | 0x20000 | 0x80000);
        if (descriptor < 0) throw new IOException("The local file could not be opened.");
        using SafeFileHandle handle = new((IntPtr)descriptor, ownsHandle: true);
        using FileStream probe = new(handle, FileAccess.Read, 1, isAsync: false);
        if (!probe.CanSeek) throw new OrganizationException("RegularFileRequired");
    }

    internal static IEnumerable<string> Entries(string directory, CancellationToken token)
    {
        int count = 0;
        foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
        {
            token.ThrowIfCancellationRequested();
            if (++count > OrganizationLimits.MaxEntries) throw new OrganizationException("ResourceLimit");
            yield return entry;
        }
    }

    internal static bool DestinationExists(string path, CancellationToken token) => Entries(Path.GetDirectoryName(path)!, token)
        .Any(p => string.Equals(Path.GetFileName(p), Path.GetFileName(path), StringComparison.OrdinalIgnoreCase));

    internal static void CheckVolume(string source, string destination)
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux()) throw new OrganizationException("UnsupportedMovePlatform");
        string Volume(string path) => OperatingSystem.IsWindows() ? Path.GetPathRoot(path)! :
            DriveInfo.GetDrives().Select(d => Path.TrimEndingDirectorySeparator(d.Name))
                .Where(d => d == "/" || OrganizationAccessScope.Contains(d, path)).OrderByDescending(d => d.Length).First();
        if (!string.Equals(Volume(source), Volume(destination), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new OrganizationException("CrossVolumeMoveUnsupported");
    }

    internal static void Move(string source, string destination)
    {
        CheckVolume(source, destination);
        // File.Move can copy/delete across Unix volumes. renameat2 forbids that fallback and atomically refuses replacement.
        if (OperatingSystem.IsLinux())
        {
            if (RenameNoReplace(-100, source, -100, destination, 1) == 0) return;
            int error = Marshal.GetLastPInvokeError();
            if (error == 18) throw new OrganizationException("CrossVolumeMoveUnsupported");
            if (error == 17) throw new OrganizationException("OutputConflict");
            throw new IOException("The local rename failed.", new Win32Exception(error));
        }
        File.Move(source, destination, overwrite: false);
    }

    [DllImport("libc", EntryPoint = "renameat2", SetLastError = true)]
    private static extern int RenameNoReplace(int oldDirectory, string oldPath, int newDirectory, string newPath, uint flags);

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int OpenNonBlocking(string path, int flags);
}
