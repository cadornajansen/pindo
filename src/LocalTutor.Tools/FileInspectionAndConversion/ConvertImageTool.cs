using LocalTutor.Core.Tools;

namespace LocalTutor.Tools.FileInspectionAndConversion;

public sealed class ConvertImageTool(FileAccessScope access, ImageMagickCodec codec, IProgress<FileToolProgress>? progress = null) : LocalTool<ConvertImageInput, ConvertedImage>
{
    public override string Id => "file.convert_image";
    public override string Description => "Convert one approved still PNG, JPEG, or WebP to a new named image for classroom slides; preserve the source.";

    protected override async Task<ToolResult<ConvertedImage>> ExecuteValidatedAsync(ConvertImageInput input, CancellationToken cancellationToken)
    {
        string? temporary = null;
        try
        {
            progress?.Report(new("Preflight", 0));
            string source = access.ResolveInput(input.InputPath);
            string output = access.ResolveOutput(input.OutputPath);
            byte[] bytes = await ImageContent.ReadAsync(source, cancellationToken);
            ImageFormat format = ImageContent.Detect(bytes) ?? throw new FileToolException("UnsupportedInput: only PNG, JPEG, and WebP image content is accepted.");
            if (!ImageContent.ExtensionMatches(source, format)) throw new FileToolException("FormatMismatch: source content does not match its extension.");
            if (ImageContent.HasColorProfile(bytes, format))
                throw new FileToolException("UnsupportedColor: embedded ICC profiles require a separately verified color-managed workflow.");
            ImageMetadata metadata = await codec.InspectAsync(bytes, format, cancellationToken);
            if (metadata.HasColorProfile || metadata.ColorSpace is not ("sRGB" or "RGB" or "Gray"))
                throw new FileToolException("UnsupportedColor: ICC-profiled and non-RGB/gray images require a separately verified color-managed workflow.");
            progress?.Report(new("Converting", 25));
            cancellationToken.ThrowIfCancellationRequested();
            byte[] converted = await codec.ConvertAsync(bytes, format, input, cancellationToken);
            progress?.Report(new("Verifying", 65));
            if (ImageContent.Detect(converted) != input.Format) throw new FileToolException("InvalidOutput: encoded content does not match the requested format.");
            ImageMetadata verified = await codec.InspectAsync(converted, input.Format, cancellationToken);
            if (input.MaxWidth is { } maxWidth && (verified.Width > maxWidth || verified.Height > input.MaxHeight))
                throw new FileToolException("InvalidOutput: the image exceeds the requested resize box.");
            if (input.Transparency == TransparencyMode.FlattenWhite && verified.Alpha)
                throw new FileToolException("InvalidOutput: transparency was not flattened.");
            // All decoding, encoding and readability checks finish before any file is written.
            access.ResolveInput(input.InputPath);
            output = access.ResolveOutput(input.OutputPath);
            progress?.Report(new("Saving", 90));
            cancellationToken.ThrowIfCancellationRequested();
            output = access.ResolveOutput(input.OutputPath);
            string candidate = Path.Combine(Path.GetDirectoryName(output)!, ".localtutor-" + Guid.NewGuid().ToString("N") + ".partial");
            await using (FileStream stream = new(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, FileOptions.Asynchronous))
            {
                temporary = candidate;
                await stream.WriteAsync(converted, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            output = access.ResolveOutput(input.OutputPath);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, output, overwrite: false);
            temporary = null;
            // The rename is the commit point: cancellation afterwards cannot undo the saved image.
            progress?.Report(new("Completed", 100));
            return new(true, new(output, input.Format, converted.Length, verified.Width, verified.Height, verified.Alpha,
                "Converted to 8-bit sRGB; embedded ICC profiles are rejected.",
                ["EXIF orientation is applied and metadata is removed. JPEG and WebP RGB encoding may lose detail; review the image before teaching.",
                 input.Transparency == TransparencyMode.FlattenWhite ? "Transparency was composited onto white." : "Transparency is preserved when supported by the output codec."]));
        }
        catch (FileToolException e) { return new(false, null, e.Message); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return new(false, null, "FileConversionFailed: input access, output conflict, or local I/O prevented a safe conversion."); }
        finally
        {
            if (temporary is not null)
            {
                try { File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
