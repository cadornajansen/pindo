using LocalTutor.Core.Tools;

namespace LocalTutor.Tools.FileInspectionAndConversion;

public sealed class InspectFileTool(FileAccessScope access, ImageMagickCodec? codec = null) : LocalTool<InspectFileInput, InspectedFile>
{
    public override string Id => "file.inspect";
    public override string Description => "Inspect approved local file metadata without returning its private contents. Image dimensions require a local decoder.";

    protected override async Task<ToolResult<InspectedFile>> ExecuteValidatedAsync(InspectFileInput input, CancellationToken cancellationToken)
    {
        try
        {
            string path = access.ResolveInput(input.InputPath);
            byte[] header = new byte[32];
            long size;
            int count;
            await using (FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous))
            {
                size = stream.Length;
                count = await stream.ReadAsync(header, cancellationToken);
            }
            ImageFormat? format = ImageContent.Detect(header.AsSpan(0, count));
            string extension = Path.GetExtension(path).ToLowerInvariant();
            List<string> warnings = [];
            if (format is { } imageFormat)
            {
                if (!ImageContent.ExtensionMatches(path, imageFormat)) throw new FileToolException("FormatMismatch: image content does not match its extension.");
                if (codec is null)
                    return new(true, new(ImageContent.Coder(imageFormat), extension, size, null, null, null, null, "SignatureOnly", [], ["Image decoding and conversion require a configured local ImageMagick dependency."]));
                byte[] bytes = await ImageContent.ReadAsync(path, cancellationToken);
                ImageMetadata metadata = await codec.InspectAsync(bytes, imageFormat, cancellationToken);
                if (metadata.HasColorProfile) warnings.Add("Embedded ICC profiles are not supported for conversion.");
                if (metadata.ColorSpace is not ("sRGB" or "RGB" or "Gray")) warnings.Add("This color space is not supported for conversion.");
                bool convertible = !metadata.HasColorProfile && metadata.ColorSpace is "sRGB" or "RGB" or "Gray";
                return new(true, new(ImageContent.Coder(imageFormat), extension, bytes.Length, metadata.Width, metadata.Height, null, null, "Decoded", convertible ? ["file.convert_image"] : [], warnings));
            }
            if (extension is ".png" or ".jpg" or ".jpeg" or ".webp")
                throw new FileToolException("InvalidImage: no supported image signature was found.");
            string type = DetectOther(header.AsSpan(0, count));
            cancellationToken.ThrowIfCancellationRequested();
            warnings.Add("Only file metadata and a bounded signature were inspected; document page count and media duration are unavailable in this slice.");
            return new(true, new(type, extension, size, null, null, null, null, "SignatureOnly", [], warnings));
        }
        catch (FileToolException e) { return new(false, null, e.Message); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return new(false, null, "FileAccessFailed: the approved file could not be inspected safely."); }
    }

    private static string DetectOther(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith("%PDF-"u8)) return "PDF";
        if (header.StartsWith("PK\u0003\u0004"u8)) return "ZIP container (Office subtype unverified)";
        if (header.Length >= 12 && header.StartsWith("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WAVE"u8)) return "WAV";
        if (header.Length >= 12 && header.Slice(4, 4).SequenceEqual("ftyp"u8)) return "ISO base media container";
        if (header.StartsWith(new byte[] { 0x1a, 0x45, 0xdf, 0xa3 })) return "EBML container";
        if (header.StartsWith("ID3"u8)) return "ID3-tagged audio (codec unverified)";
        return "Unknown";
    }
}
