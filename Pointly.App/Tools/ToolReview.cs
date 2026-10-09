using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Pointly.App.Tutor;

namespace Pointly.App.Tools;

/// <summary>A host-owned, expiring, single-use action. Only the review UI may call RunAsync.</summary>
public sealed class ToolReview(string title, object details, Func<CancellationToken, Task<object>> run, IDisposable? preview = null) : IDisposable
{
    private int _consumed;
    private readonly DateTimeOffset _expires = DateTimeOffset.UtcNow.AddMinutes(5);
    public string Title { get; } = title;
    public string Details { get; } = Format(details);
    public async Task<object> RunAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (DateTimeOffset.UtcNow >= _expires || Interlocked.Exchange(ref _consumed, 1) != 0)
            throw new InvalidOperationException("This preview expired or was already used. Ask for a fresh preview.");
        return await run(token);
    }
    public void Dispose() { Interlocked.Exchange(ref _consumed, 1); preview?.Dispose(); }

    public static string Format(object value)
    {
        JsonElement json = JsonSerializer.SerializeToElement(value, OpenRouterPlanner.Json);
        var text = new StringBuilder();
        void Write(JsonElement item, int depth)
        {
            string indent = new(' ', Math.Min(depth, 6) * 2);
            if (item.ValueKind == JsonValueKind.Object)
                foreach (JsonProperty property in item.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Null) continue;
                    string label = Regex.Replace(property.Name, "([a-z])([A-Z])", "$1 $2");
                    text.Append(indent).Append(char.ToUpperInvariant(label[0])).Append(label.AsSpan(1)).Append(": ");
                    if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array) { text.AppendLine(); Write(property.Value, depth + 1); }
                    else text.AppendLine(property.Value.ToString());
                }
            else if (item.ValueKind == JsonValueKind.Array)
                foreach (JsonElement child in item.EnumerateArray()) { text.Append(indent).AppendLine("•"); Write(child, depth + 1); }
            else text.Append(indent).AppendLine(item.ToString());
        }
        Write(json, 0);
        return text.ToString();
    }
}

public sealed record ToolSelection(string Workspace, string[] Files, string[] Folders, string[] ApprovedUrls);

public sealed record ToolDependencies(string? ImageMagick, string? Poppler, string? YtDlp, string? Ffmpeg)
{
    private static string? Read(string name) => Environment.GetEnvironmentVariable(name) ?? Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
    public static ToolDependencies Load() => new(Read("LOCAL_TUTOR_IMAGEMAGICK"), Read("LOCAL_TUTOR_POPPLER"), Read("LOCAL_TUTOR_YTDLP"), Read("LOCAL_TUTOR_FFMPEG"));
    public static string RequireFile(string? path, string label) => path is not null && Path.IsPathFullyQualified(path) && File.Exists(path)
        ? path : throw new InvalidOperationException($"{label} is not configured. Run scripts/Setup-ToolDependencies.ps1.");
}
