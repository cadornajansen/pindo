using System.Text.Json.Serialization;
using System.Text.Json;
using LocalTutor.Core.Tools;
using LocalTutor.Tools.FileInspectionAndConversion;

namespace LocalTutor.Tools.Pdf;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InspectPdfInput([property: JsonRequired] string InputPath) : ToolInput
{
    public override IReadOnlyList<string> Validate() => FileAccessScope.ValidatePath(InputPath);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MergePdfInput(
    [property: JsonRequired] string[] InputPaths,
    [property: JsonRequired] string OutputPath) : ToolInput
{
    public override IReadOnlyList<string> Validate()
    {
        List<string> errors = [.. FileAccessScope.ValidatePath(OutputPath)];
        if (InputPaths is null || InputPaths.Length is < 2 or > PdfLimits.MaxMergeFiles)
            errors.Add($"InvalidInput: select 2–{PdfLimits.MaxMergeFiles} PDFs in merge order.");
        else
        {
            foreach (string path in InputPaths) errors.AddRange(FileAccessScope.ValidatePath(path));
            if (InputPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != InputPaths.Length)
                errors.Add("InvalidInput: the same PDF cannot appear more than once.");
        }
        if (!string.Equals(Path.GetExtension(OutputPath), ".pdf", StringComparison.OrdinalIgnoreCase))
            errors.Add("UnsupportedFormat: choose a .pdf output.");
        return errors;
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SplitPdfInput(
    [property: JsonRequired] string InputPath,
    [property: JsonRequired] string OutputDirectory,
    [property: JsonRequired] string FileNamePrefix,
    int? StartPage = null,
    int? EndPage = null,
    bool OnePagePerFile = false) : ToolInput
{
    public override IReadOnlyList<string> Validate()
    {
        List<string> errors = [.. FileAccessScope.ValidatePath(InputPath), .. FileAccessScope.ValidatePath(OutputDirectory)];
        if (!PdfLimits.IsSafeFileNamePrefix(FileNamePrefix))
            errors.Add("InvalidInput: use a filename prefix of 1–64 safe characters.");
        if (OnePagePerFile && (StartPage.HasValue || EndPage.HasValue))
            errors.Add("InvalidInput: choose either one page per file or a page range.");
        if (!OnePagePerFile && (!StartPage.HasValue || !EndPage.HasValue || StartPage < 1 || EndPage < StartPage))
            errors.Add("InvalidRange: provide a positive start page and an end page at least as large.");
        return errors;
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ExtractPdfTextInput(
    [property: JsonRequired] string InputPath,
    [property: JsonRequired] int[] Pages,
    [property: JsonRequired] string OutputPath) : ToolInput
{
    public override IReadOnlyList<string> Validate()
    {
        List<string> errors = [.. FileAccessScope.ValidatePath(InputPath), .. FileAccessScope.ValidatePath(OutputPath)];
        if (Pages is null || Pages.Length is 0 or > PdfLimits.MaxSelectedPages || Pages.Any(p => p < 1) || Pages.Distinct().Count() != Pages.Length)
            errors.Add($"InvalidRange: select 1–{PdfLimits.MaxSelectedPages} unique positive page numbers.");
        if (!string.Equals(Path.GetExtension(OutputPath), ".txt", StringComparison.OrdinalIgnoreCase))
            errors.Add("UnsupportedFormat: choose a .txt output.");
        return errors;
    }
}

[JsonConverter(typeof(PdfImageFormatJsonConverter))]
public enum PdfImageFormat { Png, Jpeg }

public sealed class PdfImageFormatJsonConverter : JsonStringEnumConverter<PdfImageFormat>
{
    public PdfImageFormatJsonConverter() : base(allowIntegerValues: false) { }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RenderPdfPagesInput(
    [property: JsonRequired] string InputPath,
    [property: JsonRequired] int[] Pages,
    [property: JsonRequired] string OutputDirectory,
    [property: JsonRequired] string FileNamePrefix,
    [property: JsonRequired] PdfImageFormat Format,
    int Dpi = 150) : ToolInput
{
    public override IReadOnlyList<string> Validate()
    {
        List<string> errors = [.. FileAccessScope.ValidatePath(InputPath), .. FileAccessScope.ValidatePath(OutputDirectory)];
        if (Pages is null || Pages.Length is 0 or > PdfLimits.MaxRenderedPages || Pages.Any(p => p < 1) || Pages.Distinct().Count() != Pages.Length)
            errors.Add($"InvalidRange: select 1–{PdfLimits.MaxRenderedPages} unique positive page numbers.");
        if (!Enum.IsDefined(Format)) errors.Add("InvalidInput: choose PNG or JPEG.");
        if (Dpi is < 72 or > PdfLimits.MaxRenderDpi) errors.Add($"ResourceLimit: resolution must be 72–{PdfLimits.MaxRenderDpi} DPI.");
        if (!PdfLimits.IsSafeFileNamePrefix(FileNamePrefix))
            errors.Add("InvalidInput: use a filename prefix of 1–64 safe characters.");
        return errors;
    }
}

public static class PdfLimits
{
    internal static bool IsSafeFileNamePrefix(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 64 &&
        !value.EndsWith(' ') && !value.EndsWith('.') &&
        !value.Any(char.IsControl) &&
        value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        value.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) < 0;

    public const long MaxInputBytes = 100L * 1024 * 1024;
    public const long MaxTotalInputBytes = 200L * 1024 * 1024;
    public const long MaxOutputBytes = 128L * 1024 * 1024;
    public const long MaxTotalOutputBytes = 256L * 1024 * 1024;
    public const int MaxPages = 200;
    public const int MaxMergeFiles = 10;
    public const int MaxSelectedPages = 20;
    public const int MaxRenderedPages = 10;
    public const int MaxRenderDpi = 300;
    public const int MaxTextCharacters = 1_000_000;
    public const int MaxProcessOutputBytes = 2 * 1024 * 1024;
    public static TimeSpan OperationTimeout => TimeSpan.FromSeconds(60);
}

public sealed record PdfPageSize(int Page, double WidthPoints, double HeightPoints);
public sealed record InspectedPdf(int? PageCount, IReadOnlyList<PdfPageSize> PageSizes, bool IsEncrypted, IReadOnlyList<string> SupportedOperations, string Status);
public sealed record PdfOutputPreview(string Operation, IReadOnlyList<string> InputOrder, IReadOnlyList<string> OutputPaths, int PageCount);
public sealed record PdfProgress(string Stage, int CompletedPages, int TotalPages);
public sealed record MergedPdf(string OutputPath, long SizeBytes, int PageCount);
public sealed record SplitPdf(IReadOnlyList<string> OutputPaths, int PageCount);
public sealed record PdfTextPageStatus(int Page, string Status);
public sealed record ExtractedPdfText(string OutputPath, IReadOnlyList<PdfTextPageStatus> PageStatuses, int CharacterCount, long SizeBytes)
{
    public IReadOnlyList<int> PagesWithText => PageStatuses.Where(page => page.Status == "text_available").Select(page => page.Page).ToArray();
    public IReadOnlyList<int> PagesNeedingOcr => PageStatuses.Where(page => page.Status == "needs_ocr").Select(page => page.Page).ToArray();
}
public sealed record RenderedPdfPages(IReadOnlyList<string> OutputPaths, PdfImageFormat Format, int Dpi);

public sealed class PdfApproval(PdfOperationPreview preview)
{
    internal PdfOperationPreview Preview { get; } = preview ?? throw new ArgumentNullException(nameof(preview));
}
