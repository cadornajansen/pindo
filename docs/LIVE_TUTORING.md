# Live tutoring and reviewed chat tools

Implementation and Windows validation record, October 10, 2026. Branch: `codex/live-tutoring-tools`. PR #2 was merged locally with its original history in `0056687`. Publication is gated on the acceptance checks below. The Pointly reuse disclosure in README and HACKATHON_COMPLIANCE still applies.

## One flow through the code

1. Typed input and transcribed speech enter `ProcessGoalAsync`. `GoalSession` keeps the original goal, clarification conversation, verified history and a generation number. Cancelling invalidates that number so old responses cannot revive the session.
2. `OpenRouterPlanner` receives fresh UI Automation metadata and a captured target window. It returns one typed outcome: clarification, semantic plan, tool proposal or completion. The host validates this structure. Steps contain expected results, never saved coordinates.
3. `WalkthroughService` presents one step. Existing AssemblyAI selection and configured visual grounding locate that step against the current screen. Input hooks observe the user's actions; Pindo does not send clicks or keystrokes.
4. `VerifyGoalStepAsync` captures the resulting state and asks for positive evidence. A click alone cannot advance a goal. Missing evidence pauses; a mismatched action can replan the remaining steps. Check again runs fresh verification. Application changes clear annotations and pause the session.
5. A tool outcome goes to `LocalToolDispatcher`. The model supplies arguments, but the host supplies selected paths, constructs the preview and creates the approval only after Run. Reviews expire after five minutes and are single-use. Changed inputs and existing output paths fail rather than overwriting files. Results include paths and partial organization outcomes; + → Last tool result retains results after dismissal.

The shared busy state disables editing, Send, Clear, selection and microphone activation. Cancel, Escape and Ctrl+Space remain available. A narrow border shine travels once every 1.6 seconds. Capture temporarily hides the composer, then restores it without requesting keyboard focus. The annotation window is click-through and excluded from captures. It gently dims the target window and draws a contextual cue over 300 ms: arrow for buttons/tabs or coordinate-only targets, underline for inputs, outline for regions. Windows reduced-motion mode uses static cues and a static accented border.

## Setup and one manual acceptance flow

From the repository root, with .NET 10 and Windows 11:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Setup-ToolDependencies.ps1 -Install
powershell -ExecutionPolicy Bypass -File .\scripts\Start-Pindo.ps1
```

Cloud credentials remain in Windows User environment variables, outside Git: `OPENROUTER_API_KEY`, `ASSEMBLYAI_API_KEY` and `ELEVENLABS_API_KEY`. `POINTLY_OPENROUTER_MODEL` optionally selects the planner model. Grounding providers remain independently configured. This app is not offline.

Open a blank PowerPoint slide. Press Ctrl+Space, enter “Insert a table with three columns,” and answer “Two rows.” Follow each instruction yourself. Expect a row-count clarification, Insert/Table/dialog guidance, dimension entry and final verification of a 3×2 table. Use Check again to inspect the current result; it is never a Next button. Repeat by speaking those two sentences with the microphone enabled. A crossed-out microphone is off.

For a tool check, use + to add two disposable text files, then ask “Create a ZIP named lesson.zip in this workspace.” Cancel the first review and confirm no ZIP exists. Ask again and Run. The result should show its new path; asking again for the same output must not overwrite it.

## Implemented tool families

| Family | Available operations | Boundary |
| --- | --- | --- |
| Files/images | Inspect; PNG/JPEG/WebP conversion; implemented PNG compression | Exact selected inputs, new outputs only |
| Video | Inspect a public YouTube/Vimeo URL; preview and download supported formats | Exact URL supplied in the request, pinned binaries, size/time limits |
| ZIP | Create and extract | Traversal, links and conflicts rejected |
| PDF | Inspect, merge, split, extract text, render pages | Poppler utilities required, fresh preview for writes |
| Organization | Preview and apply moves | Host-created approval; Windows File.Move path retained; reverse log for partial results |
| Duplicates | Exact duplicate report | No deletion |

Document/media conversion, media compression and PDF optimization are not registered. `file.organize_apply` is a host continuation of the current preview, never a model-created approval.

## Public Windows dependencies verified on this machine

| Dependency | Version | Configuration |
| --- | --- | --- |
| ImageMagick | 7.1.2-31 Q16-HDRI | `LOCAL_TUTOR_IMAGEMAGICK`: absolute magick.exe path |
| Poppler | 25.07.0 | `LOCAL_TUTOR_POPPLER`: absolute bin directory; `POPPLER_BIN_DIR` for tests |
| yt-dlp | 2026.08.19 | `LOCAL_TUTOR_YTDLP`: absolute yt-dlp.exe path |
| FFmpeg | 9.0.1-full_build-www.gyan.dev | `LOCAL_TUTOR_FFMPEG`: absolute ffmpeg.exe path |

Poppler must include pdfinfo, pdfunite, pdfseparate, pdfimages, pdftotext and pdftoppm. The setup script installs from winget and checks these files. No app dependency uses Codex's private runtime. FFmpeg packaging suffixes are accepted only after the exact pinned release number; 9.0.10 is rejected. ImageMagick 7 channel metadata is normalized before alpha detection, preserving transparent image conversion.

## Automated and device validation

```powershell
dotnet test Pointly.sln -c Debug
dotnet test tests/LocalTutor.Tests/LocalTutor.Tests.csproj
```

For tools tests in an existing terminal, refresh dependency variables from Windows User settings or run the setup script in that terminal first. Desktop tests cover busy input guards, session identity, clarification, verification evidence, existing click/key/text verification, voice lifecycle, selection scope and preview approval/replay/conflicts/changed sources. Tool tests use disposable Windows files and the installed native dependencies.

- Desktop suite: 111 passed, 0 failed, 0 skipped.
- Tools suite: 240 passed, 0 failed, 6 skipped. Four process fixtures require Unix; the Windows symlink fixture needs link privileges; the live-video test is separately opt-in.
- Live video: 1 passed. Inspected, previewed and downloaded the upstream yt-dlp example `https://www.youtube.com/watch?v=YE7VzlLtp-4`, then removed the disposable output. Source: [yt-dlp's replacement test/example commit](https://github.com/yt-dlp/yt-dlp/commit/c102b20).
- PowerPoint typed entry: passed the full flow on a fresh blank slide, including row clarification and all six steps. An independently read PowerPoint COM table reported 3 columns × 2 rows. Some early attempts paused when a popup lost focus; the screen was re-inspected rather than advancing on stale coordinates. Incorrect/unconfirmed clicks did not advance.
- PowerPoint spoken entry: passed all six steps on a fresh blank slide after live ElevenLabs transcription of generated 16 kHz speech fixtures (“Insert a table with three columns” and “Two rows”). COM independently confirmed 3 columns × 2 rows. This checks live STT and the shared spoken goal route, not microphone acoustics. The fixture harness performs the test actions and calls the same result-verification path used by Check again; production Pindo does not inject actions. Physical input-hook behavior is covered separately by the desktop suite.
- Live chat tools: a real cloud ZIP proposal reached the review window. Cancel created no archive; repeating the request and Run created the reviewed archive with the source unchanged.
- Native WPF checks: busy input/duplicate blocking, available Cancel, capture restoration without stealing focus, ignored delayed cancelled response, restored input after cancellation/server failure/timeout, capture exclusion, native click-through hit testing, one spotlight plus one cue, cue replacement and cancellation cleanup all passed. Capture restoration skips the entrance animation while busy, keeping the card interior still. Actual WPF renders were inspected at the machine's 125% scale. Geometry/freshness tests cover 100/125/150/200% and DPI/window changes; physical switching between monitors/scaling settings was not performed.
- Other applications have not been fully tested. General planning support does not establish application-wide reliability.

Windows testing also exposed and fixed a persistent voice race: an unmute notification must not terminate listening unless narration is actually queued. A late verification exception now checks session identity before changing the UI.

## Changed files

The accompanying `LIVE_TUTORING_FILES.md` lists every added/modified file relative to the pre-pass baseline, including imported PR #2 files. No existing source files are deleted. Local screenshots, audio fixtures, presentations and test reports stay outside tracked source.
