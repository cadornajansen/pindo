namespace Pointly.App.Vision;

public static class GuiGroundingModelFactory
{
    public static IGuiGroundingModel CreateConfigured(out string? warning)
    {
        string? configured = Environment.GetEnvironmentVariable("POINTLY_GROUNDING_PROVIDER")?.Trim();
        warning = null;
        if (string.IsNullOrWhiteSpace(configured) ||
            configured.Equals("openrouter", StringComparison.OrdinalIgnoreCase))
            return new OpenRouterGroundingModel();
        if (configured.Equals("nova", StringComparison.OrdinalIgnoreCase))
            return new BedrockNovaVisionModel();

        warning = "Invalid POINTLY_GROUNDING_PROVIDER; vision grounding will fail until Pointly is restarted with openrouter or nova.";
        return new InvalidConfigurationModel();
    }

    private sealed class InvalidConfigurationModel : IGuiGroundingModel
    {
        public string ProviderName => "InvalidConfiguration";
        public string ModelId => "none";

        public Task<GuiGroundingResult> GroundAsync(
            GuiGroundingRequest request, CancellationToken cancellationToken) =>
            Task.FromException<GuiGroundingResult>(new GuiGroundingException(
                "POINTLY_GROUNDING_PROVIDER must be 'openrouter' or 'nova'.",
                GroundingFailureReason.Configuration));

        public void Dispose() { }
    }
}
