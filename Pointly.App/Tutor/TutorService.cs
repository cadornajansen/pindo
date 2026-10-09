namespace Pointly.App.Tutor;

public sealed class TutorService(ITutorModel model)
{
    public async Task<TutorResponse> GetNextStepAsync(
        TutorRequest request, CancellationToken cancellationToken)
    {
        if (request.Elements.Count == 0)
            throw new InvalidOperationException("No visible actionable UIA elements are available for tutoring.");

        TutorResponse response = await model.GetNextStepAsync(request, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(response.Instruction) || response.Instruction.Length > 1000 ||
            string.IsNullOrWhiteSpace(response.ExpectedNextState) || response.ExpectedNextState.Length > 1000 ||
            response.AnnotationType != AnnotationType.Rectangle ||
            !double.IsFinite(response.Confidence) || response.Confidence < 0 || response.Confidence > 1 ||
            !request.Elements.Any(element => element.Id == response.TargetElementId))
        {
            throw new InvalidOperationException("Tutor returned an invalid step or an unknown target ID.");
        }

        return response;
    }
}
