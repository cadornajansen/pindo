# Utility baseline cleanup — October 13, 2026

## What changed

Pindo is now a Windows utility chat with OpenRouter planning and local reviewed execution. The active solution and namespaces use Pindo. Tutoring, voice, visual observation, local models and old scaffolding are removed. The repository directory is unchanged.

Checkpoint commits: `fbf2032` (names/layout), `986d604` (retired features), followed by the utility/shortcut integration commit containing this document. The prior uncommitted experiment is recoverable locally at `recovery/pre-pindo-utility-cleanup`, commit `153900a`; it is not part of the main push.

## Size comparison

Measured against the local recovery snapshot; runtime C#/XAML under the app/src directories, excluding tests, docs and generated outputs. UTF-8 bytes normalize line endings. These are source-size measures, not startup/memory benchmarks.

| Measure | Before | After |
| --- | ---: | ---: |
| Runtime source files | 111 | 49 |
| Runtime source lines | 12,237 | 4,861 |
| Runtime source bytes | 590,981 | 271,759 |
| Runtime NuGet package references | 2 | 0 |

About 60% fewer runtime source lines. Native image/PDF/video tools remain separately installed dependencies; .NET/WPF is still required.

## How it works

1. Ctrl+Windows or the tray opens the compact composer on the cursor monitor; no target application is required.
2. A typed request and selected-path metadata go to OpenRouter. The response must be clarification, tool proposal or unsupported.
3. The host validates tool arguments and selected paths and prepares a single-use preview.
4. Only Run starts the local utility. Cancel, changed selection and request identities prevent stale proposals from executing.
5. Results include output paths and partial failures. Cancellation does not roll back completed writes.

The modifier hook runs on a dedicated message-loop thread. It tracks physical key states and ignores injected events. A standalone chord uses an unassigned menu-mask key before modifier release to prevent Start-menu activation while forwarding every physical modifier release. Other keys cancel recognition. Hook documentation: [Microsoft LowLevelKeyboardProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc).

## Validation

- Debug build: zero warnings/errors.
- Normal desktop suite: 43 passed, 0 failed, 2 opt-in checks skipped (cloud and native input).
- Retained tools suite: 239 passed, 0 failed, 6 skipped (four Unix process fixtures, Windows symlink privileges, opt-in live video). The retired scaffold contract test accounts for the previous 240-to-239 difference.
- Live OpenRouter ZIP smoke: 1 passed. A real cloud proposal produced a valid preview; disposing the preview created nothing; approving a fresh preview created the archive; existing output was rejected and input stayed unchanged.
- Native input smoke, separately: 1 passed across 16 modifier-side/order combinations. Synthetic input plus the actual Win32 menu-mask implementation preserved the existing foreground window and left all modifiers released.
- WPF smoke tests: composer opens without a target app, busy inputs are disabled, cancellation rejects a late reply, removed controls are absent, actual review Cancel and Run buttons produce distinct outcomes.
- Render inspected at `%TEMP%/pindo-utility-qa/composer.png`.
- Hook recognition covers left/right modifiers, both press/release orders, repeats, extra keys, injected input and reset. Native hook installation/reset/disposal passed.

An initial native check incorrectly required Windows to grant a new test window foreground focus; Windows denied that in subsequent runs. The final test preserves and checks the already-active window instead, without stealing focus. It remains opt-in because it sends desktop-wide synthetic input. This does not replace a physical-keyboard/device check; Windows shortcut combinations are covered by the state machine rather than creating/removing real virtual desktops.

No broad model-reliability, physical multi-monitor, installer, memory or startup-performance claim is made. Normal tests use fake model transport; only the stated ZIP smoke used the live cloud service.

## Files changed

This is the complete source-file inventory relative to the preserved pre-cleanup working tree. Generated build outputs and temporary QA fixtures are excluded. Historical names below identify removed/renamed files.

| Change | File(s) | Purpose |
| --- | --- | --- |
| Deleted | `LocalTutor.slnx` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Created | `Pindo.sln` | Unify project identity, native shell or application lifecycle. |
| Deleted | `Pointly.App/Automation/UiAutomationService.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Automation/UiElementInfo.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Automation/UiaWorkScheduler.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Capture/CaptureDebugPng.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Capture/CapturePngEncoder.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Capture/GraphicsCaptureInterop.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Capture/WindowCaptureResult.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Capture/WindowCaptureService.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Input/GlobalHotkey.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Interop/NativeMethods.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/MainWindow.Goals.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/MainWindow.Tools.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/MainWindow.xaml` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/MainWindow.xaml.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Overlay/HighlightOverlay.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Presentation/BuddySurface.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Presentation/GuidancePresenter.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Presentation/PresentationGeometry.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Tutor/AssemblyAiTutorModel.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Tutor/GoalSession.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Tutor/IGoalPlanner.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Tutor/OllamaClient.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Tutor/OllamaGoalPlanner.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Tutor/OpenRouterPlanner.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Tutor/TutorContracts.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Tutor/TutorService.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Verification/ClickVerificationContracts.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Verification/ClickVerificationService.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Verification/ClickVerificationState.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Verification/InputForegroundPolicy.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Verification/KeyboardVerificationService.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Verification/LowLevelKeyboardHook.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Verification/LowLevelMouseHook.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Verification/StepVerificationCoordinator.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Verification/TextEntryVerificationService.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Verification/UiAutomationStateVerifier.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Vision/BedrockNovaVisionModel.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Vision/GuiGroundingContracts.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Vision/GuiGroundingModelFactory.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Vision/GuiGroundingService.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Vision/OpenRouterGroundingModel.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Vision/VisionCoordinateConverter.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Vision/VisionDebugOptions.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Voice/DefaultMicrophone.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Voice/ElevenLabsCredentials.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Voice/ElevenLabsSpeechToText.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Voice/ElevenLabsTextToSpeech.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Voice/SpeechAudioDiagnostics.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Voice/VoiceContracts.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Voice/VoiceSession.Persistent.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Voice/VoiceSession.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Voice/WindowsAudioCapture.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Voice/WindowsAudioPlayback.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Walkthrough/WalkthroughService.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Windows/ForegroundWindowService.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Windows/TargetContextWatcher.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.App/Windows/TargetWindowContext.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.Tests/ActionVerificationTests.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.Tests/ChatBusyTests.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.Tests/ClickVerificationTests.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.Tests/D8LifecycleTests.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.Tests/ElevenLabsCredentialsTests.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.Tests/ElevenLabsSpeechToTextTests.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.Tests/GoalSessionTests.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.Tests/LocalQwenEvaluationTests.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.Tests/OllamaTests.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.Tests/PersistentVoiceTests.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.Tests/PresentationTests.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.Tests/SpeechAudioDiagnosticsTests.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.Tests/VoiceSessionTests.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.Tests/WalkthroughTests.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `Pointly.sln` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Modified | `README.md` | Document the current utility baseline, provenance and validation. |
| Deleted | `design-qa.md` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `docs/CHAT_UI.md` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `docs/DATA_MODEL.md` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `docs/HACKATHON_COMPLIANCE.md` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `docs/IMPLEMENTATION.md` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `docs/LIVE_TUTORING.md` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `docs/LIVE_TUTORING_FILES.md` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `docs/POINTLY_IMPORT.md` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `docs/PRD.md` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `docs/PRODUCT_SCOPE.md` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Created | `docs/PROVENANCE.md` | Document the current utility baseline, provenance and validation. |
| Deleted | `docs/QWEN_LOCAL.md` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `docs/README.md` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `docs/TEAM_TASKS.md` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `docs/VALIDATION_PLAN.md` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `docs/pointly-baseline/D4.md` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `docs/pointly-baseline/D5.md` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `docs/pointly-baseline/D6.1.md` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `docs/pointly-baseline/D6.2.md` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `docs/pointly-baseline/D6.3.md` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `docs/pointly-baseline/D7.md` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `docs/pointly-baseline/D8.md` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `docs/pointly-baseline/DELIVERABLES.md` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `docs/pointly-baseline/PRODUCT.md` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Modified | `scripts/Setup-ToolDependencies.ps1` | Launch, configure or test the unified Pindo app. |
| Modified | `scripts/Start-Pindo.ps1` | Launch, configure or test the unified Pindo app. |
| Deleted | `scripts/Test-LocalQwen.ps1` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Created | `scripts/Test-Pindo.ps1` | Launch, configure or test the unified Pindo app. |
| Deleted | `src/LocalTutor.Core/TutorContracts.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `src/LocalTutor.Desktop/App.xaml` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `src/LocalTutor.Desktop/App.xaml.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `src/LocalTutor.Desktop/AssemblyInfo.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `src/LocalTutor.Desktop/Interop/GlobalHotkey.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `src/LocalTutor.Desktop/Interop/NativeMethods.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `src/LocalTutor.Desktop/LocalTutor.Desktop.csproj` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `src/LocalTutor.Desktop/MainWindow.xaml` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `src/LocalTutor.Desktop/MainWindow.xaml.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `src/LocalTutor.Desktop/Services/IOllamaClient.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `src/LocalTutor.Desktop/Services/MockTutorService.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `src/LocalTutor.Desktop/Services/OllamaAvailabilityClient.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Deleted | `src/LocalTutor.Desktop/Services/OllamaSettings.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Renamed/updated | `Pointly.App/App.xaml` → `src/Pindo.App/App.xaml` | Unify project identity, native shell or application lifecycle. |
| Renamed/updated | `Pointly.App/App.xaml.cs` → `src/Pindo.App/App.xaml.cs` | Unify project identity, native shell or application lifecycle. |
| Renamed/updated | `Pointly.App/AssemblyInfo.cs` → `src/Pindo.App/AssemblyInfo.cs` | Unify project identity, native shell or application lifecycle. |
| Renamed/updated | `Pointly.App/Assets/Pindo.ico` → `src/Pindo.App/Assets/Pindo.ico` | Unify project identity, native shell or application lifecycle. |
| Created | `src/Pindo.App/Configuration.cs` | Read Pindo configuration and migrate legacy settings without overwriting. |
| Created | `src/Pindo.App/Input/ModifierChord.cs` | Ctrl+Windows chord recognition and native keyboard-hook lifecycle. |
| Created | `src/Pindo.App/Input/ModifierHotkey.cs` | Ctrl+Windows chord recognition and native keyboard-hook lifecycle. |
| Created | `src/Pindo.App/Interop/NativeMethods.cs` | Unify project identity, native shell or application lifecycle. |
| Renamed/updated | `Pointly.App/Pointly.App.csproj` → `src/Pindo.App/Pindo.App.csproj` | Unify project identity, native shell or application lifecycle. |
| Created | `src/Pindo.App/Planning/OpenRouterPlanner.cs` | Cloud-only utility planning, response contracts and request identity. |
| Created | `src/Pindo.App/Planning/UtilityRequest.cs` | Cloud-only utility planning, response contracts and request identity. |
| Renamed/updated | `Pointly.App/Presentation/BusyBorder.cs` → `src/Pindo.App/Presentation/BusyBorder.cs` | Keep compact chat and review UI; remove tutoring/voice controls. |
| Renamed/updated | `Pointly.App/Presentation/ChatWindow.xaml` → `src/Pindo.App/Presentation/ChatWindow.xaml` | Keep compact chat and review UI; remove tutoring/voice controls. |
| Renamed/updated | `Pointly.App/Presentation/ChatWindow.xaml.cs` → `src/Pindo.App/Presentation/ChatWindow.xaml.cs` | Keep compact chat and review UI; remove tutoring/voice controls. |
| Renamed/updated | `Pointly.App/Presentation/DesktopGeometry.cs` → `src/Pindo.App/Presentation/DesktopGeometry.cs` | Keep compact chat and review UI; remove tutoring/voice controls. |
| Renamed/updated | `Pointly.App/Presentation/ToolReviewWindow.cs` → `src/Pindo.App/Presentation/ToolReviewWindow.cs` | Keep compact chat and review UI; remove tutoring/voice controls. |
| Renamed/updated | `Pointly.App/Shell/TrayShell.cs` → `src/Pindo.App/Shell/TrayShell.cs` | Unify project identity, native shell or application lifecycle. |
| Renamed/updated | `Pointly.App/Tools/LocalToolDispatcher.cs` → `src/Pindo.App/Tools/LocalToolDispatcher.cs` | Bridge validated cloud proposals to reviewed local execution. |
| Renamed/updated | `Pointly.App/Tools/ToolReview.cs` → `src/Pindo.App/Tools/ToolReview.cs` | Bridge validated cloud proposals to reviewed local execution. |
| Created | `src/Pindo.App/UtilityController.cs` | Coordinate selection, clarification, cancellation, preview and execution. |
| Renamed/updated | `Pointly.App/app.manifest` → `src/Pindo.App/app.manifest` | Unify project identity, native shell or application lifecycle. |
| Renamed/updated | `src/LocalTutor.Core/LocalTutor.Core.csproj` → `src/Pindo.Core/Pindo.Core.csproj` | Retain shared typed utility contracts under Pindo. |
| Renamed/updated | `src/LocalTutor.Core/Tools/ToolContracts.cs` → `src/Pindo.Core/Tools/ToolContracts.cs` | Retain shared typed utility contracts under Pindo. |
| Renamed/updated | `src/LocalTutor.Tools/Compression/CompressImageTool.cs` → `src/Pindo.Tools/Compression/CompressImageTool.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/Compression/ImageCompressionContracts.cs` → `src/Pindo.Tools/Compression/ImageCompressionContracts.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/Compression/compress_image.schemas.json` → `src/Pindo.Tools/Compression/compress_image.schemas.json` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/FileInspectionAndConversion/ConvertImageTool.cs` → `src/Pindo.Tools/FileInspectionAndConversion/ConvertImageTool.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/FileInspectionAndConversion/FileAccessScope.cs` → `src/Pindo.Tools/FileInspectionAndConversion/FileAccessScope.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/FileInspectionAndConversion/FileToolContracts.cs` → `src/Pindo.Tools/FileInspectionAndConversion/FileToolContracts.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/FileInspectionAndConversion/ImageContent.cs` → `src/Pindo.Tools/FileInspectionAndConversion/ImageContent.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/FileInspectionAndConversion/ImageMagickCodec.cs` → `src/Pindo.Tools/FileInspectionAndConversion/ImageMagickCodec.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/FileInspectionAndConversion/InspectFileTool.cs` → `src/Pindo.Tools/FileInspectionAndConversion/InspectFileTool.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/FileOrganization/FindDuplicatesTool.cs` → `src/Pindo.Tools/FileOrganization/FindDuplicatesTool.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/FileOrganization/OrganizationAccessScope.cs` → `src/Pindo.Tools/FileOrganization/OrganizationAccessScope.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/FileOrganization/OrganizationContracts.cs` → `src/Pindo.Tools/FileOrganization/OrganizationContracts.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/FileOrganization/OrganizationIO.cs` → `src/Pindo.Tools/FileOrganization/OrganizationIO.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/FileOrganization/OrganizeApplyTool.cs` → `src/Pindo.Tools/FileOrganization/OrganizeApplyTool.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/FileOrganization/OrganizePreviewTool.cs` → `src/Pindo.Tools/FileOrganization/OrganizePreviewTool.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/LocalTool.cs` → `src/Pindo.Tools/LocalTool.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/Pdf/PdfContracts.cs` → `src/Pindo.Tools/Pdf/PdfContracts.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/Pdf/PdfNative.cs` → `src/Pindo.Tools/Pdf/PdfNative.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/Pdf/PdfTools.cs` → `src/Pindo.Tools/Pdf/PdfTools.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/Pdf/pdf.schemas.json` → `src/Pindo.Tools/Pdf/pdf.schemas.json` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/LocalTutor.Tools.csproj` → `src/Pindo.Tools/Pindo.Tools.csproj` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/VideoDownload/DownloadVideoTool.cs` → `src/Pindo.Tools/VideoDownload/DownloadVideoTool.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/VideoDownload/InspectVideoTool.cs` → `src/Pindo.Tools/VideoDownload/InspectVideoTool.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/VideoDownload/NativeYtDlpProcess.cs` → `src/Pindo.Tools/VideoDownload/NativeYtDlpProcess.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/VideoDownload/VideoContracts.cs` → `src/Pindo.Tools/VideoDownload/VideoContracts.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/VideoDownload/VideoMetadata.cs` → `src/Pindo.Tools/VideoDownload/VideoMetadata.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/VideoDownload/VideoUrl.cs` → `src/Pindo.Tools/VideoDownload/VideoUrl.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/VideoDownload/YtDlpClient.cs` → `src/Pindo.Tools/VideoDownload/YtDlpClient.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/ZipArchive/ArchiveAccessScope.cs` → `src/Pindo.Tools/ZipArchive/ArchiveAccessScope.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/ZipArchive/ArchiveContracts.cs` → `src/Pindo.Tools/ZipArchive/ArchiveContracts.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/ZipArchive/ArchiveIO.cs` → `src/Pindo.Tools/ZipArchive/ArchiveIO.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/ZipArchive/CreateZipTool.cs` → `src/Pindo.Tools/ZipArchive/CreateZipTool.cs` | Retain utility behavior under Pindo naming and configuration. |
| Renamed/updated | `src/LocalTutor.Tools/ZipArchive/ExtractZipTool.cs` → `src/Pindo.Tools/ZipArchive/ExtractZipTool.cs` | Retain utility behavior under Pindo naming and configuration. |
| Deleted | `tests/LocalTutor.Tests/TutorContractsTests.cs` | Remove retired feature, obsolete scaffold/test, or superseded historical documentation; retained in Git history. |
| Created | `tests/Pindo.App.Tests/CloudSmokeTests.cs` | Verify planner, reviews, desktop lifecycle or shortcut behavior. |
| Created | `tests/Pindo.App.Tests/DesktopTests.cs` | Verify planner, reviews, desktop lifecycle or shortcut behavior. |
| Created | `tests/Pindo.App.Tests/ModifierChordTests.cs` | Verify planner, reviews, desktop lifecycle or shortcut behavior. |
| Renamed/updated | `Pointly.Tests/Pointly.Tests.csproj` → `tests/Pindo.App.Tests/Pindo.App.Tests.csproj` | Verify planner, reviews, desktop lifecycle or shortcut behavior. |
| Created | `tests/Pindo.App.Tests/PlanningTests.cs` | Verify planner, reviews, desktop lifecycle or shortcut behavior. |
| Renamed/updated | `Pointly.Tests/ToolDispatcherTests.cs` → `tests/Pindo.App.Tests/ToolDispatcherTests.cs` | Verify planner, reviews, desktop lifecycle or shortcut behavior. |
| Renamed/updated | `tests/LocalTutor.Tests/Compression/ImageCompressionTests.cs` → `tests/Pindo.Tools.Tests/Compression/ImageCompressionTests.cs` | Retain utility verification with renamed references/configuration. |
| Renamed/updated | `tests/LocalTutor.Tests/FileInspectionAndConversion/ImageToolTests.cs` → `tests/Pindo.Tools.Tests/FileInspectionAndConversion/ImageToolTests.cs` | Retain utility verification with renamed references/configuration. |
| Renamed/updated | `tests/LocalTutor.Tests/FileOrganization/FindDuplicatesTests.cs` → `tests/Pindo.Tools.Tests/FileOrganization/FindDuplicatesTests.cs` | Retain utility verification with renamed references/configuration. |
| Renamed/updated | `tests/LocalTutor.Tests/FileOrganization/OrganizationApplyTests.cs` → `tests/Pindo.Tools.Tests/FileOrganization/OrganizationApplyTests.cs` | Retain utility verification with renamed references/configuration. |
| Renamed/updated | `tests/LocalTutor.Tests/FileOrganization/OrganizationNativeTests.cs` → `tests/Pindo.Tools.Tests/FileOrganization/OrganizationNativeTests.cs` | Retain utility verification with renamed references/configuration. |
| Renamed/updated | `tests/LocalTutor.Tests/FileOrganization/OrganizationPreviewTests.cs` → `tests/Pindo.Tools.Tests/FileOrganization/OrganizationPreviewTests.cs` | Retain utility verification with renamed references/configuration. |
| Renamed/updated | `tests/LocalTutor.Tests/LocalToolTests.cs` → `tests/Pindo.Tools.Tests/LocalToolTests.cs` | Retain utility verification with renamed references/configuration. |
| Renamed/updated | `tests/LocalTutor.Tests/Pdf/PdfToolTests.cs` → `tests/Pindo.Tools.Tests/Pdf/PdfToolTests.cs` | Retain utility verification with renamed references/configuration. |
| Renamed/updated | `tests/LocalTutor.Tests/LocalTutor.Tests.csproj` → `tests/Pindo.Tools.Tests/Pindo.Tools.Tests.csproj` | Retain utility verification with renamed references/configuration. |
| Renamed/updated | `tests/LocalTutor.Tests/VideoDownload/ApprovedLiveVideoTests.cs` → `tests/Pindo.Tools.Tests/VideoDownload/ApprovedLiveVideoTests.cs` | Retain utility verification with renamed references/configuration. |
| Renamed/updated | `tests/LocalTutor.Tests/VideoDownload/NativeVideoProcessTests.cs` → `tests/Pindo.Tools.Tests/VideoDownload/NativeVideoProcessTests.cs` | Retain utility verification with renamed references/configuration. |
| Renamed/updated | `tests/LocalTutor.Tests/VideoDownload/VideoToolTests.cs` → `tests/Pindo.Tools.Tests/VideoDownload/VideoToolTests.cs` | Retain utility verification with renamed references/configuration. |
| Renamed/updated | `tests/LocalTutor.Tests/ZipArchive/ArchiveToolTests.cs` → `tests/Pindo.Tools.Tests/ZipArchive/ArchiveToolTests.cs` | Retain utility verification with renamed references/configuration. |
| Created | `docs/CLEANUP.md` | Record the cleanup, tests, measurements and complete file inventory. |
