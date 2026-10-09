# Pindo

A Windows-native software tutor for Filipino educators. The user performs actions; the app captures the active window and shows visual guidance.

## Active desktop baseline

Open **`Pointly.sln`** in Rider. `Pointly.App` is the active desktop implementation, imported from the existing Pointly working tree on October 9, 2026. Its product name and tray icon already say Pindo; internal Pointly project names are retained to keep the import verifiable.

The active desktop app references the Core/Tools libraries under `src/`, integrated from PR #2 with its history preserved. `LocalTutor.slnx` also contains the earlier scaffold app; use `Pointly.sln` to run Pindo. Do not run both desktop apps at once: their global shortcuts can collide.

Requirements: Windows 11 build 26100 or newer for this baseline, and the .NET 10 SDK. The project targets `net10.0-windows10.0.26100.0`; other Windows versions are unverified.

```powershell
dotnet restore Pointly.sln
dotnet build Pointly.sln -c Debug --no-restore
dotnet test Pointly.sln -c Debug --no-build --no-restore
```

To build and start the live app with your locally saved cloud credentials:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Start-Pindo.ps1
```

The app starts in the system tray. Focus the app you need help with, then press **Ctrl+Space** to open the composer. Type a question and press **Enter**; **Shift+Enter** adds a line. Click the microphone to start voice, and click again to mute it. A crossed-out microphone means recording is off. **Escape** or **Ctrl+Space** dismisses the composer. Exit through the tray menu before rebuilding. Close other Pindo/Pointly instances first.

For a visual preview without AI requests, add `-Preview` to the launcher command. See [the chat UI change and validation](docs/CHAT_UI.md).

## Current capabilities and limits

The imported code includes window tracking, UI Automation, Windows.Graphics.Capture, visual overlays, step verification, walkthroughs, and voice infrastructure. Historical source notes are preserved under [docs/pointly-baseline](docs/pointly-baseline/PRODUCT.md); their verification claims describe the source project.

**Local inference is not integrated yet.** The runtime uses AssemblyAI for tutor selection, OpenRouter (or optional Bedrock) for vision, and ElevenLabs for voice. The composer starts with recording off. The live launcher selects OpenRouter for vision and refreshes credentials from Windows user environment variables. Tutor HTTP failures fall back to screenshot guidance. Do not describe this baseline as offline.

The selected local candidate is **MAI-UI-2B**, community Ollama package `maternion/mai-ui:2b`. The user is managing its download. Installing it does not wire it into this app. No benchmark result is claimed yet.

This pass adds cloud goal planning, step-by-step verification, animated annotations and reviewed chat tools. See [live tutoring setup, execution flow and validation](docs/LIVE_TUTORING.md). While thinking, inputs are disabled; Cancel, Escape and Ctrl+Space remain available. Check again captures fresh state and does not skip verification.

Use **+** to select files and a workspace, then ask for an operation. Review its proposed inputs and outputs and explicitly click **Run**. Existing outputs are never overwritten. Duplicate detection only reports matches. Document/media conversion, media compression and PDF optimization remain unavailable.

Install and configure the tools' public Windows dependencies once:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Setup-ToolDependencies.ps1 -Install
dotnet test tests/LocalTutor.Tests/LocalTutor.Tests.csproj
```

Restart Pindo after configuring dependencies. Run the setup script without `-Install` to check installed dependencies. Normal test commands do not call cloud APIs; live acceptance tests are opt-in.

Local model testing comes after this pass. Qwen3-1.7B may be tested for text planning/tool selection; screenshot understanding still needs a vision model. No local inference result is claimed.

## Repository and disclosure

Shared repository: [cadornajansen/pindo](https://github.com/cadornajansen/pindo).

Pointly contains pre-existing implementation. The team supplied the rule: “Pre-existing code is allowed only if the project is substantially built during the hackathon, with prior work disclosed.” The import itself does not establish substantial new development. Keep the reused baseline and hackathon contributions separately identifiable in the submission.

See [the import inventory and validation](docs/POINTLY_IMPORT.md), [disclosures](docs/HACKATHON_COMPLIANCE.md), and [documentation index](docs/README.md).
