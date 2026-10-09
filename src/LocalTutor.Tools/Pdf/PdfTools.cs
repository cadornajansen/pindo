using System.Security.Cryptography;
using System.Text;
using LocalTutor.Core.Tools;
using LocalTutor.Tools.FileInspectionAndConversion;

namespace LocalTutor.Tools.Pdf;

public sealed class InspectPdfTool(PdfAccessScope scope, PopplerPdfTools tools) : LocalTool<InspectPdfInput, InspectedPdf>
{
    public override string Id => "pdf.inspect";
    public override string Description => "Inspect one approved local PDF's page count, page sizes and encrypted status; returns no document text.";

    protected override Task<ToolResult<InspectedPdf>> ExecuteValidatedAsync(InspectPdfInput input, CancellationToken cancellationToken) =>
        PdfIO.RunAsync(async token =>
        {
            string path = await PdfIO.ResolvePdfAsync(scope, input.InputPath, token);
            try
            {
                PdfInfo info = await PdfIO.InspectAsync(tools, path, token);
                string[] supported = info.Encrypted ? [] : new[]
                {
                    tools.HasUtility("pdfunite") ? "pdf.merge" : null,
                    tools.HasUtility("pdfseparate") ? "pdf.split" : null,
                    tools.HasUtility("pdftotext") && tools.HasUtility("pdfimages") ? "pdf.extract_text" : null,
                    tools.HasUtility("pdftoppm") ? "pdf.render_pages" : null
                }.Where(id => id is not null).Cast<string>().ToArray();
                return new InspectedPdf(info.PageCount, info.PageSizes, info.Encrypted, supported, info.Encrypted ? "locked" : "ready");
            }
            catch (PdfException e) when (e.Message.StartsWith("LockedPdf:", StringComparison.Ordinal))
            {
                return new InspectedPdf(null, Array.Empty<PdfPageSize>(), true, Array.Empty<string>(), "locked");
            }
        }, cancellationToken);
}

public sealed class MergePdfTool(PdfApproval approval, IProgress<PdfProgress>? progress = null)
    : LocalTool<MergePdfInput, MergedPdf>
{
    public override string Id => "pdf.merge";
    public override string Description => "Merge 2–10 approved PDFs in the displayed order into a new PDF after explicit preview approval; originals are preserved.";

    public static async Task<ToolResult<PdfOperationPreview>> PreviewAsync(PdfAccessScope scope, PopplerPdfTools tools,
        MergePdfInput input, CancellationToken cancellationToken = default, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<string> errors = input.Validate();
        if (errors.Count != 0) return new(false, null, string.Join("; ", errors));
        input = input with { InputPaths = input.InputPaths.ToArray() };
        return await PdfPlanning.PrepareAsync("pdf.merge", input, scope, tools, input.InputPaths, [input.OutputPath],
            Enumerable.Empty<int>(), cancellationToken, clock);
    }

    protected override Task<ToolResult<MergedPdf>> ExecuteValidatedAsync(MergePdfInput input, CancellationToken cancellationToken)
    {
        input = input with { InputPaths = input.InputPaths.ToArray() };
        return PdfIO.RunAsync(async token =>
        {
            PdfOperationPreview preview = approval.Preview;
            preview.Consume(Id, input);
            await PdfPlanning.VerifySourcesAsync(preview, token);
            string output = preview.ResolvedOutputs[0];
            string temporary = PdfPlanning.TempSibling(output, ".pdf");
            try
            {
                progress?.Report(new("Merging", 0, preview.ExpectedPageCount));
                string[] args = [.. preview.ResolvedInputs, temporary];
                NativeResult merge = await PdfIO.InvokeAsync(preview.Tools, "pdfunite", args, token);
                if (merge.ExitCode != 0) throw new PdfException("InvalidOrLockedPdf: Poppler could not merge the selected PDFs.");
                progress?.Report(new("Verifying", preview.ExpectedPageCount, preview.ExpectedPageCount));
                await PdfIO.VerifyPdfAsync(preview.Tools, temporary, preview.ExpectedPageCount, token);
                await PdfPlanning.VerifySourcesAsync(preview, token);
                PdfPlanning.RequireNewOutputs(preview.ResolvedOutputs);
                token.ThrowIfCancellationRequested();
                File.Move(temporary, output, overwrite: false);
                temporary = "";
                progress?.Report(new("Completed", preview.ExpectedPageCount, preview.ExpectedPageCount));
                return new MergedPdf(output, new FileInfo(output).Length, preview.ExpectedPageCount);
            }
            finally { if (temporary.Length != 0) PdfIO.Cleanup(temporary, false, token); }
        }, cancellationToken);
    }
}

public sealed class SplitPdfTool(PdfApproval approval, IProgress<PdfProgress>? progress = null)
    : LocalTool<SplitPdfInput, SplitPdf>
{
    public override string Id => "pdf.split";
    public override string Description => "Extract a validated page range or one page per output into new approved PDFs after previewing every filename; never overwrites.";

    public static async Task<ToolResult<PdfOperationPreview>> PreviewAsync(PdfAccessScope scope, PopplerPdfTools tools,
        SplitPdfInput input, CancellationToken cancellationToken = default, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<string> errors = input.Validate();
        if (errors.Count != 0) return new(false, null, string.Join("; ", errors));
        int first = input.OnePagePerFile ? 1 : input.StartPage!.Value;
        int last = input.OnePagePerFile ? PdfLimits.MaxPages : input.EndPage!.Value;
        input = input with { };
        // For one-page-per-file the page count is resolved before output names and approval are frozen.
        string source;
        PdfInfo info;
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(PdfLimits.OperationTimeout);
        try
        {
            source = scope.Files.ResolveInput(input.InputPath);
            info = await PdfPlanning.ReadUsableInfoAsync(scope, tools, input.InputPath, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return new(false, null, "TimedOut: the local PDF preview exceeded its deadline."); }
        catch (OperationCanceledException) { throw; }
        catch (PdfException e) { return new(false, null, e.Message); }
        catch (FileToolException e) { return new(false, null, e.Message); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return new(false, null, "PdfFailed: local file access prevented the preview."); }
        if (input.OnePagePerFile) { first = 1; last = info.PageCount; }
        if (first < 1 || last > info.PageCount || last < first) return new(false, null, "InvalidRange: the requested pages must fit the PDF page count.");
        if (last - first + 1 > PdfLimits.MaxSelectedPages) return new(false, null, $"ResourceLimit: split is limited to {PdfLimits.MaxSelectedPages} output pages.");
        try
        {
            string directory = scope.ResolveOutputDirectory(input.OutputDirectory);
            string[] outputs = input.OnePagePerFile
                ? Enumerable.Range(first, last - first + 1)
                    .Select(page => Path.Combine(directory, $"{input.FileNamePrefix}-{page:D3}.pdf")).ToArray()
                : [Path.Combine(directory, $"{input.FileNamePrefix}-{first:D3}-{last:D3}.pdf")];
            return await PdfPlanning.PrepareKnownAsync("pdf.split", input, scope, tools, [source], outputs,
                Enumerable.Range(first, last - first + 1).ToArray(), info.PageCount, cancellationToken, clock);
        }
        catch (PdfException e) { return new(false, null, e.Message); }
    }

    protected override Task<ToolResult<SplitPdf>> ExecuteValidatedAsync(SplitPdfInput input, CancellationToken cancellationToken)
    {
        return PdfIO.RunAsync(async token =>
        {
            PdfOperationPreview preview = approval.Preview;
            preview.Consume(Id, input);
            await PdfPlanning.VerifySourcesAsync(preview, token);
            string directory = Path.GetDirectoryName(preview.ResolvedOutputs[0])!;
            string staging = Path.Combine(directory, ".localtutor-pdf-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            List<string> published = [];
            try
            {
                string pattern = Path.Combine(staging, "page-%d.pdf");
                int first = preview.Pages[0], last = preview.Pages[^1];
                NativeResult result = await PdfIO.InvokeAsync(preview.Tools, "pdfseparate",
                    ["-f", first.ToString(), "-l", last.ToString(), preview.ResolvedInputs[0], pattern], token);
                if (result.ExitCode != 0) throw new PdfException("InvalidOrLockedPdf: Poppler could not split the selected PDF.");
                List<string> pageFiles = [];
                long stagedBytes = 0;
                foreach (int page in preview.Pages)
                {
                    token.ThrowIfCancellationRequested();
                    string file = Path.Combine(staging, $"page-{page}.pdf");
                    await PdfIO.VerifyPdfAsync(preview.Tools, file, 1, token);
                    pageFiles.Add(file);
                    stagedBytes += new FileInfo(file).Length;
                    if (stagedBytes > PdfLimits.MaxTotalOutputBytes) throw new PdfException("ResourceLimit: split outputs are limited to 256 MiB combined.");
                    progress?.Report(new("Verifying", pageFiles.Count, preview.Pages.Count));
                }
                List<string> staged = pageFiles;
                if (preview.ResolvedOutputs.Length == 1 && pageFiles.Count > 1)
                {
                    string rangeFile = Path.Combine(staging, "range.pdf");
                    NativeResult combined = await PdfIO.InvokeAsync(preview.Tools, "pdfunite", [.. pageFiles, rangeFile], token);
                    if (combined.ExitCode != 0) throw new PdfException("InvalidOutput: Poppler could not combine the selected page range.");
                    await PdfIO.VerifyPdfAsync(preview.Tools, rangeFile, pageFiles.Count, token);
                    staged = [rangeFile];
                }
                await PdfPlanning.VerifySourcesAsync(preview, token);
                PdfPlanning.RequireNewOutputs(preview.ResolvedOutputs);
                for (int i = 0; i < staged.Count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    File.Move(staged[i], preview.ResolvedOutputs[i], overwrite: false);
                    published.Add(preview.ResolvedOutputs[i]);
                }
                return new SplitPdf(Array.AsReadOnly(published.ToArray()), published.Count);
            }
            catch
            {
                PdfPlanning.Rollback(published, token);
                throw;
            }
            finally { PdfIO.Cleanup(staging, true, token); }
        }, cancellationToken);
    }
}

public sealed class ExtractPdfTextTool(PdfApproval approval, IProgress<PdfProgress>? progress = null)
    : LocalTool<ExtractPdfTextInput, ExtractedPdfText>
{
    public override string Id => "pdf.extract_text";
    public override string Description => "Extract selectable text from up to 20 specified pages to a new local UTF-8 text file; image-only pages are reported as needs_ocr and are never sent to cloud OCR.";

    public static Task<ToolResult<PdfOperationPreview>> PreviewAsync(PdfAccessScope scope, PopplerPdfTools tools,
        ExtractPdfTextInput input, CancellationToken cancellationToken = default, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<string> errors = input.Validate();
        if (errors.Count != 0) return Task.FromResult(new ToolResult<PdfOperationPreview>(false, null, string.Join("; ", errors)));
        input = input with { Pages = input.Pages.ToArray() };
        return PdfPlanning.PrepareAsync("pdf.extract_text", input, scope, tools, [input.InputPath], [input.OutputPath],
            input.Pages, cancellationToken, clock);
    }

    protected override Task<ToolResult<ExtractedPdfText>> ExecuteValidatedAsync(ExtractPdfTextInput input, CancellationToken cancellationToken)
    {
        input = input with { Pages = input.Pages.ToArray() };
        return PdfIO.RunAsync(async token =>
        {
            PdfOperationPreview preview = approval.Preview;
            preview.Consume(Id, input);
            await PdfPlanning.VerifySourcesAsync(preview, token);
            StringBuilder combined = new();
            HashSet<int> imagePages = await PdfIO.ImagePagesAsync(preview.Tools, preview.ResolvedInputs[0], token);
            List<PdfTextPageStatus> statuses = [];
            foreach (int page in preview.Pages)
            {
                NativeResult result = await PdfIO.InvokeAsync(preview.Tools, "pdftotext",
                    ["-f", page.ToString(), "-l", page.ToString(), "-enc", "UTF-8", preview.ResolvedInputs[0], "-"], token);
                if (result.ExitCode != 0) throw new PdfException("InvalidOrLockedPdf: Poppler could not extract text from this PDF.");
                string text = Encoding.UTF8.GetString(result.Output);
                if (string.IsNullOrWhiteSpace(text))
                {
                    statuses.Add(new(page, imagePages.Contains(page) ? "needs_ocr" : "no_selectable_text"));
                    progress?.Report(new("Extracting", statuses.Count, preview.Pages.Count));
                    continue;
                }
                if (combined.Length + text.Length > PdfLimits.MaxTextCharacters)
                    throw new PdfException("ResourceLimit: extracted text exceeds one million characters.");
                statuses.Add(new(page, "text_available"));
                if (combined.Length != 0) combined.AppendLine().AppendLine($"--- Page {page} ---").AppendLine();
                combined.Append(text);
                progress?.Report(new("Extracting", statuses.Count, preview.Pages.Count));
            }
            string output = preview.ResolvedOutputs[0];
            string temporary = PdfPlanning.TempSibling(output, ".txt");
            try
            {
                await File.WriteAllTextAsync(temporary, combined.ToString(), new UTF8Encoding(false), token);
                FileInfo file = new(temporary);
                if (file.Length > PdfLimits.MaxOutputBytes) throw new PdfException("ResourceLimit: text output exceeds 128 MiB.");
                await PdfPlanning.VerifySourcesAsync(preview, token);
                PdfPlanning.RequireNewOutputs(preview.ResolvedOutputs);
                token.ThrowIfCancellationRequested();
                File.Move(temporary, output, overwrite: false);
                temporary = "";
                progress?.Report(new("Completed", preview.Pages.Count, preview.Pages.Count));
                return new ExtractedPdfText(output, statuses.AsReadOnly(), combined.Length, new FileInfo(output).Length);
            }
            finally { if (temporary.Length != 0) PdfIO.Cleanup(temporary, false, token); }
        }, cancellationToken);
    }
}

public sealed class RenderPdfPagesTool(PdfApproval approval, IProgress<PdfProgress>? progress = null)
    : LocalTool<RenderPdfPagesInput, RenderedPdfPages>
{
    public override string Id => "pdf.render_pages";
    public override string Description => "Render up to 10 selected PDF pages to new PNG or JPEG files at 72–300 DPI after previewing all output paths.";

    public static Task<ToolResult<PdfOperationPreview>> PreviewAsync(PdfAccessScope scope, PopplerPdfTools tools,
        RenderPdfPagesInput input, CancellationToken cancellationToken = default, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<string> errors = input.Validate();
        if (errors.Count != 0) return Task.FromResult(new ToolResult<PdfOperationPreview>(false, null, string.Join("; ", errors)));
        input = input with { Pages = input.Pages.ToArray() };
        string[] outputs;
        try { outputs = PdfIO.RenderOutputNames(input, scope); }
        catch (PdfException e) { return Task.FromResult(new ToolResult<PdfOperationPreview>(false, null, e.Message)); }
        return PdfPlanning.PrepareAsync("pdf.render_pages", input, scope, tools, [input.InputPath], outputs,
            input.Pages.Order(), cancellationToken, clock, input.Dpi, input.Format);
    }

    protected override Task<ToolResult<RenderedPdfPages>> ExecuteValidatedAsync(RenderPdfPagesInput input, CancellationToken cancellationToken)
    {
        input = input with { Pages = input.Pages.ToArray() };
        return PdfIO.RunAsync(async token =>
        {
            PdfOperationPreview preview = approval.Preview;
            preview.Consume(Id, input);
            await PdfPlanning.VerifySourcesAsync(preview, token);
            string directory = Path.GetDirectoryName(preview.ResolvedOutputs[0])!;
            string staging = Path.Combine(directory, ".localtutor-pdf-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            List<string> published = [];
            try
            {
                List<string> staged = [];
                long stagedBytes = 0;
                string switchFormat = preview.Format == PdfImageFormat.Png ? "-png" : "-jpeg";
                string extension = preview.Format == PdfImageFormat.Png ? ".png" : ".jpg";
                foreach (int page in preview.Pages)
                {
                    token.ThrowIfCancellationRequested();
                    string prefix = Path.Combine(staging, $"page-{page:D3}");
                    NativeResult rendered = await PdfIO.InvokeAsync(preview.Tools, "pdftoppm",
                        [switchFormat, "-r", preview.Dpi.ToString(), "-f", page.ToString(), "-l", page.ToString(), "-singlefile", preview.ResolvedInputs[0], prefix], token);
                    if (rendered.ExitCode != 0) throw new PdfException("InvalidOrLockedPdf: Poppler could not render a selected page.");
                    string file = prefix + extension;
                    PdfPlanning.VerifyImage(file, preview.Format);
                    staged.Add(file);
                    stagedBytes += new FileInfo(file).Length;
                    if (stagedBytes > PdfLimits.MaxTotalOutputBytes) throw new PdfException("ResourceLimit: rendered images are limited to 256 MiB combined.");
                    progress?.Report(new("Rendering", staged.Count, preview.Pages.Count));
                }
                await PdfPlanning.VerifySourcesAsync(preview, token);
                PdfPlanning.RequireNewOutputs(preview.ResolvedOutputs);
                for (int i = 0; i < staged.Count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    File.Move(staged[i], preview.ResolvedOutputs[i], overwrite: false);
                    published.Add(preview.ResolvedOutputs[i]);
                }
                progress?.Report(new("Completed", published.Count, published.Count));
                return new RenderedPdfPages(Array.AsReadOnly(published.ToArray()), preview.Format, preview.Dpi);
            }
            catch
            {
                PdfPlanning.Rollback(published, token);
                throw;
            }
            finally { PdfIO.Cleanup(staging, true, token); }
        }, cancellationToken);
    }
}

internal static class PdfPlanning
{
    internal static async Task<ToolResult<PdfOperationPreview>> PrepareAsync<TInput>(string id, TInput input,
        PdfAccessScope scope, PopplerPdfTools tools, IEnumerable<string> inputPaths, IEnumerable<string> outputPaths,
        IEnumerable<int> pages, CancellationToken cancellationToken, TimeProvider? clock = null, int dpi = 0, PdfImageFormat format = PdfImageFormat.Png)
        where TInput : ToolInput
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(PdfLimits.OperationTimeout);
        try
        {
            string[] inputs = [];
            List<PdfInfo> infos = [];
            foreach (string path in inputPaths)
            {
                string resolved = await PdfIO.ResolvePdfAsync(scope, path, timeout.Token);
                inputs = [.. inputs, resolved];
                infos.Add(await ReadUsableInfoAsync(scope, tools, path, timeout.Token));
            }
            if (infos.Sum(info => info.PageCount) > PdfLimits.MaxPages) throw new PdfException($"ResourceLimit: operation output is limited to {PdfLimits.MaxPages} pages.");
            int expected = infos.Sum(info => info.PageCount);
            int[] requested = pages.ToArray();
            foreach (PdfInfo info in infos) PdfIO.CheckPages(requested, info.PageCount);
            return await PrepareKnownAsync(id, input, scope, tools, inputs, outputPaths, requested, expected, timeout.Token, clock, dpi, format);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException)
        { return new(false, null, "TimedOut: the local PDF preview exceeded its deadline."); }
        catch (PdfException e) { return new(false, null, e.Message); }
        catch (FileToolException e) { return new(false, null, e.Message); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return new(false, null, "PdfFailed: local file access prevented the preview."); }
    }

    internal static async Task<ToolResult<PdfOperationPreview>> PrepareKnownAsync<TInput>(string id, TInput input,
        PdfAccessScope scope, PopplerPdfTools tools, IEnumerable<string> resolvedInputs, IEnumerable<string> outputPaths,
        IEnumerable<int> pages, int expectedPageCount, CancellationToken cancellationToken, TimeProvider? clock = null, int dpi = 0, PdfImageFormat format = PdfImageFormat.Png)
        where TInput : ToolInput
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(PdfLimits.OperationTimeout);
        try
        {
            string[] inputs = resolvedInputs.ToArray();
            int[] requested = pages.ToArray();
            long totalInput = 0;
            foreach (string path in inputs)
            {
                timeout.Token.ThrowIfCancellationRequested();
                long length = new FileInfo(path).Length;
                if (length > PdfLimits.MaxInputBytes) throw new PdfException("ResourceLimit: PDF files are limited to 100 MiB each.");
                totalInput += length;
            }
            if (totalInput > PdfLimits.MaxTotalInputBytes)
                throw new PdfException("ResourceLimit: combined PDF inputs are limited to 200 MiB.");
            string[] outputs = PdfIO.ResolveOutputs(scope, outputPaths);
            var preview = new PdfOperationPreview(id, input, scope, tools, inputs, outputs, requested, expectedPageCount, dpi, format,
                new PdfOutputPreview(id, Array.AsReadOnly(inputs), Array.AsReadOnly(outputs), requested.Length == 0 ? expectedPageCount : requested.Length), clock ?? TimeProvider.System);
            preview.Hashes = await HashInputsAsync(inputs, timeout.Token);
            return new(true, preview);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException)
        { return new(false, null, "TimedOut: the local PDF preview exceeded its deadline."); }
        catch (PdfException e) { return new(false, null, e.Message); }
        catch (FileToolException e) { return new(false, null, e.Message); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return new(false, null, "PdfFailed: local file access prevented the preview."); }
    }

    internal static async Task<PdfInfo> ReadUsableInfoAsync(PdfAccessScope scope, PopplerPdfTools tools, string path, CancellationToken token)
    {
        string input = await PdfIO.ResolvePdfAsync(scope, path, token);
        PdfInfo info = await PdfIO.InspectAsync(tools, input, token);
        if (info.Encrypted) throw new PdfException("LockedPdf: encrypted PDFs are not supported; unlock the file locally and retry.");
        return info;
    }

    private static async Task<Dictionary<string, byte[]>> HashInputsAsync(IEnumerable<string> paths, CancellationToken token)
    {
        Dictionary<string, byte[]> hashes = new(StringComparer.Ordinal);
        foreach (string path in paths) hashes.Add(path, await HashAsync(path, token));
        return hashes;
    }

    internal static async Task<byte[]> HashAsync(string path, CancellationToken token)
    {
        PdfIO.CheckPath(path);
        await using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (file.Length > PdfLimits.MaxInputBytes) throw new PdfException("ResourceLimit: PDF files are limited to 100 MiB each.");
        return await SHA256.HashDataAsync(file, token);
    }

    internal static async Task VerifySourcesAsync(PdfOperationPreview preview, CancellationToken token)
    {
        foreach (string path in preview.ResolvedInputs)
        {
            token.ThrowIfCancellationRequested();
            byte[] actual = await HashAsync(path, token);
            if (!preview.Hashes.TryGetValue(path, out byte[]? expected) || !CryptographicOperations.FixedTimeEquals(expected, actual))
                throw new PdfException("SourceChanged: an approved PDF changed after preview; review a fresh preview.");
        }
    }

    internal static string TempSibling(string output, string extension) =>
        Path.Combine(Path.GetDirectoryName(output)!, ".localtutor-pdf-" + Guid.NewGuid().ToString("N") + extension);

    internal static void RequireNewOutputs(IEnumerable<string> outputs)
    {
        foreach (string path in outputs)
        {
            PdfIO.CheckPath(path);
            if (File.Exists(path) || Directory.Exists(path)) throw new PdfException("OutputConflict: choose a new output name; overwriting is disabled.");
        }
    }

    internal static void Rollback(IEnumerable<string> paths, CancellationToken token)
    {
        foreach (string path in paths)
        {
            try { File.Delete(path); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            { throw new PdfException("CleanupFailed: a partially published output could not be removed."); }
        }
    }

    internal static void VerifyImage(string path, PdfImageFormat format)
    {
        FileInfo file = new(path);
        if (!file.Exists || file.Length is <= 0 or > PdfLimits.MaxOutputBytes) throw new PdfException("InvalidOutput: rendered image is missing or exceeds 128 MiB.");
        using FileStream stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[8];
        int read = stream.Read(header);
        bool valid = format == PdfImageFormat.Png
            ? read == 8 && header.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            : read >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff;
        if (!valid) throw new PdfException("InvalidOutput: rendered image format could not be verified.");
        if (format == PdfImageFormat.Jpeg)
        {
            stream.Seek(-2, SeekOrigin.End);
            if (stream.ReadByte() != 0xff || stream.ReadByte() != 0xd9) throw new PdfException("InvalidOutput: rendered JPEG end marker could not be verified.");
        }
    }
}
