using Pointly.App.Voice;

namespace Pointly.Tests;

public sealed class ElevenLabsCredentialsTests
{
    [Fact]
    public void ProcessOverrideWinsWithoutReadingSavedKey()
    {
        string key = ElevenLabsCredentials.GetApiKey("STT", (name, target) =>
        {
            Assert.Equal("ELEVENLABS_API_KEY", name);
            Assert.Equal(EnvironmentVariableTarget.Process, target);
            return "test-process-value";
        });
        Assert.Equal("test-process-value", key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void MissingProcessValueReadsSavedUserKey(string? processValue)
    {
        var reads = new List<EnvironmentVariableTarget>();
        string key = ElevenLabsCredentials.GetApiKey("TTS", (name, target) =>
        {
            Assert.Equal("ELEVENLABS_API_KEY", name);
            reads.Add(target);
            return target == EnvironmentVariableTarget.Process ? processValue : "test-user-value";
        });
        Assert.Equal("test-user-value", key);
        Assert.Equal(new[] { EnvironmentVariableTarget.Process, EnvironmentVariableTarget.User }, reads);
    }

    [Theory]
    [InlineData("STT")]
    [InlineData("TTS")]
    public void MissingBothValuesFailsSafely(string stage)
    {
        VoiceException error = Assert.Throws<VoiceException>(() =>
            ElevenLabsCredentials.GetApiKey(stage, (_, _) => null));
        Assert.Equal("MissingElevenLabsApiKey", error.Message);
    }

    [Fact]
    public void SavedKeyIsReadAgainForNextRequestWithoutRestart()
    {
        string? saved = null;
        string? Read(string name, EnvironmentVariableTarget target) =>
            target == EnvironmentVariableTarget.User ? saved : null;
        Assert.Throws<VoiceException>(() => ElevenLabsCredentials.GetApiKey("STT", Read));
        saved = "test-new-value";
        Assert.Equal(saved, ElevenLabsCredentials.GetApiKey("STT", Read));
        saved = "test-replaced-value";
        Assert.Equal(saved, ElevenLabsCredentials.GetApiKey("TTS", Read));
    }
}
