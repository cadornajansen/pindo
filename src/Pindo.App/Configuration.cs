namespace Pindo.App;

public static class Configuration
{
    public static string? Read(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value)) value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static void Migrate()
    {
        // Compatibility is confined to migration; runtime code uses only the Pindo keys.
        foreach (var (oldName, newName) in new[]
        {
            ("POINTLY_OPENROUTER_MODEL", "PINDO_OPENROUTER_MODEL"),
            ("LOCAL_TUTOR_IMAGEMAGICK", "PINDO_IMAGEMAGICK"),
            ("LOCAL_TUTOR_POPPLER", "PINDO_POPPLER"),
            ("LOCAL_TUTOR_YTDLP", "PINDO_YTDLP"),
            ("LOCAL_TUTOR_FFMPEG", "PINDO_FFMPEG")
        })
        {
            if (Read(newName) is not null || Read(oldName) is not { } value) continue;
            Environment.SetEnvironmentVariable(newName, value, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable(newName, value);
        }
    }
}
