namespace LocalTutor.Desktop.Services;

public sealed record OllamaSettings
{
    public Uri BaseUrl { get; init; } = new("http://localhost:11434/");
    public string Model { get; init; } = "qwen3:1.7b";
}
