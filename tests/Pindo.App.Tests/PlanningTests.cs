using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Pindo.App.Planning;
using Pindo.App.Tools;

namespace Pindo.App.Tests;

public sealed class PlanningTests
{
    private static readonly ToolSelection Selection = new("C:\\work", ["C:\\work\\lesson.txt"], [], []);
    private static string Envelope(string content, string reason = "stop") => JsonSerializer.Serialize(new
    {
        choices = new[] { new { finish_reason = reason, message = new { content } } }
    });
    private static HttpResponseMessage Reply(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };
    private static string? Config(string key) => key == "OPENROUTER_API_KEY" ? "test-key" : null;

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token);
    }

    [Theory]
    [InlineData("clarification")]
    [InlineData("unsupported")]
    public async Task TextOnlyRequestIncludesSelectionAndExcludesHostOnlyTool(string kind)
    {
        string? sent = null;
        using var http = new HttpClient(new Handler(async (request, token) =>
        {
            sent = await request.Content!.ReadAsStringAsync(token);
            return Reply(Envelope(JsonSerializer.Serialize(new { kind, message = "Select files first.", tool = (object?)null })));
        }));
        using var planner = new OpenRouterPlanner(http, Config);
        UtilityResponse result = await planner.PlanAsync(["Make a ZIP"], Selection, CancellationToken.None);
        Assert.Equal(kind, result.Kind);
        Assert.Contains("lesson.txt", sent);
        Assert.DoesNotContain("image_url", sent);
        using JsonDocument request = JsonDocument.Parse(sent!);
        using JsonDocument context = JsonDocument.Parse(request.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.DoesNotContain(context.RootElement.GetProperty("tools").EnumerateArray(), tool => tool.GetProperty("id").GetString() == "file.organize_apply");
    }

    [Fact]
    public async Task MissingKeyMakesNoNetworkRequest()
    {
        using var http = new HttpClient(new Handler((_, _) => throw new InvalidOperationException("Must not send")));
        using var planner = new OpenRouterPlanner(http, _ => null);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => planner.PlanAsync(["ZIP"], Selection, CancellationToken.None));
        Assert.Contains("OPENROUTER_API_KEY", error.Message);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"kind\":\"complete\",\"message\":\"Done\"}")]
    [InlineData("{\"kind\":\"tool\",\"message\":\"Do it\",\"tool\":{\"toolId\":\"file.organize_apply\",\"arguments\":{\"planId\":\"fake\"}}}")]
    [InlineData("{\"kind\":\"tool\",\"message\":\"Do it\",\"tool\":{\"toolId\":\"pdf.optimize\",\"arguments\":{}}}")]
    [InlineData("{\"kind\":\"unsupported\",\"message\":\"No\",\"extra\":true}")]
    public async Task RejectsMalformedOrForbiddenProposals(string content)
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Reply(Envelope(content)))));
        using var planner = new OpenRouterPlanner(http, Config);
        await Assert.ThrowsAsync<InvalidOperationException>(() => planner.PlanAsync(["ZIP"], Selection, CancellationToken.None));
    }

    [Fact]
    public async Task CancellationRejectsLateResponse()
    {
        using var cancel = new CancellationTokenSource();
        using var http = new HttpClient(new Handler((_, _) =>
        {
            cancel.Cancel();
            return Task.FromResult(Reply(Envelope("{\"kind\":\"clarification\",\"message\":\"Late\"}")));
        }));
        using var planner = new OpenRouterPlanner(http, Config);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => planner.PlanAsync(["ZIP"], Selection, cancel.Token));
    }

    [Fact]
    public void ClarificationKeepsContextAndResetInvalidatesOldRequests()
    {
        var session = new UtilitySession();
        long first = session.Begin("Make a ZIP");
        session.Clarify("What name?");
        long second = session.Begin("lesson.zip");
        Assert.Equal(3, session.Conversation.Count);
        Assert.False(session.IsCurrent(first));
        Assert.True(session.IsCurrent(second));
        session.Reset(); // cancellation, changed selection or completed operation
        Assert.Empty(session.Conversation);
        Assert.False(session.IsCurrent(second));
    }

    [Fact]
    public async Task NetworkErrorIsActionable()
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests))));
        using var planner = new OpenRouterPlanner(http, Config);
        HttpRequestException error = await Assert.ThrowsAsync<HttpRequestException>(() => planner.PlanAsync(["ZIP"], Selection, CancellationToken.None));
        Assert.Contains("429", error.Message);
    }

    [Fact]
    public async Task DeadlineCancelsAnInFlightRequest()
    {
        using var http = new HttpClient(new Handler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Reply("");
        }));
        using var planner = new OpenRouterPlanner(http, Config);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => planner.PlanAsync(["ZIP"], Selection, timeout.Token));
    }

    [Fact]
    public async Task OversizedReplyIsRejected()
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Reply(new string('x', 256 * 1024 + 1)))));
        using var planner = new OpenRouterPlanner(http, Config);
        await Assert.ThrowsAsync<InvalidOperationException>(() => planner.PlanAsync(["ZIP"], Selection, CancellationToken.None));
    }

    [Fact]
    public async Task UtilityResponseCanReachRealZipReviewWithoutWritingBeforeRun()
    {
        string root = Path.Combine(Path.GetTempPath(), "pindo-plan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string source = Path.Combine(root, "lesson.txt"), output = Path.Combine(root, "lesson.zip");
            await File.WriteAllTextAsync(source, "disposable fixture");
            string content = JsonSerializer.Serialize(new { kind = "tool", message = "Review ZIP", tool = new
            { toolId = "archive.create_zip", arguments = new { inputPaths = new[] { source }, outputPath = output } } });
            using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Reply(Envelope(content)))));
            using var planner = new OpenRouterPlanner(http, Config);
            var selected = new ToolSelection(root, [source], [], []);
            UtilityResponse response = await planner.PlanAsync(["ZIP this file"], selected, CancellationToken.None);
            var dispatcher = new LocalToolDispatcher(new(null, null, null, null));
            using (ToolReview cancelled = await dispatcher.PrepareAsync(response.Tool!, selected, CancellationToken.None)) { }
            Assert.False(File.Exists(output));
            using ToolReview review = await dispatcher.PrepareAsync(response.Tool!, selected, CancellationToken.None);
            await review.RunAsync(CancellationToken.None);
            Assert.True(File.Exists(output));
            using ToolReview conflict = await dispatcher.PrepareAsync(response.Tool!, selected, CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => conflict.RunAsync(CancellationToken.None));
        }
        finally { Directory.Delete(root, true); }
    }
}
