namespace Pointly.App.Vision;

public sealed class GuiGroundingService(IGuiGroundingModel model)
{
    public const double MinimumConfidence = 0.65;

    public async Task<GuiGroundingResult> GroundAsync(
        GuiGroundingRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ScreenshotPng.Length == 0 ||
            request.ScreenshotWidth <= 0 ||
            request.ScreenshotHeight <= 0)
        {
            throw new ArgumentException("Vision request has no valid screenshot.", nameof(request));
        }

        GuiGroundingResult response = await model.GroundAsync(request, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(response);
        return response;
    }

    private static void Validate(GuiGroundingResult response)
    {
        if (response.Point is null && response.BoundingBox is null)
            throw new GuiGroundingException("No visible grounding target was returned.", GroundingFailureReason.NoTarget);

        if (string.IsNullOrWhiteSpace(response.TargetLabel) || response.TargetLabel.Length > 200 ||
            response.Description?.Length > 1_000 || response.Instruction?.Length > 1_000 ||
            (response.Instruction is not null && string.IsNullOrWhiteSpace(response.Instruction)))
        {
            throw new GuiGroundingException("Grounding response has invalid text fields.");
        }

        if (response.Confidence is double confidence &&
            (!double.IsFinite(confidence) || confidence < 0 || confidence > 1))
        {
            throw new GuiGroundingException("Grounding confidence must be between 0 and 1.");
        }

        if (response.Confidence is double lowConfidence && lowConfidence < MinimumConfidence)
        {
            throw new GuiGroundingException(
                $"Grounding confidence {lowConfidence:F2} is below the {MinimumConfidence:F2} highlight threshold.",
                GroundingFailureReason.LowConfidence);
        }

        if (response.Point is { } point &&
            (!ValidCoordinate(point.X) || !ValidCoordinate(point.Y)))
            throw new GuiGroundingException("Grounding point must be within 0..1000.");

        if (response.BoundingBox is { } box &&
            (!ValidCoordinate(box.X1) || !ValidCoordinate(box.Y1) ||
             !ValidCoordinate(box.X2) || !ValidCoordinate(box.Y2) ||
             box.X2 <= box.X1 || box.Y2 <= box.Y1))
        {
            throw new GuiGroundingException(
                "Grounding box must satisfy 0 <= x1 < x2 <= 1000 and 0 <= y1 < y2 <= 1000.");
        }

        if (response.Point is { } selectedPoint && response.BoundingBox is { } selectedBox &&
            (selectedPoint.X < selectedBox.X1 || selectedPoint.X > selectedBox.X2 ||
             selectedPoint.Y < selectedBox.Y1 || selectedPoint.Y > selectedBox.Y2))
            throw new GuiGroundingException("Grounding point falls outside the returned box.");
    }

    private static bool ValidCoordinate(double value) =>
        double.IsFinite(value) && value >= 0 && value <= 1_000;
}
