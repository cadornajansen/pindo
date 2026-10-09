using System.Diagnostics;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LocalTutor.Core.Tools;
using LocalTutor.Tools.FileInspectionAndConversion;

namespace LocalTutor.Tools.Pdf;

/// <summary>Trusted host configuration for one local Poppler installation. No executable lookup through PATH.</summary>
public sealed class PopplerPdfTools
{
    private readonly string directory;
    public PopplerPdfTools(string executableDirectory)
    {
        if (!Path.IsPathFullyQualified(executableDirectory) || !Directory.Exists(executableDirectory))
            throw new ArgumentException("An existing absolute Poppler executable directory is required.", nameof(executableDirectory));
        directory = Path.GetFullPath(executableDirectory);
    }

    internal string Executable(string tool)
    {
        string name = OperatingSystem.IsWindows() ? tool + ".exe" : tool;
        string path = Path.Combine(directory, name);
        if (!File.Exists(path)) throw new PdfException("MissingDependency: install a trusted Poppler distribution and configure its absolute utility directory.");
        return path;
    }

    internal bool HasUtility(string tool)
    {
        string name = OperatingSystem.IsWindows() ? tool + ".exe" : tool;
        return File.Exists(Path.Combine(directory, name));
    }
}

/// <summary>Host-created, exact-path approval. Construct it only after the user selects the PDF and output workspace.</summary>
public sealed class PdfAccessScope
{
    internal FileAccessScope Files { get; }
    internal string Workspace { get; }
    public PdfAccessScope(string workspace, IEnumerable<string> approvedInputs, IEnumerable<string>? approvedOutputs = null)
    {
        string[] outputPaths = (approvedOutputs ?? []).ToArray();
        string fullWorkspace = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspace));
        StringComparer comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        if (outputPaths.Select(path => Path.GetFullPath(path, fullWorkspace)).Distinct(comparer).Count() != outputPaths.Length)
            throw new ArgumentException("Duplicate approved output paths are unsupported.", nameof(approvedOutputs));
        Files = new FileAccessScope(workspace, approvedInputs, outputPaths);
        Workspace = fullWorkspace;
    }

    internal string ResolveOutputDirectory(string path)
    {
        if (FileAccessScope.ValidatePath(path).Count != 0) throw new PdfException("InvalidPath: select an unambiguous local output directory.");
        string full = Path.GetFullPath(path, Workspace);
        if (!full.StartsWith(Workspace + Path.DirectorySeparatorChar, PathComparison))
            throw new PdfException("PathOutsideWorkspace: the output directory must be inside the approved workspace.");
        PdfIO.CheckPath(full);
        if (!Directory.Exists(full)) throw new PdfException("OutputDirectoryMissing: choose an existing output directory.");
        return full;
    }

    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}

public sealed class PdfOperationPreview : IDisposable
{
    private int consumed;
    private readonly TimeProvider clock;
    internal PdfAccessScope Scope { get; }
    internal PopplerPdfTools Tools { get; }
    internal string Signature { get; }
    internal string ToolId { get; }
    internal string[] ResolvedInputs { get; }
    internal string[] ResolvedOutputs { get; }
    internal IReadOnlyList<int> Pages { get; }
    internal int ExpectedPageCount { get; }
    internal int Dpi { get; }
    internal PdfImageFormat Format { get; }
    internal Dictionary<string, byte[]> Hashes { get; set; } = new(StringComparer.Ordinal);
    public PdfOutputPreview Details { get; }
    public DateTimeOffset ExpiresAt { get; }

    internal PdfOperationPreview(string id, ToolInput input, PdfAccessScope scope, PopplerPdfTools tools,
        string[] inputs, string[] outputs, IReadOnlyList<int> pages, int pageCount, int dpi, PdfImageFormat format,
        PdfOutputPreview details, TimeProvider clock)
    {
        ToolId = id; Signature = InputSignature(input); Scope = scope; Tools = tools;
        ResolvedInputs = inputs; ResolvedOutputs = outputs; Pages = pages; ExpectedPageCount = pageCount;
        Dpi = dpi; Format = format; Details = details; this.clock = clock;
        ExpiresAt = clock.GetUtcNow().AddMinutes(5);
    }

    internal void Consume(string id, ToolInput input)
    {
        if (id != ToolId || Signature != InputSignature(input)) throw new PdfException("ApprovalMismatch: review a fresh preview for these exact arguments.");
        if (Interlocked.Exchange(ref consumed, 1) != 0) throw new PdfException("ApprovalConsumed: request a fresh preview before trying again.");
        if (clock.GetUtcNow() >= ExpiresAt) throw new PdfException("ApprovalExpired: request a fresh preview before trying again.");
    }

    internal static string InputSignature(ToolInput input) => JsonSerializer.Serialize(input, input.GetType());
    public void Dispose() => Interlocked.Exchange(ref consumed, 1);
}

internal sealed class PdfException(string message) : Exception(message);
internal sealed record PdfInfo(int PageCount, IReadOnlyList<PdfPageSize> PageSizes, bool Encrypted);
internal sealed record NativeResult(int ExitCode, byte[] Output, byte[] Error);

internal static class PdfIO
{
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static readonly Regex PageCountRegex = new(@"^Pages:\s+(\d+)\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex EncryptedRegex = new(@"^Encrypted:\s+(yes|no)\b", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex PageSizeRegex = new(@"^Page(?:\s+(\d+))?\s+size:\s+([\d.]+)\s+x\s+([\d.]+)\s+pts\b", RegexOptions.Multiline | RegexOptions.CultureInvariant);

    internal static async Task<ToolResult<T>> RunAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken userToken)
    {
        userToken.ThrowIfCancellationRequested();
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(userToken);
        timeout.CancelAfter(PdfLimits.OperationTimeout);
        try { return new(true, await operation(timeout.Token)); }
        catch (OperationCanceledException) when (!userToken.IsCancellationRequested)
        { return new(false, default, "TimedOut: the local PDF operation exceeded its deadline."); }
        catch (PdfException e) { return new(false, default, e.Message); }
        catch (FileToolException e) { return new(false, default, e.Message); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return new(false, default, "PdfFailed: invalid PDF content or local file access prevented the operation."); }
    }

    internal static void CheckPath(string path)
    {
        string[] protectedRoots = OperatingSystem.IsWindows()
            ? [Environment.GetFolderPath(Environment.SpecialFolder.Windows), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)]
            : ["/etc", "/proc", "/sys", "/dev", "/usr", "/bin", "/sbin", "/boot", "/lib", "/lib64", "/root"];
        string full = Path.GetFullPath(path);
        foreach (string root in protectedRoots.Where(r => !string.IsNullOrEmpty(r)))
            if (full.Equals(root, PathComparison) || full.StartsWith(root + Path.DirectorySeparatorChar, PathComparison))
                throw new PdfException("ProtectedPath: system locations cannot be used.");
        for (string? current = full; current is not null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new PdfException("LinkedPath: symbolic links and reparse points are unsupported.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    internal static async Task<string> ResolvePdfAsync(PdfAccessScope scope, string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        string full = scope.Files.ResolveInput(path);
        FileInfo file = new(full);
        if (file.Length is <= 0 or > PdfLimits.MaxInputBytes) throw new PdfException("ResourceLimit: PDF files must be between 1 byte and 100 MiB.");
        if (!string.Equals(Path.GetExtension(full), ".pdf", StringComparison.OrdinalIgnoreCase)) throw new PdfException("UnsupportedFormat: choose a .pdf file.");
        byte[] signature = new byte[5];
        await using (FileStream stream = new(full, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            if (!await ReadHeaderAsync(stream, signature, token) || !signature.AsSpan().SequenceEqual("%PDF-"u8))
                throw new PdfException("InvalidPdf: the file does not have a PDF header.");
        }
        return full;
    }

    internal static async Task<PdfInfo> InspectAsync(PopplerPdfTools tools, string input, CancellationToken token)
    {
        NativeResult result = await InvokeAsync(tools, "pdfinfo", ["-f", "1", "-l", PdfLimits.MaxPages.ToString(CultureInfo.InvariantCulture), input], token);
        if (result.ExitCode != 0)
        {
            if (Encoding.UTF8.GetString(result.Error).Contains("Incorrect password", StringComparison.OrdinalIgnoreCase))
                throw new PdfException("LockedPdf: this PDF is encrypted; unlock it locally to inspect or process it.");
            throw new PdfException("InvalidPdf: Poppler could not read this PDF.");
        }
        string text = Encoding.UTF8.GetString(result.Output);
        Match pages = PageCountRegex.Match(text);
        Match encrypted = EncryptedRegex.Match(text);
        if (!pages.Success || !int.TryParse(pages.Groups[1].Value, CultureInfo.InvariantCulture, out int count) || count < 1)
            throw new PdfException("InvalidPdf: page count metadata is missing or invalid.");
        if (count > PdfLimits.MaxPages) throw new PdfException($"ResourceLimit: PDFs are limited to {PdfLimits.MaxPages} pages.");
        bool isEncrypted = encrypted.Success && encrypted.Groups[1].Value.Equals("yes", StringComparison.OrdinalIgnoreCase);
        List<PdfPageSize> sizes = [];
        foreach (Match match in PageSizeRegex.Matches(text))
        {
            if (!match.Groups[1].Success || !int.TryParse(match.Groups[1].Value, out int page)) continue;
            if (double.TryParse(match.Groups[2].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double width) &&
                double.TryParse(match.Groups[3].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double height))
                sizes.Add(new(page, width, height));
        }
        if (sizes.Count != count || sizes.Where((size, index) => size.Page != index + 1 ||
                !double.IsFinite(size.WidthPoints) || !double.IsFinite(size.HeightPoints) ||
                size.WidthPoints <= 0 || size.HeightPoints <= 0).Any())
            throw new PdfException("InvalidPdf: per-page size metadata could not be verified.");
        return new(count, sizes.AsReadOnly(), isEncrypted);
    }

    internal static async Task EnsureUsableAsync(PopplerPdfTools tools, string input, CancellationToken token)
    {
        PdfInfo info = await InspectAsync(tools, input, token);
        if (info.Encrypted) throw new PdfException("LockedPdf: encrypted PDFs are not supported; unlock the file locally and retry.");
    }

    internal static async Task<HashSet<int>> ImagePagesAsync(PopplerPdfTools tools, string input, CancellationToken token)
    {
        NativeResult result = await InvokeAsync(tools, "pdfimages", ["-list", input], token);
        if (result.ExitCode != 0) throw new PdfException("InvalidOrLockedPdf: Poppler could not inspect page images.");
        HashSet<int> pages = [];
        foreach (string line in Encoding.UTF8.GetString(result.Output).Split('\n'))
        {
            string[] columns = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (columns.Length >= 3 && int.TryParse(columns[0], NumberStyles.None, CultureInfo.InvariantCulture, out int page) &&
                int.TryParse(columns[1], NumberStyles.None, CultureInfo.InvariantCulture, out _))
                pages.Add(page);
        }
        return pages;
    }

    private static async Task<bool> ReadHeaderAsync(Stream stream, byte[] header, CancellationToken token)
    {
        int read = 0;
        while (read < header.Length)
        {
            int count = await stream.ReadAsync(header.AsMemory(read), token);
            if (count == 0) return false;
            read += count;
        }
        return true;
    }

    internal static async Task<NativeResult> InvokeAsync(PopplerPdfTools tools, string utility, IReadOnlyList<string> args, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        string executable = tools.Executable(utility);
        ProcessStartInfo start = new(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.Environment["LC_ALL"] = "C";
        foreach (string argument in args) start.ArgumentList.Add(argument);
        using Process process = new() { StartInfo = start, EnableRaisingEvents = true };
        try { if (!process.Start()) throw new PdfException("MissingDependency: the configured PDF utility could not start."); }
        catch (Win32Exception) { throw new PdfException("MissingDependency: the configured PDF utility could not start."); }
        using CancellationTokenRegistration registration = token.Register(() => { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } catch (Win32Exception) { } });
        Task<byte[]> stdout = ReadBoundedAsync(process.StandardOutput.BaseStream, PdfLimits.MaxProcessOutputBytes, process, token);
        Task<byte[]> stderr = ReadBoundedAsync(process.StandardError.BaseStream, 32 * 1024, process, token);
        try
        {
            await process.WaitForExitAsync(token);
            byte[] output = await stdout;
            byte[] error = await stderr; // Diagnostics remain private; only a fixed password error is classified.
            token.ThrowIfCancellationRequested();
            return new(process.ExitCode, output, error);
        }
        catch
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } catch (Win32Exception) { }
            throw;
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, int limit, Process process, CancellationToken token)
    {
        using MemoryStream result = new();
        byte[] buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer, token)) != 0)
        {
            if (result.Length + read > limit)
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } catch (Win32Exception) { }
                throw new PdfException("ResourceLimit: the PDF utility produced too much output.");
            }
            await result.WriteAsync(buffer.AsMemory(0, read), token);
        }
        return result.ToArray();
    }

    internal static string[] ResolveOutputs(PdfAccessScope scope, IEnumerable<string> outputs)
    {
        string[] resolved = outputs.Select(scope.Files.ResolveOutput).ToArray();
        if (resolved.Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).Count() != resolved.Length)
            throw new PdfException("OutputConflict: output names must be unique.");
        return resolved;
    }

    internal static string[] SplitOutputNames(SplitPdfInput input, int first, int last, PdfAccessScope scope)
    {
        string directory = scope.ResolveOutputDirectory(input.OutputDirectory);
        string[] names = Enumerable.Range(first, last - first + 1).Select(page => Path.Combine(directory, $"{input.FileNamePrefix}-{page:D3}.pdf")).ToArray();
        return names;
    }

    internal static string[] RenderOutputNames(RenderPdfPagesInput input, PdfAccessScope scope)
    {
        string directory = scope.ResolveOutputDirectory(input.OutputDirectory);
        string extension = input.Format == PdfImageFormat.Png ? ".png" : ".jpg";
        return input.Pages.Order().Select(page => Path.Combine(directory, $"{input.FileNamePrefix}-{page:D3}{extension}")).ToArray();
    }

    internal static void CheckPages(IEnumerable<int> pages, int count)
    {
        if (pages.Any(page => page < 1 || page > count)) throw new PdfException("InvalidRange: every selected page must be within the PDF page count.");
    }

    internal static async Task VerifyPdfAsync(PopplerPdfTools tools, string path, int expectedPages, CancellationToken token)
    {
        FileInfo file = new(path);
        if (!file.Exists || file.Length is <= 0 or > PdfLimits.MaxOutputBytes) throw new PdfException("InvalidOutput: the generated PDF is missing or exceeds 128 MiB.");
        await using FileStream stream = File.OpenRead(path);
        byte[] header = new byte[5];
        if (!await ReadHeaderAsync(stream, header, token) || !header.AsSpan().SequenceEqual("%PDF-"u8)) throw new PdfException("InvalidOutput: generated PDF header could not be verified.");
        PdfInfo info = await InspectAsync(tools, path, token);
        if (info.Encrypted || info.PageCount != expectedPages) throw new PdfException("InvalidOutput: generated PDF page count did not match the preview.");
    }

    internal static void Cleanup(string? path, bool directory, CancellationToken token)
    {
        if (path is null) return;
        try { if (directory) Directory.Delete(path, recursive: true); else File.Delete(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            if (token.IsCancellationRequested) throw new OperationCanceledException("CanceledWithCleanupWarning: a partial PDF output could not be removed.", e, token);
            throw new PdfException("CleanupFailed: a partial output could not be removed.");
        }
    }
}
