# Pindo

A Windows-native software tutor for Filipino educators. The user performs actions; the app captures the active window and shows visual guidance.

## Active desktop baseline

Open **`Pointly.sln`** in Rider. `Pointly.App` is the active desktop implementation, imported from the existing Pointly working tree on October 9, 2026. Its product name and tray icon already say Pindo; internal Pointly project names are retained to keep the import verifiable.

The earlier `LocalTutor.slnx`, `src/`, and `tests/` remain intact as a separate scaffold. They are not connected to Pointly. Do not run both apps at once: their global shortcuts can collide.

Requirements: Windows 11 build 26100 or newer for this baseline, and the .NET 10 SDK. The project targets `net10.0-windows10.0.26100.0`; other Windows versions are unverified.

```powershell
dotnet restore Pointly.sln
dotnet build Pointly.sln -c Debug --no-restore
dotnet test Pointly.sln -c Debug --no-build --no-restore
```

For a visual preview that does not call AI providers, run these in the same PowerShell terminal:

```powershell
$env:POINTLY_VOICE_MODE = 'false'
$env:POINTLY_DEBUG_PREVIEW = 'true'
dotnet run --project Pointly.App/Pointly.App.csproj -c Debug --no-build
```

The app starts in the system tray. Press **Ctrl+Space** to show/dismiss the preview. Exit using its tray menu. This preview only verifies presentation, not inference or grounding. Close other Pindo/Pointly instances first. Remove `POINTLY_DEBUG_PREVIEW` from the launch environment before testing real inference.

## Current capabilities and limits

The imported code includes window tracking, UI Automation, Windows.Graphics.Capture, visual overlays, step verification, walkthroughs, and voice infrastructure. Historical source notes are preserved under [docs/pointly-baseline](docs/pointly-baseline/PRODUCT.md); their verification claims describe the source project.

**Local inference is not integrated yet.** The imported runtime still uses AssemblyAI for tutor selection, OpenRouter (or optional Bedrock) for vision, and ElevenLabs for voice. Voice is enabled by default outside the preview above. Normal invocation can use configured cloud credentials; do not describe this baseline as offline.

The selected local candidate is **MAI-UI-2B**, community Ollama package `maternion/mai-ui:2b`. The user is managing its download. Installing it does not wire it into this app. No benchmark result is claimed yet.

Next checkpoint: test screenshot grounding with MAI-UI-2B, then implement its local provider boundary. No model download, app launch, or cloud API call is triggered by these build/test commands.

## Repository and disclosure

Shared repository: [cadornajansen/pindo](https://github.com/cadornajansen/pindo).

Pointly contains pre-existing implementation. The team supplied the rule: “Pre-existing code is allowed only if the project is substantially built during the hackathon, with prior work disclosed.” The import itself does not establish substantial new development. Keep the reused baseline and hackathon contributions separately identifiable in the submission.

See [the import inventory and validation](docs/POINTLY_IMPORT.md), [disclosures](docs/HACKATHON_COMPLIANCE.md), and [documentation index](docs/README.md).
