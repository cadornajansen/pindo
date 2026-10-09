namespace LocalTutor.Core.Tools;

public abstract record ToolInput
{
    public abstract IReadOnlyList<string> Validate();
}

public sealed record ToolResult<TOutput>(bool Success, TOutput? Value, string? Error = null);

public interface ILocalTool<in TInput, TOutput> where TInput : ToolInput
{
    string Id { get; }

    string Description { get; }

    Task<ToolResult<TOutput>> ExecuteAsync(
        TInput input,
        CancellationToken cancellationToken = default);
}
