# Pindo

A compact Windows-native chat for reviewed file utilities. Type what you need, select your files, review the proposal, then choose **Run**.

**Cloud-only planning through OpenRouter; utility execution runs locally.** Requests and selected path metadata go to the cloud. Pindo does not capture your screen, listen to your microphone, or operate another app.

## Run

Requirements: Windows 11 build 26100+, .NET 10 SDK, and an OpenRouter API key configured as `OPENROUTER_API_KEY` in your Windows user environment. Open `Pindo.sln` in Rider or Visual Studio.

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Start-Pindo.ps1
```

Press and release **Ctrl+Windows** (no Space) to open/dismiss chat. Either left/right modifier and either press order work. Adding another key cancels Pindo's toggle and leaves the Windows shortcut alone. Escape cancels/dismisses. Enter sends; Shift+Enter inserts a newline. The tray menu also opens Pindo.

Use **+** to select files, folders and a workspace. Ask, for example, "Create a ZIP named lesson.zip from these files." Missing information produces a clarification. A proposal never runs until you approve its preview. Selection changes clear the pending conversation; send the full request again. Last tool result remains available through **+** until app exit.

To inspect the UI without cloud requests:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Start-Pindo.ps1 -Preview
```

Exit Pindo from its tray before rebuilding. Only one instance may run. Session state is in memory.

## Utilities

| Family | Available |
| --- | --- |
| Files/images | Inspect; PNG/JPEG/WebP conversion; PNG compression |
| ZIP | Create and extract |
| PDF | Inspect, merge, split, extract text, render pages |
| Organization | Preview and approve moves; report exact duplicates |
| Video | Inspect/download supported public YouTube/Vimeo URLs |

Selected-path restrictions, five-minute single-use reviews, changed-input checks and no-overwrite rules remain enforced. Duplicate detection does not delete files. Organization may report partial outcomes with a reverse log; cancellation does not undo completed writes. Unsupported requests return an explanation.

Image/PDF/video operations require public native dependencies. Configure installed binaries, or add `-Install` to install them:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Setup-ToolDependencies.ps1
```

The setup script checks ImageMagick, Poppler, yt-dlp and FFmpeg. It preserves existing Pindo user settings. No native executables are bundled with this repository.

## Configuration

- `OPENROUTER_API_KEY`: required for cloud requests.
- `PINDO_OPENROUTER_MODEL`: optional; source default is `google/gemini-3.8-flash`.
- `PINDO_IMAGEMAGICK`, `PINDO_POPPLER`, `PINDO_YTDLP`, `PINDO_FFMPEG`: paths to the native utilities (Poppler uses its binary directory).

Pindo reads only the settings listed above. Configure model and native-tool paths using the `PINDO_` names.

## Code and tests

```text
src/Pindo.App          WPF chat, tray, shortcut, cloud planner, utility review
src/Pindo.Core         Shared typed utility contracts
src/Pindo.Tools        Local utility implementations
tests/Pindo.App.Tests App, planner, review and shortcut tests
tests/Pindo.Tools.Tests Utility and native dependency tests
```

The execution flow is `chat → cloud proposal → host validation → preview → Run → local utility → result`. The host controls permissions and cancellation; the model cannot authorize itself.

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Test-Pindo.ps1
```

The app tests briefly show WPF windows. Live cloud requests are opt-in with `-LiveCloud`; live video tests remain separately opt-in. Test outputs/build products are ignored by Git.

Run the native modifier-input check separately on an idle, unlocked desktop without typing:

```powershell
$env:PINDO_INPUT_TEST = '1'
dotnet test tests/Pindo.App.Tests/Pindo.App.Tests.csproj --filter FullyQualifiedName~NativeMenuMask
Remove-Item Env:PINDO_INPUT_TEST
```

This sends synthetic Ctrl/Windows input to validate Start-menu masking and released modifier state. Unit tests cover recognition, press/release order and cancellation; a physical-keyboard check remains useful on your device.

See [cleanup validation](docs/CLEANUP.md) and [source/dependency attribution](docs/PROVENANCE.md). Tutoring, voice, vision, local models and obsolete scaffold code have been removed from the maintained tree. The next architecture redesign is separate work.
