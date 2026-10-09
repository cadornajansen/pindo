using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;

namespace Pointly.App.Voice;

public sealed class ElevenLabsTextToSpeech : ITextToSpeech
{
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly string _voiceId = Environment.GetEnvironmentVariable("POINTLY_VOICE_ID") is { Length: > 0 } voice
        ? voice : "JBFqnCBsd6RMkjVDRZzb";
    public string ModelId { get; } = Environment.GetEnvironmentVariable("POINTLY_TTS_MODEL") is { Length: > 0 } model
        ? model : "eleven_flash_v2_5";

    public async IAsyncEnumerable<byte[]> StreamAsync(string text,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string key = ElevenLabsCredentials.GetApiKey("TTS");
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://api.elevenlabs.io/v1/text-to-speech/{Uri.EscapeDataString(_voiceId)}/stream?output_format=pcm_16000")
        {
            Content = JsonContent.Create(new { text, model_id = ModelId })
        };
        request.Headers.Add("xi-api-key", key);
        using HttpResponseMessage response = await _client.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new VoiceException("TTS", $"Http{(int)response.StatusCode}");
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        while (true)
        {
            byte[] chunk = new byte[4096];
            int count = await stream.ReadAsync(chunk, cancellationToken);
            if (count == 0) break;
            if (count != chunk.Length) Array.Resize(ref chunk, count);
            yield return chunk;
        }
    }

    public void Dispose() => _client.Dispose();
}
