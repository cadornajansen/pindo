using System.IO;
using Pindo.App.Planning;
using Pindo.App.Tools;

namespace Pindo.App.Tests;

public sealed class CloudSmokeTests
{
    private sealed class LiveFactAttribute : FactAttribute
    {
        public LiveFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("PINDO_LIVE_TEST") != "1")
                Skip = "Opt-in cloud request: set PINDO_LIVE_TEST=1 and configure OPENROUTER_API_KEY.";
        }
    }

    [LiveFact]
    public async Task CloudZipProposalRespectsCancelRunAndOutputConflict()
    {
        string root = Path.Combine(Path.GetTempPath(), "pindo-cloud-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string input = Path.Combine(root, "lesson.txt"), output = Path.Combine(root, "lesson.zip");
            await File.WriteAllTextAsync(input, "Original disposable Pindo fixture.");
            var selection = new ToolSelection(root, [input], [], []);
            using var planner = new OpenRouterPlanner();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            UtilityResponse response = await planner.PlanAsync(
                [$"Create a ZIP archive from exactly the selected lesson.txt file. Save the new archive at {output}. Use the selected workspace. Prepare it for my review."],
                selection, timeout.Token);
            Assert.Equal("tool", response.Kind);
            Assert.Equal("archive.create_zip", response.Tool!.ToolId);
            var dispatcher = new LocalToolDispatcher(new(null, null, null, null));
            using (ToolReview cancelled = await dispatcher.PrepareAsync(response.Tool, selection, timeout.Token)) { }
            Assert.False(File.Exists(output));
            using ToolReview review = await dispatcher.PrepareAsync(response.Tool, selection, timeout.Token);
            await review.RunAsync(timeout.Token);
            Assert.True(File.Exists(output));
            Assert.Equal("Original disposable Pindo fixture.", await File.ReadAllTextAsync(input));
            using ToolReview conflict = await dispatcher.PrepareAsync(response.Tool, selection, timeout.Token);
            await Assert.ThrowsAsync<InvalidOperationException>(() => conflict.RunAsync(timeout.Token));
        }
        finally { Directory.Delete(root, true); }
    }
}
