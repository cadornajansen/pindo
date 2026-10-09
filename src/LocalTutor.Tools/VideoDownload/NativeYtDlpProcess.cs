using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace LocalTutor.Tools.VideoDownload;

internal interface IYtDlpProcess
{
    Task<string> VersionAsync(CancellationToken cancellationToken);
    Task<string?> FfmpegVersionAsync(CancellationToken cancellationToken);
    Task<string> InspectAsync(string url, CancellationToken cancellationToken);
    Task<string> DownloadAsync(string url, VideoCandidate format, string scratch, IProgress<VideoDownloadProgress>? progress, CancellationToken cancellationToken);
}

internal sealed class NativeYtDlpProcess : IYtDlpProcess
{
    private readonly string executable;
    private readonly string? ffmpeg;

    internal NativeYtDlpProcess(string executablePath, string? ffmpegPath = null)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !Path.IsPathFullyQualified(executablePath) ||
            Path.GetFileName(executablePath) is not ("yt-dlp" or "yt-dlp.exe"))
            throw new ArgumentException("Configure an absolute path to a trusted yt-dlp executable.", nameof(executablePath));
        executable = executablePath;
        if (ffmpegPath is not null && (!Path.IsPathFullyQualified(ffmpegPath) || Path.GetFileName(ffmpegPath) is not ("ffmpeg" or "ffmpeg.exe")))
            throw new ArgumentException("Configure an absolute path to a trusted FFmpeg executable.", nameof(ffmpegPath));
        ffmpeg = ffmpegPath;
    }

    public Task<string> VersionAsync(CancellationToken cancellationToken) =>
        RunIsolatedAsync(["--version"], 1024, TimeSpan.FromSeconds(5), cancellationToken);

    public async Task<string?> FfmpegVersionAsync(CancellationToken cancellationToken) => ffmpeg is null || !File.Exists(ffmpeg)
        ? null : await RunIsolatedAsync(["-version"], 16384, TimeSpan.FromSeconds(5), cancellationToken, ffmpegVersion: true);

    public Task<string> InspectAsync(string url, CancellationToken cancellationToken) =>
        RunIsolatedAsync(["--use-extractors", Extractor(url), "--simulate", "--skip-download", "--dump-single-json", "--", url], 2 * 1024 * 1024, TimeSpan.FromSeconds(30), cancellationToken);

    public Task<string> DownloadAsync(string url, VideoCandidate format, string scratch, IProgress<VideoDownloadProgress>? progress, CancellationToken cancellationToken)
    {
        List<string> arguments = ["--use-extractors", Extractor(url), "--no-simulate", "--format", format.Id, "--output", "media." + format.Extension,
            "--fixup", "never", "--no-continue", "--no-overwrites", "--max-filesize", "100M", "--limit-rate", "4M",
            "--match-filters", "duration <= 600 & !is_live", "--print", "after_move:%(format_id)s"];
        if (format.AudioMediaHost is not null)
        {
            if (ffmpeg is null || !File.Exists(ffmpeg)) throw new VideoToolException("MissingDependency: the approved format requires configured local FFmpeg.");
            arguments.AddRange(["--merge-output-format", format.Extension]);
        }
        arguments.AddRange(["--", url]);
        return RunAsync(arguments.ToArray(), scratch, 4096, TimeSpan.FromMinutes(3), format, progress, cancellationToken);
    }

    private async Task<string> RunIsolatedAsync(string[] arguments, int maxOutput, TimeSpan timeout, CancellationToken cancellationToken, bool ffmpegVersion = false)
    {
        string scratch = Path.Combine(Path.GetTempPath(), "localtutor-video-" + Guid.NewGuid().ToString("N"));
        CreateScratch(scratch);
        try { return await RunAsync(arguments, scratch, maxOutput, timeout, null, null, cancellationToken, ffmpegVersion); }
        finally { DeleteScratch(scratch); }
    }

    internal ProcessStartInfo BuildStart(string scratch, IEnumerable<string> arguments)
    {
        ProcessStartInfo start = new(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = scratch,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        // Disable user configuration/plugins, credentials, proxies, persistence and external code/runtime execution.
        foreach (string argument in new[] { "--ignore-config", "--no-plugin-dirs", "--no-remote-components", "--no-js-runtimes",
            "--no-cache-dir", "--no-cookies", "--no-cookies-from-browser", "--no-exec", "--no-update",
            "--proxy", "", "--xff", "never", "--no-playlist", "--playlist-items", "1", "--no-write-playlist-metafiles", "--no-mark-watched",
            "--socket-timeout", "10", "--retries", "0", "--extractor-retries", "0",
            "--fragment-retries", "0", "--no-warnings", "--quiet", "--no-progress" }) start.ArgumentList.Add(argument);
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        // Avoid falling back to an unrelated FFmpeg found on PATH when the host did not configure one.
        start.ArgumentList.Insert(0, ffmpeg ?? Path.Combine(scratch, "unavailable-native-media"));
        start.ArgumentList.Insert(0, "--ffmpeg-location");
        foreach (string variable in new[] { "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "http_proxy", "https_proxy", "all_proxy", "PYTHONPATH", "PYTHONSTARTUP" })
            start.Environment.Remove(variable);
        start.Environment["PYTHONNOUSERSITE"] = "1";
        return start;
    }

    private static string Extractor(string url) => VideoUrl.Source(url) switch { "YouTube" => "Youtube", "Vimeo" => "Vimeo", _ => "Generic" };

    private async Task<string> RunAsync(string[] arguments, string scratch, int maxOutput, TimeSpan timeout,
        VideoCandidate? format, IProgress<VideoDownloadProgress>? progress, CancellationToken cancellationToken, bool ffmpegVersion = false)
    {
        if (!File.Exists(executable)) throw new VideoToolException("MissingDependency: configure an installed, verified local yt-dlp executable.");
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        CancellationToken token = deadline.Token;
        ProcessStartInfo start = BuildStart(scratch, ffmpegVersion ? [] : arguments);
        if (ffmpegVersion)
        {
            start.FileName = ffmpeg!;
            start.ArgumentList.Clear();
            start.ArgumentList.Add("-version");
        }
        using Process process = new() { StartInfo = start };
        try
        {
            token.ThrowIfCancellationRequested();
            process.Start();
            process.StandardInput.Close();
            using CancellationTokenRegistration stop = token.Register(() => Kill(process));
            Task<string> output = ReadOutputAsync();
            Task errors = process.StandardError.BaseStream.CopyToAsync(Stream.Null, token);
            Task monitor = MonitorAsync();
            await Task.WhenAll(output, errors, monitor, process.WaitForExitAsync(token));
            token.ThrowIfCancellationRequested();
            if (process.ExitCode != 0)
                throw new VideoToolException(ffmpegVersion
                    ? "MissingDependency: the configured FFmpeg could not report its version."
                    : "SourceUnavailable: yt-dlp could not access a supported public video or format; no credentials or bypass will be attempted.");
            return await output;

            async Task<string> ReadOutputAsync()
            {
                using MemoryStream buffer = new();
                byte[] chunk = new byte[8192];
                int count;
                while ((count = await process.StandardOutput.BaseStream.ReadAsync(chunk, token)) != 0)
                {
                    if (buffer.Length + count > maxOutput) { Kill(process); throw new VideoToolException("ResourceLimit: native output exceeded its size budget."); }
                    buffer.Write(chunk, 0, count);
                }
                return Encoding.UTF8.GetString(buffer.ToArray());
            }

            async Task MonitorAsync()
            {
                if (format is null) return;
                while (!process.HasExited)
                {
                    long bytes = 0;
                    foreach (string file in Directory.EnumerateFiles(scratch))
                        try { bytes += new FileInfo(file).Length; }
                        catch (FileNotFoundException) { /* yt-dlp may rename its partial file while sampled. */ }
                    if (bytes > VideoMetadata.MaxBytes) { Kill(process); throw new VideoToolException("ResourceLimit: download exceeded 100 MiB."); }
                    progress?.Report(new VideoDownloadProgress("Downloading", bytes, format.Bytes));
                    await Task.Delay(100, token);
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new VideoToolException("TimedOut: yt-dlp exceeded its time budget."); }
        catch (Win32Exception)
        { throw new VideoToolException(ffmpegVersion
            ? "MissingDependency: the configured FFmpeg executable could not be started."
            : "MissingDependency: the configured yt-dlp executable could not be started."); }
        finally
        {
            Kill(process);
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2)); }
            catch (InvalidOperationException) { }
            catch (TimeoutException) { }
        }
    }

    private static void Kill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (Exception e) when (e is InvalidOperationException or Win32Exception) { }
    }

    internal static void CreateScratch(string path)
    {
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(path);
        else Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    internal static void DeleteScratch(string path)
    {
        try { Directory.Delete(path, true); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { throw new VideoToolException("CleanupFailed: temporary video storage could not be removed; inspect local temporary storage."); }
    }
}
