using System.Security.Cryptography;
using LocalTutor.Core.Tools;
using LocalTutor.Tools.FileInspectionAndConversion;

namespace LocalTutor.Tools.Compression;

public sealed class CompressImageTool(ImageCompressionApproval approval, IProgress<FileToolProgress>? progress = null)
    : LocalTool<CompressImageInput, CompressedImage>
{
    public override string Id => "file.compress_image";
    public override string Description => "Save one approved smaller PNG copy using the previewed Slides1600 or Share800 resize preset. Preserve the source and transparency; report dimensions, bytes and quality tradeoffs. If it is not smaller, save nothing.";

    /// <summary>Prepare a bounded PNG candidate without writing the destination; approval is separate. Clock is trusted host configuration.</summary>
    public static async Task<ToolResult<ImageCompressionPreview>> PreviewAsync(FileAccessScope access, ImageMagickCodec codec,
        CompressImageInput input, CancellationToken cancellationToken = default,
        IProgress<FileToolProgress>? progress = null, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<string> errors = input.Validate();
        if (errors.Count > 0) return new(false, null, string.Join("; ", errors));
        try
        {
            progress?.Report(new("Preparing", 0));
            string source = access.ResolveInput(input.InputPath);
            string output = access.ResolveOutput(input.OutputPath);
            byte[] original = await ImageContent.ReadAsync(source, cancellationToken);
            if (ImageContent.Detect(original) != ImageFormat.Png)
                throw new FileToolException("FormatMismatch: source content must be PNG, matching its extension.");
            if (ImageContent.HasColorProfile(original, ImageFormat.Png))
                throw new FileToolException("UnsupportedColor: embedded ICC profiles require a separately verified color-managed workflow.");
            ImageMetadata before = await codec.InspectAsync(original, ImageFormat.Png, cancellationToken);
            if (before.ColorSpace is not ("sRGB" or "RGB" or "Gray"))
                throw new FileToolException("UnsupportedColor: only RGB, sRGB and gray PNG images are supported.");
            (int width, int height) = input.Preset == ImageCompressionPreset.Slides1600 ? (1600, 900) : (800, 600);
            progress?.Report(new("Encoding", 25));
            byte[] candidate = await codec.ConvertAsync(original, ImageFormat.Png,
                new(input.InputPath, input.OutputPath, ImageFormat.Png, MaxWidth: width, MaxHeight: height), cancellationToken);
            progress?.Report(new("Verifying", 65));
            if (ImageContent.Detect(candidate) != ImageFormat.Png)
                throw new FileToolException("InvalidOutput: the candidate is not PNG content.");
            ImageMetadata after = await codec.InspectAsync(candidate, ImageFormat.Png, cancellationToken);
            if (after.Width > width || after.Height > height || candidate.Length == 0)
                throw new FileToolException("InvalidOutput: the candidate exceeds the preset or is empty.");
            bool smaller = candidate.Length < original.Length;
            List<string> warnings = ["Resizing can remove detail and make small text harder to read. Review the image before sharing.",
                "Output is 8-bit sRGB; orientation is applied and metadata is removed. No lossless optimization claim is made."];
            if (!smaller) warnings.Add("The candidate is not smaller: the source may already be efficient or too small for resizing. No output will be written.");
            ImageCompressionSummary summary = new(input.Preset, original.Length, candidate.Length,
                before.Width, before.Height, after.Width, after.Height, after.Alpha, smaller, warnings.AsReadOnly());
            access.ResolveInput(input.InputPath);
            output = access.ResolveOutput(input.OutputPath);
            progress?.Report(new("Ready", 100));
            cancellationToken.ThrowIfCancellationRequested();
            return new(true, new(input, access, source, output, SHA256.HashData(original),
                smaller ? candidate : [], summary, clock ?? TimeProvider.System));
        }
        catch (FileToolException e) { return new(false, null, e.Message); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return new(false, null, "PreviewFailed: input access, output conflict or local I/O prevented preparation."); }
    }

    protected override async Task<ToolResult<CompressedImage>> ExecuteValidatedAsync(CompressImageInput input, CancellationToken cancellationToken)
    {
        try { return await SaveApprovedAsync(input, cancellationToken); }
        catch (FileToolException e) { return new(false, null, e.Message); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return new(false, null, "CompressionFailed: input access, output conflict or local I/O prevented saving."); }
    }

    private async Task<ToolResult<CompressedImage>> SaveApprovedAsync(CompressImageInput input, CancellationToken cancellationToken)
    {
        string? temporary = null;
        try
        {
            ImageCompressionPreview preview = approval.Preview;
            byte[] candidate = preview.Consume(input);
            progress?.Report(new("CheckingSource", 0));
            await preview.VerifySourceAsync(cancellationToken);
            string output = preview.Paths.ResolveOutput(input.OutputPath);
            cancellationToken.ThrowIfCancellationRequested();
            if (!preview.Summary.IsSmaller)
                return new(true, new(null, ImageCompressionStatus.NoReduction, preview.Summary));
            progress?.Report(new("Saving", 75));
            // Recheck after progress callbacks and before creating any sibling file.
            output = preview.Paths.ResolveOutput(input.OutputPath);
            cancellationToken.ThrowIfCancellationRequested();
            string path = Path.Combine(Path.GetDirectoryName(output)!, ".localtutor-compression-" + Guid.NewGuid().ToString("N") + ".partial");
            await using (FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, FileOptions.Asynchronous))
            {
                temporary = path;
                await stream.WriteAsync(candidate, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            progress?.Report(new("Publishing", 90));
            await preview.VerifySourceAsync(cancellationToken);
            output = preview.Paths.ResolveOutput(input.OutputPath);
            cancellationToken.ThrowIfCancellationRequested();
            // The exact previewed, decoded bytes are published without overwriting. This rename is the commit point.
            File.Move(temporary, output, overwrite: false);
            temporary = null;
            progress?.Report(new("Completed", 100));
            return new(true, new(output, ImageCompressionStatus.Compressed, preview.Summary));
        }
        finally
        {
            if (temporary is not null)
            {
                try { File.Delete(temporary); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    if (cancellationToken.IsCancellationRequested)
                        throw new OperationCanceledException("CanceledWithCleanupWarning: the partial compression output could not be removed.", e, cancellationToken);
                    throw new FileToolException("CleanupFailed: the partial compression output could not be removed; no destination was published.");
                }
            }
        }
    }
}
