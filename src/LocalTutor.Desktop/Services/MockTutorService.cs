using LocalTutor.Core;

namespace LocalTutor.Desktop.Services;

public sealed class MockTutorService : ILocalTutorService
{
    public Task<TutorResponse> GetNextStepAsync(TutorRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new TutorResponse(
            TargetElementId: null,
            Instruction: "Mock response: your instruction reached the tutor service. " +
                         "Local AI and interface guidance are not connected yet.",
            Success: true));
    }
}
