namespace Pointly.App.Voice;

internal static class ElevenLabsCredentials
{
    internal static string GetApiKey(string stage,
        Func<string, EnvironmentVariableTarget, string?>? readVariable = null)
    {
        readVariable ??= Environment.GetEnvironmentVariable;
        const string name = "ELEVENLABS_API_KEY";
        string? key = readVariable(name, EnvironmentVariableTarget.Process);
        // A running launcher may have inherited its environment before the user saved the key.
        if (string.IsNullOrWhiteSpace(key))
            key = readVariable(name, EnvironmentVariableTarget.User);
        if (string.IsNullOrWhiteSpace(key))
            throw new VoiceException(stage, "MissingElevenLabsApiKey");
        return key;
    }
}
