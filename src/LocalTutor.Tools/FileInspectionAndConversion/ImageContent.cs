using System.Buffers.Binary;

namespace LocalTutor.Tools.FileInspectionAndConversion;

internal static class ImageContent
{
    internal const int MaxInputBytes = 20 * 1024 * 1024;
    internal const int MaxOutputBytes = 32 * 1024 * 1024;
    internal const long MaxPixels = 4_000_000;

    internal static ImageFormat? Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return ImageFormat.Png;
        if (bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255) return ImageFormat.Jpeg;
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8)) return ImageFormat.WebP;
        return null;
    }

    internal static bool ExtensionMatches(string? path, ImageFormat format)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        string extension = Path.GetExtension(path).ToLowerInvariant();
        return format switch { ImageFormat.Png => extension == ".png", ImageFormat.Jpeg => extension is ".jpg" or ".jpeg", ImageFormat.WebP => extension == ".webp", _ => false };
    }

    internal static string Coder(ImageFormat format) => format switch
    {
        ImageFormat.Png => "PNG", ImageFormat.Jpeg => "JPEG", ImageFormat.WebP => "WEBP",
        _ => throw new FileToolException("UnsupportedFormat: choose PNG, JPEG, or WebP.")
    };

    internal static void RejectAnimation(ReadOnlySpan<byte> bytes, ImageFormat format)
    {
        // Detect animation chunks even when the installed decoder would silently read only frame 1.
        if (format == ImageFormat.Jpeg) return;
        int offset = format == ImageFormat.Png ? 8 : 12;
        while (offset <= bytes.Length - 8)
        {
            uint length = format == ImageFormat.Png ? BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4)) : BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset + 4, 4));
            ReadOnlySpan<byte> type = bytes.Slice(offset + (format == ImageFormat.Png ? 4 : 0), 4);
            if (type.SequenceEqual("acTL"u8) || type.SequenceEqual("ANIM"u8) || type.SequenceEqual("ANMF"u8))
                throw new FileToolException("UnsupportedAnimation: only single still images are supported.");
            long next = offset + 8L + length + (format == ImageFormat.Png ? 4 : length % 2);
            if (next > bytes.Length) break; // Native full decode reports corrupt/truncated image data.
            offset = (int)next;
        }
    }

    internal static bool HasColorProfile(ReadOnlySpan<byte> bytes, ImageFormat format)
    {
        if (format == ImageFormat.Jpeg) return bytes.IndexOf("ICC_PROFILE\0"u8) >= 0;
        int offset = format == ImageFormat.Png ? 8 : 12;
        while (offset <= bytes.Length - 8)
        {
            uint length = format == ImageFormat.Png ? BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4)) : BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset + 4, 4));
            ReadOnlySpan<byte> type = bytes.Slice(offset + (format == ImageFormat.Png ? 4 : 0), 4);
            if (type.SequenceEqual("iCCP"u8) || type.SequenceEqual("ICCP"u8)) return true;
            long next = offset + 8L + length + (format == ImageFormat.Png ? 4 : length % 2);
            if (next > bytes.Length) break;
            offset = (int)next;
        }
        return false;
    }

    internal static async Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.Asynchronous);
        if (stream.Length is < 1 or > MaxInputBytes) throw new FileToolException("ResourceLimit: images must be nonempty and at most 20 MiB.");
        byte[] data = new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(data, cancellationToken);
        return data;
    }
}
