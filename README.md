# LocalTutor

A Windows teaching assistant for Filipino educators learning digital tools where they work. The intended product uses local AI to suggest one step and highlight an accessible control; the teacher performs the action.

This is the **bootstrap milestone** for AppBuildersPH Hackathon 2026. The organizer details in [the compliance checklist](docs/HACKATHON_COMPLIANCE.md) were supplied by the team and still require checking against official rules.

## Current status

- Four-project .NET 10 solution with a compact, dark WPF assistant.
- Text input, Submit/Enter, a clearly labeled mock response, and native `Ctrl + Space` registration with collision reporting and cleanup.
- Shared tutoring contracts and validated, typed utilities: `file.inspect` and `file.convert_image` are implemented and tested as library classes, with exact-path approvals.
- `video.inspect_url` and `video.download` are implemented as library classes using local yt-dlp, with a fresh preview approval for each bounded download. The user-approved sample passed a live Linux download and FFmpeg merge; these online utilities are not registered in the desktop/model.
- `file.compress_image` is implemented and tested for **PNG to PNG** using two fixed resize presets. It shows actual candidate dimensions/bytes and warnings, then requires fresh approval before saving a smaller copy. Already-small files return `NoReduction` without writing. See [compression setup and handoff](docs/IMPLEMENTATION.md#png-compression).
- `archive.create_zip` and `archive.extract_zip` are implemented as local library classes using built-in .NET APIs. Both require a displayed entry/size/path/conflict preview and fresh host-owned approval. Creation preserves originals using stored ZIP entries; extraction rejects unsafe paths, links, collisions and excessive expansion, and publishes only into a new directory. See [ZIP contracts and handoff](docs/IMPLEMENTATION.md#zip-archives).
- Central Ollama settings and an asynchronous availability-check boundary. Ollama is optional for launching this scaffold.

**Not implemented:** model inference, Windows UI Automation capture, highlighting, real tutoring, voice, desktop/model tool registration, `file.convert_document`, `file.convert_media`, JPEG/WebP compression, `file.compress_media`, or `pdf.optimize`. No offline tutoring, privacy guarantee, model accuracy, or performance benchmark is claimed. An Ollama availability result only confirms that its API responded.

## Requirements and setup

- Windows 10/11 for WPF execution.
- .NET 10 SDK. `global.json` selects a stable SDK from `10.0.100` onward within .NET 10 using `latestFeature`; the bootstrap machine has `10.0.302` on Windows 11.
- Git; JetBrains Rider with .NET 10 support, or the .NET CLI.
- Ollama is needed only for the next inference milestone; it was not installed on the bootstrap machine and no model was downloaded.
- Optional image utilities require a separately installed local ImageMagick with PNG/JPEG/WebP codecs. The trusted caller supplies its absolute executable path to `ImageMagickCodec`; no binary is bundled or downloaded automatically. Linux tests used ImageMagick 6.9.12-98 Q16. Windows installation, codec policy, process cleanup, and the presentation application still need device validation. See [utility setup and limits](docs/IMPLEMENTATION.md#file-inspection-and-image-conversion).
- PNG compression reuses that ImageMagick configuration and needs only its PNG codec. The host displays a `CompressImageTool.PreviewAsync` result and obtains explicit approval before constructing `ImageCompressionApproval`; model arguments cannot grant consent. Keep one active preview and dispose it when abandoned. Windows codec/process behavior and desktop/model integration remain unverified.
- ZIP tools need only the existing .NET 10 runtime. The host supplies `ArchiveAccessScope` with exact selected inputs and output inside a trusted local workspace, displays `ArchivePreview`, and constructs `ArchiveApproval` only after explicit consent. Dispose abandoned previews. Existing outputs are never overwritten or merged; encrypted, ZIP64, multidisk and non-ZIP archives are unsupported. No install/download or new NuGet dependency is required.
- Optional video utilities require separately installed **yt-dlp 2026.08.19**, configured by absolute trusted executable path. **FFmpeg 9.0.1** is optional for combined files and required for merging separate HTTPS audio/video streams. Versions are checked locally on each operation; the app does not install or update them. The Windows `yt-dlp.exe` distribution has different bundled license obligations from upstream source; neither native binary is bundled here. See [video setup, consent, tests and limitations](docs/IMPLEMENTATION.md#video-inspection-and-download).

Public repository: [cadornajansen/pindo](https://github.com/cadornajansen/pindo).

Clone and verify:

```powershell
git clone https://github.com/cadornajansen/pindo.git
cd pindo
dotnet restore LocalTutor.slnx
dotnet build LocalTutor.slnx --no-restore
dotnet test LocalTutor.slnx --no-build --no-restore
dotnet run --project src/LocalTutor.Desktop/LocalTutor.Desktop.csproj
```

In Rider, open `LocalTutor.slnx`, let package restore finish, and run `LocalTutor.Desktop`. If the IDE cannot locate .NET 10, select the installed .NET CLI in its toolset settings. The CLI commands above remain the verification reference.

## Try the scaffold

1. Launch with the command above while Ollama is unavailable.
2. Type an instruction and press Enter. The response must identify itself as a mock.
3. Focus another application, then press `Ctrl + Space`. The palette should return with its input focused.
4. With the hotkey registered, Escape hides the window and `Ctrl + Space` brings it back. The close button exits and releases the hotkey.
5. If another application owns the shortcut, read the status message. Hiding is disabled so the assistant remains reachable.

Verified on the bootstrap Windows 11 machine:

| Command / check | Result |
|---|---|
| `dotnet restore LocalTutor.slnx` | Passed |
| `dotnet build LocalTutor.slnx --no-restore` | Passed; 0 warnings, 0 errors |
| `dotnet test LocalTutor.slnx --no-build --no-restore` | Passed; 3 tests |
| Windows desktop smoke check | Passed 22 assertions: launch/focus, Submit, empty input, Enter, Hide/Escape, actual global shortcut, second-instance collision, exit, and hotkey release |
| Launch without Ollama | Passed; unavailable status shown and mock tutor remained usable |

The interactive smoke check ran on Windows 11 build `26200` at 125% display scaling. A screenshot was visually reviewed with no clipping observed. Automated unit tests cover shared contracts and pure tool logic; the separate smoke check exercised actual Windows input. Windows 10 and mixed-DPI monitors remain untested. Product UI Automation, inference, and highlighting remain unimplemented.

## Structure and configuration

```text
src/LocalTutor.Desktop/   WPF window, native hotkey, mock tutor, Ollama boundary
src/LocalTutor.Core/      UI snapshots, tutor DTOs, service and tool contracts
src/LocalTutor.Tools/     Validated utility base class, file/image utilities, PNG compression, video and ZIP utilities
tests/LocalTutor.Tests/   Lightweight tests for contracts and pure logic
docs/                    Product, architecture, compliance, team ownership
LocalTutor.slnx           Solution containing the four projects
```

`Desktop → Core`, `Tools → Core`, and `Tests → Core + Tools`. Desktop can reference Tools when a real tool is integrated; Core stays independent of the desktop.

Ollama defaults live in `src/LocalTutor.Desktop/Services/OllamaSettings.cs`: base URL `http://localhost:11434`, model `qwen3:1.7b`. This is source-only configuration for now: edit these central defaults and rebuild. There is no settings UI or environment-variable loader. The availability check uses `GET /api/version`; selecting a model does not load or download it.

The intended flow is instruction + filtered UI Automation snapshot → local Ollama inference → validated target ID + teaching instruction → screen highlight. Only the contracts and mock submission path exist today. Third-party online applications can still require internet even when our future inference is local.

## Classroom image example

The selected utility slice prepares one local water-cycle illustration for a slide: approve `water-cycle.webp` and the new name `water-cycle-slides.png`, inspect the source, then request PNG conversion with a maximum 1600 × 900 box and preserved transparency. The aspect ratio is retained and small images are not enlarged. Review the new image and insert it manually using the presentation application's file picker; acceptance by the installed PowerPoint version is not yet verified. These library tools are not wired into the assistant UI or a model registry. [Implementation notes](docs/IMPLEMENTATION.md#file-inspection-and-image-conversion) include the C# call and test commands.

Verification on Linux (.NET SDK 10.0.112): 24 focused utility cases and all 27 repository tests pass, including all nine PNG/JPEG/WebP conversion pairs with actual pixel decoding. This does not establish Windows desktop or native converter compatibility. Documents and media conversion remain planned.

The subsequent video slice passed 87 focused offline cases (including the optional installed-binary option check), all 114 repository offline cases, and a separately user-approved end-to-end sample test. Downloads are limited to one new MP4/WebM file, ten minutes, and 100 MiB, with source/path validation, cancellation and partial-file cleanup. Only selected HTTPS streams are supported; HLS, credentials, DRM, playlists and bulk downloads are unavailable. Windows native execution and desktop/model integration remain unverified.

The compression slice passed **26 focused cases** on Linux with actual PNG decoding, including both resize presets, already-small/corrupt files, missing dependencies, cancellation, output conflicts, source changes and approval reuse/expiry. The full offline repository run passed **139 cases**, with two video checks skipped (live sample and optional pinned-binary parser). `Slides1600` fits a 1600 × 900 box; `Share800` fits 800 × 600. Both retain aspect ratio and avoid enlargement. Resizing can remove detail; output uses 8-bit sRGB and strips metadata, so no lossless claim is made. The original stays in place. Run `dotnet test tests/LocalTutor.Tests/LocalTutor.Tests.csproj --no-restore --filter FullyQualifiedName~Compression` after configuring the installed codec.

The ZIP slice passed **56 focused cases** on Linux (.NET SDK 10.0.112/runtime 10.0.12). The full offline run passed **195 cases**, with the same two video checks skipped; Tools built with zero warnings/errors. Coverage includes nested/empty folders and archives, stored/deflated payloads, exclusions, CRC corruption, traversal and Windows name attacks, links, conflicting trees, forged bomb metadata, byte/count/depth limits, cancellation cleanup, source changes and approval expiry/reuse. Limits are 4096 entries including implied directories, 128 MiB per file, 256 MiB total input/expansion/archive size, depth 32, and a cooperative two-minute deadline per operation. Extraction refuses ratios above 200:1 for entries larger than 1 MiB. Creation packages files without compression; it makes no size-reduction claim. Windows junctions/ACLs and actual device execution remain unverified. These tools are not callable through the Desktop/model yet. Run `dotnet test tests/LocalTutor.Tests/LocalTutor.Tests.csproj --no-restore --filter FullyQualifiedName~ZipArchive`.

## Team workflow

Use one shared repository and direct collaborator access. Create your assigned branch from current `main`, make a focused commit, push your branch, and request a pull request review. Do not force-push `main`. Branch protection is an intended workflow; it is not claimed to be configured.

The current utility prompt overrides that general workflow: work on local `tool_calling_functions`, commit only its completed work with the user-confirmed author/committer, and do not push, publish, deploy, open a PR, or transfer project data without specific user approval.

| Member | Branch | Primary area |
|---|---|---|
| 1 — technical lead | `feat/ai-desktop` | Desktop, Core contracts, integration |
| 2 — utilities | `feat/local-tools` | Tools and their own tests |
| 3 — documentation | `docs/product` | README and core documentation |
| 4 — research | `research/educators` | Scoped research, prompts, validation scenarios |

Start with the [product documentation index](docs/README.md) for scope, requirements, the data model/ERD, and validation. See [team onboarding and boundaries](docs/TEAM_TASKS.md), [implementation contracts](docs/IMPLEMENTATION.md), and [submission disclosures](docs/HACKATHON_COMPLIANCE.md).

Next milestone: benchmark `qwen3:1.7b` on the team's actual laptop and implement a small, application-independent UI Automation snapshot. Do not infer success from prior experiments or an API availability check.

References: [.NET downloads](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), [WPF overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/), [Ollama API](https://github.com/ollama/ollama/blob/main/docs/api.md).
