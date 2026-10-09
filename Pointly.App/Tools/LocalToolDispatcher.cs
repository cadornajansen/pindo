using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using LocalTutor.Core.Tools;
using LocalTutor.Tools.Compression;
using LocalTutor.Tools.FileInspectionAndConversion;
using LocalTutor.Tools.FileOrganization;
using LocalTutor.Tools.Pdf;
using LocalTutor.Tools.VideoDownload;
using LocalTutor.Tools.ZipArchive;
using Pointly.App.Tutor;

namespace Pointly.App.Tools;

public sealed class LocalToolDispatcher(ToolDependencies dependencies)
{
    private static readonly IReadOnlyDictionary<string, Type> Inputs = new Dictionary<string, Type>
    {
        ["file.inspect"] = typeof(InspectFileInput), ["file.convert_image"] = typeof(ConvertImageInput),
        ["file.compress_image"] = typeof(CompressImageInput), ["video.inspect_url"] = typeof(InspectVideoInput),
        ["video.download"] = typeof(DownloadVideoInput), ["archive.create_zip"] = typeof(CreateZipInput),
        ["archive.extract_zip"] = typeof(ExtractZipInput), ["pdf.inspect"] = typeof(InspectPdfInput),
        ["pdf.merge"] = typeof(MergePdfInput), ["pdf.split"] = typeof(SplitPdfInput),
        ["pdf.extract_text"] = typeof(ExtractPdfTextInput), ["pdf.render_pages"] = typeof(RenderPdfPagesInput),
        ["file.organize_preview"] = typeof(OrganizePreviewInput), ["file.organize_apply"] = typeof(OrganizeApplyInput),
        ["file.find_duplicates"] = typeof(FindDuplicatesInput)
    };

    public static object Catalog => Inputs.Select(entry => new
    {
        id = entry.Key,
        arguments = entry.Value.GetProperties().Select(property => new
        {
            name = JsonNamingPolicy.CamelCase.ConvertName(property.Name),
            type = Describe(property.PropertyType)
        }),
        note = entry.Key == "file.organize_apply" ? "Host-only continuation of an existing reviewed organization plan; propose file.organize_preview instead." :
            "Use only selected paths. Output paths must be new files inside the selected workspace."
    }).ToArray();

    private static string Describe(Type type)
    {
        Type actual = Nullable.GetUnderlyingType(type) ?? type;
        if (actual.IsEnum) return string.Join("|", Enum.GetNames(actual));
        if (actual.IsArray) return "array of " + Describe(actual.GetElementType()!);
        if (actual == typeof(OrganizationRule)) return "{kind: Extension|ModifiedDate|LessonLabel,destinationDirectory,extension?,fromDate?,throughDate?,lessonLabel?}";
        return actual.Name;
    }

    public static ToolInput Parse(ToolProposal proposal)
    {
        if (!Inputs.TryGetValue(proposal.ToolId, out Type? type)) throw new InvalidOperationException("That tool is not available.");
        var input = (ToolInput?)proposal.Arguments.Deserialize(type, OpenRouterPlanner.Json)
            ?? throw new InvalidOperationException("Missing tool arguments.");
        IReadOnlyList<string> errors = input.Validate();
        if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
        return input;
    }

    private ImageMagickCodec Images() => new(ToolDependencies.RequireFile(dependencies.ImageMagick, "ImageMagick"));
    private PopplerPdfTools Pdf() => new(dependencies.Poppler ?? throw new InvalidOperationException("Configure LOCAL_TUTOR_POPPLER first."));
    private YtDlpClient Video() => new(ToolDependencies.RequireFile(dependencies.YtDlp, "yt-dlp"), dependencies.Ffmpeg);
    private static T Require<T>(ToolResult<T> result) => result.Success && result.Value is not null
        ? result.Value : throw new InvalidOperationException(result.Error ?? "The operation failed.");
    private static async Task<object> Execute<T>(Task<ToolResult<T>> task)
    {
        ToolResult<T> result = await task;
        if (!result.Success && result.Value is null) throw new InvalidOperationException(result.Error ?? "The operation failed.");
        // Partial organization failures include the completed moves and reverse log in Value.
        return result;
    }

    public async Task<ToolReview> PrepareAsync(ToolProposal proposal, ToolSelection selected, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ToolInput parsed = Parse(proposal);
        FileAccessScope Files(params string[] outputs) => new(selected.Workspace, selected.Files, outputs);
        PdfAccessScope PdfScope(params string[] outputs) => new(selected.Workspace, selected.Files, outputs);
        ArchiveAccessScope Archive(params string[] outputs) => new(selected.Workspace, selected.Files.Concat(selected.Folders), outputs);
        void RequireUrl(string url)
        {
            if (!selected.ApprovedUrls.Contains(url, StringComparer.Ordinal))
                throw new InvalidOperationException("Paste the exact video URL in your request before inspecting or downloading it.");
        }

        switch (parsed)
        {
            case InspectFileInput input:
                return new("Inspect file", input, ct => Execute(new InspectFileTool(Files(),
                    dependencies.ImageMagick is null ? null : Images()).ExecuteAsync(input, ct)));
            case ConvertImageInput input:
            {
                FileAccessScope scope = Files(input.OutputPath);
                // Inspection resolves the exact selected input before reading a source fingerprint.
                var metadata = Require(await new InspectFileTool(scope, Images()).ExecuteAsync(new(input.InputPath), token));
                string full = Path.GetFullPath(input.InputPath, selected.Workspace);
                byte[] hash = await HashAsync(full, token);
                return new("Convert image", new { input, metadata, note = "Creates a new image; the original stays unchanged." }, async ct =>
                {
                    byte[] currentHash = await HashAsync(full, ct);
                    if (!hash.SequenceEqual(currentHash)) throw new InvalidOperationException("The source changed. Request a fresh preview.");
                    return await Execute(new ConvertImageTool(scope, Images()).ExecuteAsync(input, ct));
                });
            }
            case CompressImageInput input:
            {
                ImageCompressionPreview preview = Require(await CompressImageTool.PreviewAsync(Files(input.OutputPath), Images(), input, token));
                return new("Compress PNG", new { input, preview.Summary },
                    ct => Execute(new CompressImageTool(new ImageCompressionApproval(preview)).ExecuteAsync(input, ct)), preview);
            }
            case CreateZipInput input:
            {
                ArchivePreview preview = Require(await CreateZipTool.PreviewAsync(Archive(input.OutputPath), input, token));
                return new("Create ZIP", preview, ct => Execute(new CreateZipTool(new ArchiveApproval(preview)).ExecuteAsync(input, ct)), preview);
            }
            case ExtractZipInput input:
            {
                ArchivePreview preview = Require(await ExtractZipTool.PreviewAsync(Archive(input.OutputDirectory), input, token));
                return new("Extract ZIP", preview, ct => Execute(new ExtractZipTool(new ArchiveApproval(preview)).ExecuteAsync(input, ct)), preview);
            }
            case InspectPdfInput input:
                return new("Inspect PDF", input, ct => Execute(new InspectPdfTool(PdfScope(), Pdf()).ExecuteAsync(input, ct)));
            case MergePdfInput input:
            {
                PdfOperationPreview preview = Require(await MergePdfTool.PreviewAsync(PdfScope(input.OutputPath), Pdf(), input, token));
                return new("Merge PDFs", preview.Details, ct => Execute(new MergePdfTool(new PdfApproval(preview)).ExecuteAsync(input, ct)), preview);
            }
            case SplitPdfInput input:
            {
                InspectedPdf inspected = Require(await new InspectPdfTool(PdfScope(), Pdf()).ExecuteAsync(new(input.InputPath), token));
                int last = input.OnePagePerFile ? inspected.PageCount ?? throw new InvalidOperationException("Unknown page count.") : input.EndPage!.Value;
                int first = input.OnePagePerFile ? 1 : input.StartPage!.Value;
                string directory = Path.GetFullPath(input.OutputDirectory, selected.Workspace);
                string[] outputs = input.OnePagePerFile ? Enumerable.Range(first, last - first + 1)
                    .Select(page => Path.Combine(directory, $"{input.FileNamePrefix}-{page:D3}.pdf")).ToArray()
                    : [Path.Combine(directory, $"{input.FileNamePrefix}-{first:D3}-{last:D3}.pdf")];
                PdfOperationPreview preview = Require(await SplitPdfTool.PreviewAsync(PdfScope(outputs), Pdf(), input, token));
                return new("Split PDF", preview.Details, ct => Execute(new SplitPdfTool(new PdfApproval(preview)).ExecuteAsync(input, ct)), preview);
            }
            case ExtractPdfTextInput input:
            {
                PdfOperationPreview preview = Require(await ExtractPdfTextTool.PreviewAsync(PdfScope(input.OutputPath), Pdf(), input, token));
                return new("Extract PDF text", preview.Details, ct => Execute(new ExtractPdfTextTool(new PdfApproval(preview)).ExecuteAsync(input, ct)), preview);
            }
            case RenderPdfPagesInput input:
            {
                string extension = input.Format == PdfImageFormat.Png ? "png" : "jpg";
                string[] outputs = input.Pages.Order().Select(page => Path.Combine(Path.GetFullPath(input.OutputDirectory, selected.Workspace),
                    $"{input.FileNamePrefix}-{page:D3}.{extension}")).ToArray();
                PdfOperationPreview preview = Require(await RenderPdfPagesTool.PreviewAsync(PdfScope(outputs), Pdf(), input, token));
                return new("Render PDF pages", new { preview.Details, input.Dpi, input.Format },
                    ct => Execute(new RenderPdfPagesTool(new PdfApproval(preview)).ExecuteAsync(input, ct)), preview);
            }
            case InspectVideoInput input:
                RequireUrl(input.Url);
                return new("Inspect video URL", input, ct => Execute(new InspectVideoTool(Video(), input.Url).ExecuteAsync(input, ct)));
            case DownloadVideoInput input:
            {
                RequireUrl(input.Url);
                VideoDownloadPreview preview = Require(await DownloadVideoTool.PreviewAsync(Video(), input, selected.Workspace, token));
                return new("Download video", new { preview.Source, preview.Title, preview.DurationSeconds, preview.DestinationPath,
                    preview.Format, note = "Run confirms you have permission to download this video." },
                    ct => Execute(new DownloadVideoTool(Video(), new VideoDownloadApproval(preview)).ExecuteAsync(input, ct)));
            }
            case OrganizePreviewInput input:
            {
                // The model cannot grant itself access to arbitrary destination folders.
                var scope = new OrganizationAccessScope(selected.Workspace, selected.Files, selected.Folders);
                OrganizationPlan preview = Require(await new OrganizePreviewTool(scope).ExecuteAsync(input, token));
                return new("Organize files", preview,
                    ct => Execute(new OrganizeApplyTool(new OrganizationApproval(preview)).ExecuteAsync(new(preview.PlanId), ct)), preview);
            }
            case OrganizeApplyInput:
                throw new InvalidOperationException("Use Run on the current organization preview. Ask for a new preview if it was closed.");
            case FindDuplicatesInput input:
                return new("Find exact duplicates", input, ct => Execute(new FindDuplicatesTool(
                    new OrganizationAccessScope(selected.Workspace, [], [], selected.Folders)).ExecuteAsync(input, ct)));
            default: throw new InvalidOperationException("That operation is not registered.");
        }
    }

    private static async Task<byte[]> HashAsync(string path, CancellationToken token)
    {
        await using FileStream stream = File.OpenRead(path);
        return await SHA256.HashDataAsync(stream, token);
    }
}
