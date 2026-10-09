using System.Text.Json.Serialization;
using LocalTutor.Core.Tools;

namespace LocalTutor.Tools.FileInspectionAndConversion;

[JsonConverter(typeof(JsonStringEnumConverter<ImageFormat>))]
public enum ImageFormat { Png, Jpeg, WebP }

[JsonConverter(typeof(JsonStringEnumConverter<TransparencyMode>))]
public enum TransparencyMode { Preserve, FlattenWhite }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InspectFileInput([property: JsonRequired] string InputPath) : ToolInput
{
    public override IReadOnlyList<string> Validate() => FileAccessScope.ValidatePath(InputPath);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConvertImageInput(
    [property: JsonRequired] string InputPath,
    [property: JsonRequired] string OutputPath,
    [property: JsonRequired] ImageFormat Format,
    TransparencyMode Transparency = TransparencyMode.Preserve,
    int? MaxWidth = null,
    int? MaxHeight = null,
    int? Quality = null) : ToolInput
{
    public override IReadOnlyList<string> Validate()
    {
        List<string> errors = [.. FileAccessScope.ValidatePath(InputPath), .. FileAccessScope.ValidatePath(OutputPath)];
        if (!Enum.IsDefined(Format) || !Enum.IsDefined(Transparency)) errors.Add("Unsupported image or transparency setting.");
        if (MaxWidth.HasValue != MaxHeight.HasValue || MaxWidth is < 1 or > 8192 || MaxHeight is < 1 or > 8192)
            errors.Add("Resize requires both maximum dimensions, each from 1 to 8192.");
        if (Quality is < 1 or > 100) errors.Add("Quality must be from 1 to 100.");
        if (Format == ImageFormat.Png && Quality.HasValue) errors.Add("PNG does not accept a lossy quality setting.");
        if (Format == ImageFormat.Jpeg && Transparency != TransparencyMode.FlattenWhite)
            errors.Add("JPEG requires explicit FlattenWhite because it cannot preserve transparency.");
        if (!ImageContent.ExtensionMatches(OutputPath, Format)) errors.Add("Output extension must match the selected format.");
        return errors;
    }
}

// No names, paths, document text, EXIF values, thumbnails, or raw native diagnostics.
public sealed record InspectedFile(
    string DetectedType,
    string Extension,
    long SizeBytes,
    int? Width,
    int? Height,
    int? PageCount,
    double? DurationSeconds,
    string Verification,
    IReadOnlyList<string> SupportedNextActions,
    IReadOnlyList<string> Warnings);

public sealed record ConvertedImage(
    string OutputPath,
    ImageFormat Format,
    long SizeBytes,
    int Width,
    int Height,
    bool HasAlphaChannel,
    string ColorHandling,
    IReadOnlyList<string> Warnings);

public sealed record FileToolProgress(string Stage, int Percent);

internal sealed class FileToolException(string message) : Exception(message);
