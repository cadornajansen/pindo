using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalTutor.Desktop.Services;

public sealed class OllamaAvailabilityClient(HttpClient httpClient, OllamaSettings settings) : IOllamaClient
{
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            VersionResponse? response = await httpClient.GetFromJsonAsync<VersionResponse>(
                new Uri(settings.BaseUrl, "api/version"), cancellationToken);
            return !string.IsNullOrWhiteSpace(response?.Version);
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private sealed record VersionResponse([property: JsonPropertyName("version")] string Version);
}
