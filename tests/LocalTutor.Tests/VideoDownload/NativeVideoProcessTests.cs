using System.Diagnostics;
using System.Text.Json;
using LocalTutor.Tools.VideoDownload;

namespace LocalTutor.Tests.VideoDownload;

public sealed class NativeVideoProcessTests : IDisposable
{
    private readonly string workspace = Path.Combine(Path.GetTempPath(), "localtutor-native-video-test-" + Guid.NewGuid().ToString("N"));
    public NativeVideoProcessTests() => Directory.CreateDirectory(workspace);

    [PinnedVideoBinaryFact]
    public async Task PinnedBinaryAcceptsTheFixedOptionsWithoutNetwork()
    {
        NativeYtDlpProcess worker = new(Environment.GetEnvironmentVariable("LOCAL_TUTOR_YTDLP")!);
        Assert.Equal(YtDlpClient.SupportedVersion, (await worker.VersionAsync(CancellationToken.None)).Trim());
        using Process native = new() { StartInfo = worker.BuildStart(workspace, ["--help"]) };
        native.Start(); native.StandardInput.Close();
        Task<string> output = native.StandardOutput.ReadToEndAsync();
        Task<string> errors = native.StandardError.ReadToEndAsync();
        await native.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, native.ExitCode);
        Assert.Contains("Usage:", await output);
        Assert.DoesNotContain("no such option", await errors);
    }

    [NativeUnixVideoFact]
    public async Task RealProcessReadsStubOutputAndDiscardsPrivateErrors()
    {
        NativeYtDlpProcess worker = await WorkerAsync("import sys\nsys.stderr.write('private URL and cookie should never be returned\\n')\nprint('{\"title\":\"Fixture\"}')\n");
        string output = await worker.InspectAsync("https://youtu.be/abcdefghijk", CancellationToken.None);
        Assert.Equal("Fixture", JsonDocument.Parse(output).RootElement.GetProperty("title").GetString());
        Assert.DoesNotContain("cookie", output);
    }

    [NativeUnixVideoFact]
    public async Task NativeOutputOverflowIsBounded()
    {
        NativeYtDlpProcess worker = await WorkerAsync("import sys\nsys.stdout.write('x' * (3 * 1024 * 1024))\n");
        VideoToolException error = await Assert.ThrowsAsync<VideoToolException>(() => worker.InspectAsync("https://youtu.be/abcdefghijk", CancellationToken.None));
        Assert.StartsWith("ResourceLimit", error.Message);
    }

    [NativeUnixVideoFact]
    public async Task NativeCancellationKillsTheProcess()
    {
        NativeYtDlpProcess worker = await WorkerAsync("import os,time\nopen('started','w').write(str(os.getpid()))\ntime.sleep(20)\n");
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(300));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker.InspectAsync("https://youtu.be/abcdefghijk", cancellation.Token));
    }

    [NativeUnixVideoFact]
    public async Task NonzeroExitDoesNotEchoTheNativeError()
    {
        NativeYtDlpProcess worker = await WorkerAsync("import sys\nsys.stderr.write('private-secret-url')\nsys.exit(1)\n");
        VideoToolException error = await Assert.ThrowsAsync<VideoToolException>(() => worker.InspectAsync("https://youtu.be/abcdefghijk", CancellationToken.None));
        Assert.StartsWith("SourceUnavailable", error.Message);
        Assert.DoesNotContain("private-secret-url", error.Message);
    }

    private async Task<NativeYtDlpProcess> WorkerAsync(string body)
    {
        string executable = Path.Combine(workspace, "yt-dlp");
        await File.WriteAllTextAsync(executable, "#!/usr/bin/python3\n" + body);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return new NativeYtDlpProcess(executable);
    }

    public void Dispose() => Directory.Delete(workspace, true);
}

public sealed class NativeUnixVideoFactAttribute : FactAttribute
{
    public NativeUnixVideoFactAttribute()
    {
        if (OperatingSystem.IsWindows()) Skip = "Linux process fixtures use the preinstalled /usr/bin/python3; Windows native lifecycle still needs device validation.";
    }
}

public sealed class PinnedVideoBinaryFactAttribute : FactAttribute
{
    public PinnedVideoBinaryFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LOCAL_TUTOR_YTDLP")))
            Skip = "Optional offline check requires an installed pinned binary; stub process tests always run.";
    }
}
