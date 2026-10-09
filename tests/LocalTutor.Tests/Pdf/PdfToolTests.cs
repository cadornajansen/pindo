using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using LocalTutor.Tools.FileInspectionAndConversion;
using LocalTutor.Tools.Pdf;

namespace LocalTutor.Tests.Pdf;

public sealed class PdfToolTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "localtutor-pdf-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string toolsPath = Environment.GetEnvironmentVariable("POPPLER_BIN_DIR") ?? "/usr/bin";
    private readonly string imageToolsPath = Environment.GetEnvironmentVariable("LOCAL_TUTOR_IMAGEMAGICK") ??
        (OperatingSystem.IsWindows() ? throw new InvalidOperationException("Set LOCAL_TUTOR_IMAGEMAGICK for PDF image decode checks.") : "/usr/bin/convert");
    private PopplerPdfTools Tools => new(toolsPath);

    public PdfToolTests()
    {
        Directory.CreateDirectory(root);
        string[] required = OperatingSystem.IsWindows()
            ? ["pdfinfo.exe", "pdfunite.exe", "pdfseparate.exe", "pdftotext.exe", "pdftoppm.exe", "pdfimages.exe"]
            : ["pdfinfo", "pdfunite", "pdfseparate", "pdftotext", "pdftoppm", "pdfimages"];
        if (!required.All(name => File.Exists(Path.Combine(toolsPath, name))))
            throw new InvalidOperationException("Set POPPLER_BIN_DIR to an installed Poppler utility directory before running PDF tests.");
    }

    public void Dispose() => Directory.Delete(root, recursive: true);
    private string At(string path) => Path.Combine(root, path);
    private PdfAccessScope Scope(string[] inputs, params string[] outputs) => new(root, inputs, outputs);

    private void WritePdf(string path, string? text, bool imageOnly = false, int width = 612, int height = 792)
    {
        string content = imageOnly ? "q 100 0 0 100 72 700 cm /Im0 Do Q" : text ?? "";
        string resources = imageOnly ? "<< /XObject << /Im0 6 0 R >> >>" : "<< /Font << /F1 6 0 R >> >>";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} {height}] /Resources 5 0 R /Contents 4 0 R >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream",
            resources,
            imageOnly ? "<< /Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /ASCIIHexDecode /Length 7 >>\nstream\nFF0000>\nendstream"
                : "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        ];
        using MemoryStream pdf = new();
        void Add(string value) => pdf.Write(Encoding.ASCII.GetBytes(value));
        Add("%PDF-1.4\n");
        long[] offsets = new long[objects.Length + 1];
        for (int i = 0; i < objects.Length; i++)
        {
            offsets[i + 1] = pdf.Position;
            Add($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        long xref = pdf.Position;
        Add($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        for (int i = 1; i < offsets.Length; i++) Add(offsets[i].ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        Add($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        File.WriteAllBytes(At(path), pdf.ToArray());
    }

    private void WriteTextPdf(string path, string text) => WritePdf(path, $"BT /F1 18 Tf 72 720 Td ({text}) Tj ET");

    [Fact]
    public async Task InspectMergeSplitExtractAndRenderVerifyOutputsAndPageCounts()
    {
        WriteTextPdf("first.pdf", "Teacher lesson");
        WritePdf("second.pdf", null, width: 400, height: 600);
        WritePdf("third.pdf", null, imageOnly: true);

        var inspected = await new InspectPdfTool(Scope(["first.pdf"]), Tools).ExecuteAsync(new("first.pdf"));
        Assert.True(inspected.Success, inspected.Error);
        Assert.Equal(1, inspected.Value!.PageCount);
        Assert.Equal(612, inspected.Value.PageSizes[0].WidthPoints);
        Assert.False(inspected.Value.IsEncrypted);
        Assert.Contains("pdf.extract_text", inspected.Value.SupportedOperations);

        using PdfOperationPreview mergePreview = (await MergePdfTool.PreviewAsync(Scope(["first.pdf", "second.pdf", "third.pdf"], "merged.pdf"), Tools,
            new(["first.pdf", "second.pdf", "third.pdf"], "merged.pdf"))).Value!;
        Assert.Equal(3, mergePreview.Details.PageCount);
        Assert.Equal([At("first.pdf"), At("second.pdf"), At("third.pdf")], mergePreview.Details.InputOrder);
        var merged = await new MergePdfTool(new(mergePreview)).ExecuteAsync(new(["first.pdf", "second.pdf", "third.pdf"], "merged.pdf"));
        Assert.True(merged.Success, merged.Error);
        Assert.Equal(3, merged.Value!.PageCount);
        Assert.True(File.Exists(At("merged.pdf")));
        var mixedSizes = await new InspectPdfTool(Scope(["merged.pdf"]), Tools).ExecuteAsync(new("merged.pdf"));
        Assert.True(mixedSizes.Success, mixedSizes.Error);
        Assert.Equal([612, 400, 612], mixedSizes.Value!.PageSizes.Select(size => size.WidthPoints));
        Assert.Equal([792, 600, 792], mixedSizes.Value.PageSizes.Select(size => size.HeightPoints));

        Directory.CreateDirectory(At("range"));
        using PdfOperationPreview rangePreview = (await SplitPdfTool.PreviewAsync(Scope(["merged.pdf"], "range/lesson-001-002.pdf"), Tools,
            new("merged.pdf", "range", "lesson", StartPage: 1, EndPage: 2))).Value!;
        Assert.Single(rangePreview.Details.OutputPaths);
        Assert.Equal(2, rangePreview.Details.PageCount);
        var range = await new SplitPdfTool(new(rangePreview)).ExecuteAsync(new("merged.pdf", "range", "lesson", StartPage: 1, EndPage: 2));
        Assert.True(range.Success, range.Error);
        Assert.Single(range.Value!.OutputPaths);
        var reopenedRange = await new InspectPdfTool(Scope(["range/lesson-001-002.pdf"]), Tools).ExecuteAsync(new("range/lesson-001-002.pdf"));
        Assert.True(reopenedRange.Success, reopenedRange.Error);
        Assert.Equal(2, reopenedRange.Value!.PageCount);
        Assert.Equal([612, 400], reopenedRange.Value.PageSizes.Select(size => size.WidthPoints));

        Directory.CreateDirectory(At("split"));
        string[] splitOutputs = ["split/lesson-001.pdf", "split/lesson-002.pdf", "split/lesson-003.pdf"];
        using PdfOperationPreview splitPreview = (await SplitPdfTool.PreviewAsync(Scope(["merged.pdf"], splitOutputs), Tools,
            new("merged.pdf", "split", "lesson", OnePagePerFile: true))).Value!;
        Assert.Equal(3, splitPreview.Details.OutputPaths.Count);
        var split = await new SplitPdfTool(new(splitPreview)).ExecuteAsync(new("merged.pdf", "split", "lesson", OnePagePerFile: true));
        Assert.True(split.Success, split.Error);
        Assert.Equal(3, split.Value!.PageCount);
        foreach (string file in split.Value.OutputPaths)
        {
            var page = await new InspectPdfTool(Scope([file]), Tools).ExecuteAsync(new(file));
            Assert.True(page.Success, page.Error);
            Assert.Equal(1, page.Value!.PageCount);
        }

        using PdfOperationPreview textPreview = (await ExtractPdfTextTool.PreviewAsync(Scope(["merged.pdf"], "extracted.txt"), Tools,
            new("merged.pdf", [1, 2, 3], "extracted.txt"))).Value!;
        var extracted = await new ExtractPdfTextTool(new(textPreview)).ExecuteAsync(new("merged.pdf", [1, 2, 3], "extracted.txt"));
        Assert.True(extracted.Success, extracted.Error);
        Assert.Contains(1, extracted.Value!.PagesWithText);
        Assert.Equal("no_selectable_text", extracted.Value.PageStatuses.Single(page => page.Page == 2).Status);
        Assert.Equal("needs_ocr", extracted.Value.PageStatuses.Single(page => page.Page == 3).Status);
        Assert.Contains("Teacher lesson", File.ReadAllText(At("extracted.txt")));

        Directory.CreateDirectory(At("rendered"));
        using PdfOperationPreview renderPreview = (await RenderPdfPagesTool.PreviewAsync(Scope(["merged.pdf"], "rendered/page-001.png"), Tools,
            new("merged.pdf", [1], "rendered", "page", PdfImageFormat.Png, 96))).Value!;
        var rendered = await new RenderPdfPagesTool(new(renderPreview)).ExecuteAsync(new("merged.pdf", [1], "rendered", "page", PdfImageFormat.Png, 96));
        Assert.True(rendered.Success, rendered.Error);
        Assert.Single(rendered.Value!.OutputPaths);
        byte[] image = File.ReadAllBytes(At("rendered/page-001.png"));
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, image[..8]);
        var decodedPng = await new InspectFileTool(new FileAccessScope(root, [At("rendered/page-001.png")]), new ImageMagickCodec(imageToolsPath))
            .ExecuteAsync(new(At("rendered/page-001.png")));
        Assert.True(decodedPng.Success, decodedPng.Error);
        Assert.True(decodedPng.Value!.Width > 0);

        using PdfOperationPreview jpegPreview = (await RenderPdfPagesTool.PreviewAsync(Scope(["merged.pdf"], "rendered/page-001.jpg"), Tools,
            new("merged.pdf", [1], "rendered", "page", PdfImageFormat.Jpeg, 96))).Value!;
        var jpeg = await new RenderPdfPagesTool(new(jpegPreview)).ExecuteAsync(new("merged.pdf", [1], "rendered", "page", PdfImageFormat.Jpeg, 96));
        Assert.True(jpeg.Success, jpeg.Error);
        byte[] jpegBytes = File.ReadAllBytes(At("rendered/page-001.jpg"));
        Assert.Equal(new byte[] { 0xff, 0xd8, 0xff }, jpegBytes[..3]);
        Assert.Equal(new byte[] { 0xff, 0xd9 }, jpegBytes[^2..]);
        var decodedJpeg = await new InspectFileTool(new FileAccessScope(root, [At("rendered/page-001.jpg")]), new ImageMagickCodec(imageToolsPath))
            .ExecuteAsync(new(At("rendered/page-001.jpg")));
        Assert.True(decodedJpeg.Success, decodedJpeg.Error);
        Assert.True(decodedJpeg.Value!.Width > 0);
    }

    [Fact]
    public async Task InvalidRangesDuplicatePagesCorruptAndOutputConflictsAreRejected()
    {
        WriteTextPdf("one.pdf", "Only page");
        var invalidRange = await SplitPdfTool.PreviewAsync(Scope(["one.pdf"], "out/not-possible-002.pdf"), Tools,
            new("one.pdf", "out", "not-possible", StartPage: 2, EndPage: 2));
        Assert.False(invalidRange.Success);
        Assert.Contains("InvalidRange", invalidRange.Error);
        Assert.False(new ExtractPdfTextInput("one.pdf", [1, 1], "text.txt").Validate().Count.Equals(0));
        Assert.False(new MergePdfInput(["one.pdf", "one.pdf"], "merged.pdf").Validate().Count.Equals(0));
        Assert.False(new RenderPdfPagesInput("one.pdf", [1, 1], "out", "page", PdfImageFormat.Png).Validate().Count.Equals(0));
        Assert.NotEmpty(new SplitPdfInput("one.pdf", "out", "bad:", 1, 1).Validate());
        Assert.NotEmpty(new SplitPdfInput("one.pdf", "out", "trailing.", 1, 1).Validate());
        Assert.Empty(new SplitPdfInput("one.pdf", "out", "lesson.v2", 1, 1).Validate());
        Assert.ThrowsAny<Exception>(() => Scope(["one.pdf"], "out/duplicate.pdf", "out/duplicate.pdf"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<InspectPdfInput>("{\"InputPath\":\"one.pdf\",\"Extra\":true}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RenderPdfPagesInput>("{\"InputPath\":\"one.pdf\",\"Pages\":[1],\"OutputDirectory\":\"out\",\"FileNamePrefix\":\"page\",\"Format\":1}"));

        Directory.CreateDirectory(At("out"));
        File.WriteAllText(At("out/lesson-001.pdf"), "already exists");
        var conflict = await SplitPdfTool.PreviewAsync(Scope(["one.pdf"], "out/lesson-001.pdf"), Tools,
            new("one.pdf", "out", "lesson", OnePagePerFile: true));
        Assert.False(conflict.Success);
        Assert.Contains("OutputConflict", conflict.Error);

        File.WriteAllText(At("broken.pdf"), "not a PDF");
        var corrupt = await new InspectPdfTool(Scope(["broken.pdf"]), Tools).ExecuteAsync(new("broken.pdf"));
        Assert.False(corrupt.Success);
        Assert.Contains("InvalidPdf", corrupt.Error);
        File.WriteAllText(At("fake-encrypted.pdf"), "%PDF-1.4\n/Encrypt\nnot a PDF");
        var fakeEncrypted = await new InspectPdfTool(Scope(["fake-encrypted.pdf"]), Tools).ExecuteAsync(new("fake-encrypted.pdf"));
        Assert.False(fakeEncrypted.Success);
        Assert.Contains("InvalidPdf", fakeEncrypted.Error);
    }

    [Fact]
    public async Task EncryptedPdfIsClearlyReportedAsLocked()
    {
        const string lockedPdf = "JVBERi0xLjQKJcK1wrYKCjEgMCBvYmoKPDwvVHlwZS9DYXRhbG9nL1BhZ2VzIDIgMCBSPj4KZW5kb2JqCgoyIDAgb2JqCjw8L1R5cGUvUGFnZXMvS2lkc1szIDAgUl0vQ291bnQgMT4+CmVuZG9iagoKMyAwIG9iago8PC9UeXBlL1BhZ2UvUGFyZW50IDIgMCBSL01lZGlhQm94WzAgMCA2MTIgNzkyXS9SZXNvdXJjZXMgNSAwIFIvQ29udGVudHMgNCAwIFI+PgplbmRvYmoKCjQgMCBvYmoKPDwvTGVuZ3RoIDY0Pj4Kc3RyZWFtCghvlp5vS9qxStQtVtNK2JikB1ITNghY9l7zjBqO3dvAwFwob4oobXqLKIPsmjxKnx0N/9ypE0fm81yy+gL7l7EKZW5kc3RyZWFtCmVuZG9iagoKNSAwIG9iago8PC9Gb250PDwvRjEgNiAwIFI+Pj4+CmVuZG9iagoKNiAwIG9iago8PC9UeXBlL0ZvbnQvU3VidHlwZS9UeXBlMS9CYXNlRm9udC9IZWx2ZXRpY2E+PgplbmRvYmoKCnhyZWYKMCA3CjAwMDAwMDAwMDAgNjU1MzYgZiAKMDAwMDAwMDAxNiAwMDAwMCBuIAowMDAwMDAwMDYyIDAwMDAwIG4gCjAwMDAwMDAxMTQgMDAwMDAgbiAKMDAwMDAwMDIxMSAwMDAwMCBuIAowMDAwMDAwMzI0IDAwMDAwIG4gCjAwMDAwMDAzNjMgMDAwMDAgbiAKCnRyYWlsZXIKPDwvU2l6ZSA3L1Jvb3QgMSAwIFIvSURbPDkxREU5QUFBQUM2ODcxRTBDRTAxNDcwNUQ3MDE2MTQ3Pjw4N0ZFMzg4RTBEOEYxMTBGMkUyQjNBRkUwRTJGRTI2MD5dL0VuY3J5cHQ8PC9GaWx0ZXIvU3RhbmRhcmQvUiA2L1YgNS9MZW5ndGggMjU2L1AgLTQvRW5jcnlwdE1ldGFkYXRhIHRydWUvU3RtRi9TdGRDRi9TdHJGL1N0ZENGL0NGPDwvU3RkQ0Y8PC9BdXRoRXZlbnQvRG9jT3Blbi9DRk0vQUVTVjMvTGVuZ3RoIDMyPj4+Pi9PPEU3NEEyOUIwOEY2QTEyOTE5RTZCMTZFQUYzQjc3NzdDNjg5RjhFQTg2QjUxOEM1NTU1NDI4N0YzNDg1ODc1OUQxMTg0RjcyOTEwQzk0QkQ3NTlBMjM4OUY2QkJDRDcyOD4vVTxDQjFCOUU1NEJCQzg3NjNFMjk0MTkwNzQ3NzlFMzBFMUI2REQxOTM2QzU0NzlENjdGNTdFQkYzMkE1Q0VDQjJCRURDQ0RCMDNDNkEzQUY4OTJFMDk2MjYzREUyNkE0REY+L09FPDY2QTA4NUE1OUE5ODI2NzhGRDEyNDNBRDEwQjk4RTdEQjc3MTcyODc1MTk3MjU1MjIwQ0VDNUI5QzUxQkNGRTg+L1VFPDQ0QjZFRUE1MDFCN0ZDMjNENDlDN0IyMTI3RDBCNjYyRDEyODA3NEI0NzZGRjlFNDVGNUJCQ0JGODc3MzAwQTk+L1Blcm1zPERDOUU2N0JDMEMxQkYzMDRDMTcwMDVBRjJGMDcyMEI1Pj4+Pj4Kc3RhcnR4cmVmCjQyNwolJUVPRgo=";
        File.WriteAllBytes(At("locked.pdf"), Convert.FromBase64String(lockedPdf));
        var result = await new InspectPdfTool(Scope(["locked.pdf"]), Tools).ExecuteAsync(new("locked.pdf"));
        Assert.True(result.Success, result.Error);
        Assert.True(result.Value!.IsEncrypted);
        Assert.Null(result.Value.PageCount);
        Assert.Equal("locked", result.Value.Status);
        Assert.Empty(result.Value.SupportedOperations);
    }

    [Fact]
    public async Task CancellationAndWorkspaceBoundariesDoNotWriteOutputs()
    {
        WriteTextPdf("source.pdf", "Private content");
        using CancellationTokenSource canceled = new();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new InspectPdfTool(Scope(["source.pdf"]), Tools)
            .ExecuteAsync(new("source.pdf"), canceled.Token));
        Assert.ThrowsAny<Exception>(() => Scope([At("../outside.pdf")], "output.pdf"));
        Assert.ThrowsAny<Exception>(() => Scope(["source.pdf"], "../escape.pdf"));
        Assert.Empty(Directory.GetFiles(root, ".localtutor-pdf-*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task MidRenderCancellationCleansStagedPages()
    {
        WriteTextPdf("first.pdf", "First page");
        WritePdf("second.pdf", null);
        using PdfOperationPreview mergePreview = (await MergePdfTool.PreviewAsync(Scope(["first.pdf", "second.pdf"], "two.pdf"), Tools,
            new(["first.pdf", "second.pdf"], "two.pdf"))).Value!;
        Assert.True((await new MergePdfTool(new(mergePreview)).ExecuteAsync(new(["first.pdf", "second.pdf"], "two.pdf"))).Success);

        Directory.CreateDirectory(At("cancelled"));
        using PdfOperationPreview preview = (await RenderPdfPagesTool.PreviewAsync(Scope(["two.pdf"], "cancelled/page-001.png", "cancelled/page-002.png"), Tools,
            new("two.pdf", [1, 2], "cancelled", "page", PdfImageFormat.Png))).Value!;
        using CancellationTokenSource canceled = new();
        var progress = new InlineProgress(state => { if (state.Stage == "Rendering") canceled.Cancel(); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RenderPdfPagesTool(new(preview), progress)
            .ExecuteAsync(new("two.pdf", [1, 2], "cancelled", "page", PdfImageFormat.Png), canceled.Token));
        Assert.False(File.Exists(At("cancelled/page-001.png")));
        Assert.False(File.Exists(At("cancelled/page-002.png")));
        Assert.Empty(Directory.GetFiles(At("cancelled"), ".localtutor-pdf-*", SearchOption.TopDirectoryOnly));
    }

    private sealed class InlineProgress(Action<PdfProgress> callback) : IProgress<PdfProgress>
    {
        public void Report(PdfProgress value) => callback(value);
    }
}
