using LocalTutor.Core.Tools;
using LocalTutor.Tools;

namespace LocalTutor.Tests;

public sealed class LocalToolTests
{
    [Fact]
    public async Task InvalidInputDoesNotReachExecution()
    {
        RecordingTool tool = new();

        ToolResult<string> result = await tool.ExecuteAsync(new ExampleInput(""));

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal("Name is required.", result.Error);
        Assert.Equal(0, tool.ExecutionCount);
    }

    [Fact]
    public async Task CancellationBeforeExecutionPreventsSideEffects()
    {
        RecordingTool tool = new();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => tool.ExecuteAsync(new ExampleInput("Save"), cancellation.Token));

        Assert.Equal(0, tool.ExecutionCount);
    }

    private sealed record ExampleInput(string Name) : ToolInput
    {
        public override IReadOnlyList<string> Validate() =>
            string.IsNullOrWhiteSpace(Name) ? ["Name is required."] : [];
    }

    private sealed class RecordingTool : LocalTool<ExampleInput, string>
    {
        public override string Id => "test.record";

        public override string Description => "Records execution for validation and cancellation tests.";

        public int ExecutionCount { get; private set; }

        protected override Task<ToolResult<string>> ExecuteValidatedAsync(
            ExampleInput input,
            CancellationToken cancellationToken)
        {
            ExecutionCount++;
            return Task.FromResult(new ToolResult<string>(true, input.Name));
        }
    }
}
