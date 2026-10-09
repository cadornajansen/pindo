namespace LocalTutor.Desktop.Services;

// Next milestone: add typed inference, then implement ILocalTutorService using it.
public interface IOllamaClient
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
