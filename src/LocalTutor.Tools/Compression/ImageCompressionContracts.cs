using System.Security.Cryptography;
using System.Text.Json.Serialization;
using LocalTutor.Core.Tools;
using LocalTutor.Tools.FileInspectionAndConversion;

namespace LocalTutor.Tools.Compression;

[JsonConverter(typeof(ImageCompressionPresetConverter))]
public enum ImageCompressionPreset { Slides1600, Share800 }

public sealed class ImageCompressionPresetConverter() : JsonStringEnumConverter<ImageCompressionPreset>(allowIntegerValues: false);

[JsonConverter(typeof(ImageCompressionStatusConverter))]
public enum ImageCompressionStatus { Compressed, NoReduction }

public sealed class ImageCompressionStatusConverter() : JsonStringEnumConverter<ImageCompressionStatus>(allowIntegerValues: false);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CompressImageInput(
    [property: JsonRequired] string InputPath,
    [property: JsonRequired] string OutputPath,
    [property: JsonRequired] ImageCompressionPreset Preset) : ToolInput
{
    public override IReadOnlyList<string> Validate()
    {
        List<string> errors = [.. FileAccessScope.ValidatePath(InputPath), .. FileAccessScope.ValidatePath(OutputPath)];
        if (!ImageContent.ExtensionMatches(InputPath, ImageFormat.Png) || !ImageContent.ExtensionMatches(OutputPath, ImageFormat.Png))
            errors.Add("UnsupportedFormat: this demo slice supports PNG to PNG only; JPEG and WebP compression are planned.");
        if (!Enum.IsDefined(Preset)) errors.Add("UnsupportedPreset: choose Slides1600 or Share800.");
        return errors;
    }
}

public sealed record ImageCompressionSummary(
    ImageCompressionPreset Preset, long OriginalBytes, long NewBytes,
    int OriginalWidth, int OriginalHeight, int NewWidth, int NewHeight,
    bool HasAlphaChannel, bool IsSmaller, IReadOnlyList<string> Warnings);

public sealed record CompressedImage(string? OutputPath, ImageCompressionStatus Status, ImageCompressionSummary Summary);

/// <summary>Host-owned preview; display paths, all summary fields and warnings before explicit approval. Dispose abandoned previews.</summary>
public sealed class ImageCompressionPreview : IDisposable
{
    private readonly object gate = new();
    private byte[]? candidate;
    private readonly byte[] sourceHash;
    private readonly DateTimeOffset createdAt;
    private readonly TimeProvider clock;

    public string InputPath { get; }
    public string OutputPath { get; }
    public ImageCompressionSummary Summary { get; }
    public DateTimeOffset ExpiresAt => createdAt.AddMinutes(5);
    internal CompressImageInput Input { get; }
    internal FileAccessScope Paths { get; }

    internal ImageCompressionPreview(CompressImageInput input, FileAccessScope paths, string source, string output,
        byte[] sourceHash, byte[] candidate, ImageCompressionSummary summary, TimeProvider clock)
    {
        Input = input; Paths = paths; InputPath = source; OutputPath = output;
        this.sourceHash = sourceHash; this.candidate = candidate; Summary = summary;
        this.clock = clock; createdAt = clock.GetUtcNow();
    }

    internal byte[] Consume(CompressImageInput input)
    {
        lock (gate)
        {
            if (input != Input) throw new FileToolException("ApprovalMismatch: approve the exact source, output and preset in a fresh preview.");
            if (clock.GetUtcNow() >= ExpiresAt)
            {
                candidate = null;
                throw new FileToolException("ApprovalExpired: prepare and approve a fresh preview.");
            }
            byte[] bytes = candidate ?? throw new FileToolException("ApprovalConsumed: prepare a fresh preview after every attempt or disposal.");
            candidate = null;
            return bytes;
        }
    }

    internal async Task VerifySourceAsync(CancellationToken cancellationToken)
    {
        string source = Paths.ResolveInput(Input.InputPath);
        byte[] current = await ImageContent.ReadAsync(source, cancellationToken);
        if (!CryptographicOperations.FixedTimeEquals(sourceHash, SHA256.HashData(current)))
            throw new FileToolException("SourceChanged: prepare and approve a fresh preview of the current source.");
    }

    public void Dispose() { lock (gate) candidate = null; }
}

/// <summary>Trusted host consent, never a model argument. Construct only after explicit user approval of the displayed preview.</summary>
public sealed class ImageCompressionApproval(ImageCompressionPreview preview)
{
    internal ImageCompressionPreview Preview { get; } = preview ?? throw new ArgumentNullException(nameof(preview));
}
