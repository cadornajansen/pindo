namespace Pindo.App;

public static class Configuration
{
    public static string? Read(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value)) value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
