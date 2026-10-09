using LocalTutor.Core.Tools;

namespace LocalTutor.Tools;

/// <summary>
/// Base class for local tools. Implementations execute only after validation and cancellation checks.
/// </summary>
public abstract class LocalTool<TInput, TOutput> : ILocalTool<TInput, TOutput>
    where TInput : ToolInput
{
    public abstract string Id { get; }

    public abstract string Description { get; }

    public Task<ToolResult<TOutput>> ExecuteAsync(
        TInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        IReadOnlyList<string> errors = input.Validate();
        if (errors.Count > 0)
        {
            return Task.FromResult(new ToolResult<TOutput>(false, default, string.Join("; ", errors)));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return ExecuteValidatedAsync(input, cancellationToken);
    }

    protected abstract Task<ToolResult<TOutput>> ExecuteValidatedAsync(
        TInput input,
        CancellationToken cancellationToken);
}
