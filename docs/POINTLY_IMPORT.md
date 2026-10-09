# Pointly baseline import — October 9, 2026

## Provenance and scope

Imported from `C:\Development\apps\Pointly` into the GitHub-connected checkout `C:\Development\apps\Pindo\LocalTutor` (remote `cadornajansen/pindo`). Source HEAD: `960ba2e` (September 21, 2026, AssemblyAI tutor milestone).

The source working tree had modified and untracked files, including vision, verification, presentation and voice work. This import captures those current working files, not merely source HEAD. Their individual creation dates and authorship are not established by the HEAD date. Treat the full imported snapshot as prior work in disclosures; do not count it as newly implemented hackathon functionality.

79 files were copied: application C#/XAML/project/manifest/icon files, test C#/project files, the two-project solution, and Markdown source notes under `docs/pointly-baseline/`. All copied files were SHA-256 checked against the source immediately after copying. Namespaces, provider behavior and test internals are unchanged.

Excluded: `.git`, IDE metadata, `bin`, `obj`, `artifacts`, local settings/credentials, provider-key setup scripts, opencode configuration, and MSIX packaging. Packaging was not required for this runnable desktop baseline. The source `AGENTS.md` is not imported as an instruction override. The original Pointly checkout and the existing LocalTutor scaffold are preserved.

A heuristic scan found no AWS access-key, common provider-token, or private-key patterns in selected text files. This is not a complete secret audit. The provider code reads credentials from the launch/user environment; no environment values were copied.

## Data flow

Global shortcut → foreground-window tracking → UI Automation and screenshot capture → existing tutor/vision provider → response validation → physical screen coordinates → overlay → user action verification.

The current tutor is AssemblyAI; the default vision provider is OpenRouter, with Bedrock opt-in; voice uses ElevenLabs. MAI-UI-2B is a selected test candidate only. No local model implementation is included in this import. The existing `LocalTutor.Core` and `LocalTutor.Tools` contracts remain separate and are not used by Pointly.

## Validation

Verified in the destination checkout on October 9, 2026, with .NET SDK 10.0.302 on Windows 11 build 26200:

- `dotnet build Pointly.sln -c Debug`: package restore succeeded; zero warnings/errors.
- `dotnet test Pointly.sln -c Debug --no-build --no-restore`: 95 passed, zero failed/skipped.
- `dotnet build LocalTutor.slnx --no-restore`: preserved scaffold builds with zero warnings/errors.
- `dotnet test LocalTutor.slnx --no-build --no-restore`: all 3 preserved scaffold tests passed.
- All 79 imported files matched source SHA-256 hashes; no existing source file was edited.
- Staged diff inspection found one inherited trailing-whitespace line in `Pointly.App/App.xaml`; it is retained to preserve the source snapshot. Build outputs remain ignored.

Native capture, real AI, voice, foreground correctness and mixed-DPI behavior were not exercised by this import validation. No copied app was launched and no cloud inference was invoked. Unit tests include mocked provider behavior and presentation/lifecycle checks; they do not establish end-to-end tutoring correctness. MAI-UI download and inference are outside this checkpoint.

## File changes outside the snapshot

- `README.md` modified: active solution, commands, preview mode and honest runtime status.
- `docs/README.md` modified: current documentation entry points and historical-plan status.
- `docs/HACKATHON_COMPLIANCE.md` modified: user-supplied reuse rule, prior-work disclosure and dependency/model status.
- `docs/POINTLY_IMPORT.md` created: this provenance, validation and complete copied-file inventory.

No existing source files were modified or deleted.

## Copied file inventory

Paths are relative to this repository. Hashes identify the imported snapshot, not future edited versions.

| Created file | SHA-256 at import |
|---|---|
| `Pointly.App/app.manifest` | `eff742c232ca5177f1f7aa298656dcaf8f8c15e6439ec710c6077abf2ba2c52b` |
| `Pointly.App/App.xaml` | `601a04205514431f98262f0e9e65ca7559cb23bd39b56747868eeeaea79f83bb` |
| `Pointly.App/App.xaml.cs` | `9f662b92ef39e540e55712eaf451017fd45b2a5b9d9d0e727ffc0b58b8262748` |
| `Pointly.App/AssemblyInfo.cs` | `d1e00464caeeb68937cbc604712c1888ace97ed2220fc4e683077c0538b0cbbd` |
| `Pointly.App/MainWindow.xaml` | `2b37e8af742c4aa4a01cf613549c36f5cec1ea003fe8edfc84f547a3e41e440c` |
| `Pointly.App/MainWindow.xaml.cs` | `778055c3ad51b4e4649442c88b9a1da3c4d3a647bd2ad10f4e940e54edf796e0` |
| `Pointly.App/Pointly.App.csproj` | `17d55526563f0998f5c97252e2550b653cf6310659e969d0f22e828e677e1307` |
| `Pointly.App/Assets/Pindo.ico` | `345b9cd0873a9b07e83ea6d34835debbbdd9f56ed9b3a566f0ef5d2d989e258f` |
| `Pointly.App/Automation/UiAutomationService.cs` | `2dcd1ca1f77549d51248608294545f37ec5664fe1df82e82f7e7fa4537daa01c` |
| `Pointly.App/Automation/UiaWorkScheduler.cs` | `37adaa634b895b220ce382e859fbb2fe81d71868ed81f10e557596af9a354f88` |
| `Pointly.App/Automation/UiElementInfo.cs` | `fe7f8455f4bea55ea34fcd772117719389f24315baabb72a87269c76c0575d24` |
| `Pointly.App/Capture/CaptureDebugPng.cs` | `cc5450fa1e8ce5d88050f09a6f879ff1839ae24afe396baabdd400bb94d742a1` |
| `Pointly.App/Capture/CapturePngEncoder.cs` | `2eb798dada9731f2e34ad03152c53c29886df127674aa6e3f622241cff4e9c55` |
| `Pointly.App/Capture/GraphicsCaptureInterop.cs` | `1d67b28da36f85c3171cb96925690c5aea9645bde5b45e02016b7dcc5038ce43` |
| `Pointly.App/Capture/WindowCaptureResult.cs` | `a8a04b662bad1b5bd322ed156d796fadd1b51c7e391414e938f5f1322ddf67c8` |
| `Pointly.App/Capture/WindowCaptureService.cs` | `7c8c785d16b91f797bbfb0061be73596d9735f3cb21235cd5745aad3f4d2e7f7` |
| `Pointly.App/Input/GlobalHotkey.cs` | `b122b8702824eed09e5b3eddf115522a4b7ce0cbeca68910f348de4bd1abbdc1` |
| `Pointly.App/Interop/NativeMethods.cs` | `96f0a3d409b005724390c4bd8163db42fa53b53510e63938f5bac3379e8893c5` |
| `Pointly.App/Overlay/HighlightOverlay.cs` | `e0f83726ce145cbd5aa710cfc8526a23e638f796d436f795c34afe84c810b318` |
| `Pointly.App/Presentation/BuddySurface.cs` | `2ef55c9198f08d5c37ee36fb0d3b424f596ddc4d7fcfded89497f76fd9a5bdd7` |
| `Pointly.App/Presentation/DesktopGeometry.cs` | `83270d2b657d270333dba1b69b4e98e95b310bb940989b25589dd112d2a973d1` |
| `Pointly.App/Presentation/GuidancePresenter.cs` | `bc6401d06d4f2df7e06a9a039cb734c0b2c831abc8fcbc78420a7dba1456a97b` |
| `Pointly.App/Presentation/PresentationGeometry.cs` | `5b73dc82fb1bf9cd591f618d3448c63416f43b1558c9f50150fee1222f9ab59c` |
| `Pointly.App/Shell/TrayShell.cs` | `8c8a09280171a48087d6d202324efdddafd76d7386f28fb35a82e00fc1ab1368` |
| `Pointly.App/Tutor/AssemblyAiTutorModel.cs` | `6d90377937bfe41e492ab21ab8f956c4d9aa8d1fd3a3be60d41c726bff50642e` |
| `Pointly.App/Tutor/TutorContracts.cs` | `7aadbb910bbd76025dbb11b63f5c4ca64021c9a3f9db2341422ef8d0ebfd325c` |
| `Pointly.App/Tutor/TutorService.cs` | `60a7e045cae6ffb61e22f122413d47377d8b9bbde3921385c799656ff85bc39b` |
| `Pointly.App/Verification/ClickVerificationContracts.cs` | `4251de87d59b959a44fe0b51f42e03d6dc0a64651c5f72900d3313d7e8f02548` |
| `Pointly.App/Verification/ClickVerificationService.cs` | `a2b9121576dca4da557222a90d7a7a67176d6a1d6bae1e2f4b27f009c1ea66cc` |
| `Pointly.App/Verification/ClickVerificationState.cs` | `dca5fd2b0b768b677c6ffef8d6d4a195637f541c2864890da24323aac80c922f` |
| `Pointly.App/Verification/InputForegroundPolicy.cs` | `5436ed426cf69efcf6154b3dc3942ce1ee92775da5175d9dbd5a405455a73c54` |
| `Pointly.App/Verification/KeyboardVerificationService.cs` | `456d5065717c45d4da70409ea3380821a58c8af42260f475bcbf48ffaa141fa2` |
| `Pointly.App/Verification/LowLevelKeyboardHook.cs` | `4b54988b1f641c4e9c0973873bd3565010757a702321591cf8c7194c3b43eea3` |
| `Pointly.App/Verification/LowLevelMouseHook.cs` | `fbb16619ac37e2f6d2d5075965f308d933466d75c2d2bd9d4d963e9bf638557d` |
| `Pointly.App/Verification/StepVerificationCoordinator.cs` | `d7eb8fc547417f7c0197ba3f463038793b229e32b2d92a6b8733f20b58eed1b9` |
| `Pointly.App/Verification/TextEntryVerificationService.cs` | `e6213498c769c0966e1ddf60bb9e76528f15b963ae2a960b76e159cedb4f552e` |
| `Pointly.App/Verification/UiAutomationStateVerifier.cs` | `e26860acc85cda4ccb481baeba6ca5801e517fd3c1544a0cde6ee3f932d08a0c` |
| `Pointly.App/Vision/BedrockNovaVisionModel.cs` | `262f5e39f9318644077697e43f62069c4416873e09ff9c8843ef64e7bb063307` |
| `Pointly.App/Vision/GuiGroundingContracts.cs` | `257fd1f159ce8516e1c0afb86d318f3b4bd4c3c00195637ed79cffeb42032b18` |
| `Pointly.App/Vision/GuiGroundingModelFactory.cs` | `db7dc7bcdea26c88d8869929aa8286e5b2033d2ae90eebab8e19319d867a9fb4` |
| `Pointly.App/Vision/GuiGroundingService.cs` | `b03687c9c4ee969d0513e6e4874d18b6d447e7f4f8f890a7ab23861b7e1fbd08` |
| `Pointly.App/Vision/OpenRouterGroundingModel.cs` | `a3acf8a81d6d2170a261d00102a52d38c798971e7a3177681a9cd1203d8f3ecf` |
| `Pointly.App/Vision/VisionCoordinateConverter.cs` | `c494c8a4f730cbf1aa9023a56c7a35eba29e1e66b32a9bff57681b9382a300b7` |
| `Pointly.App/Vision/VisionDebugOptions.cs` | `a96398be6341bcc366c318a77f3df9870f1cc05e2c8e0336bbb4d52d0ff5b0e7` |
| `Pointly.App/Voice/DefaultMicrophone.cs` | `b6531c8669e0c6910ae415ac6bc170bedabcd72b3a2d5d983a50c642c5327172` |
| `Pointly.App/Voice/ElevenLabsCredentials.cs` | `6e70bdacdd027ed3e9b7d96d38733d0f406598b5a89528abf489a84cdcdf6544` |
| `Pointly.App/Voice/ElevenLabsSpeechToText.cs` | `7e4aa584887e3fb92d2f6ec559aab07af7dc6589b7083f74d7e4033c0fe4df2c` |
| `Pointly.App/Voice/ElevenLabsTextToSpeech.cs` | `1614d92076e39fb48139b48d4b6392dfcb8680d873e6f6bb43bd90bf311009df` |
| `Pointly.App/Voice/SpeechAudioDiagnostics.cs` | `f8e57c68c089b4e80bde76cf8bd9de7854f238c4515765379fe98fc034afda51` |
| `Pointly.App/Voice/VoiceContracts.cs` | `e127e8ca8294aad6ab48be6b3679a2b2b470f7802ed8eb9ad4d9a97b2a47b4e0` |
| `Pointly.App/Voice/VoiceSession.cs` | `e1e743bd4ee19cf4fc4059be81e2f811e1c9c999f16e9f0ffb281db531633607` |
| `Pointly.App/Voice/VoiceSession.Persistent.cs` | `bf00640227efe9d827c04284b38cd255236b3ddb3740c9ad642d8408859b818a` |
| `Pointly.App/Voice/WindowsAudioCapture.cs` | `eb556b9e55cc4dea881fa168b01baef06e183082ccfd8832367e880876a04d02` |
| `Pointly.App/Voice/WindowsAudioPlayback.cs` | `5befa28d8882dcff2e4d8c082cc11f45760d04bc252134e7f7b37800cc1dbe1f` |
| `Pointly.App/Walkthrough/WalkthroughService.cs` | `92ad1bb92973c32b1c507a211f1c3c48352be09b7e6a821fd0ffff6f86e600d0` |
| `Pointly.App/Windows/ForegroundWindowService.cs` | `ef71565ae9957e44ea520a99a5eee4f1cf55679f11d68fe517e6981701879698` |
| `Pointly.App/Windows/TargetContextWatcher.cs` | `7222ef97e5f082506a20e99231117398d79e1c14007ac612c25f276dae65d979` |
| `Pointly.App/Windows/TargetWindowContext.cs` | `bbfd43e9f81de02ef482690de3271dd414a49ebd85b112ec386071f29115f0e9` |
| `Pointly.Tests/ActionVerificationTests.cs` | `99404c1d812d030406e23b2f00e6206eca187661d08d8bb4f45df62729bc5595` |
| `Pointly.Tests/ClickVerificationTests.cs` | `bf99204db52b4fb6e6a985afc41b3832409097723a3744570d6b6bb0c84ac697` |
| `Pointly.Tests/D8LifecycleTests.cs` | `b36b019c97cdefd53a8a7ace425586795ce9100ca909899ac875b15e48d30a9a` |
| `Pointly.Tests/ElevenLabsCredentialsTests.cs` | `b8c48c28c88294f6804391100e969cc21df3ccd8093c647cf6c9434ddf903c39` |
| `Pointly.Tests/ElevenLabsSpeechToTextTests.cs` | `8f534ae5cec57491a6c0a79828caf877583f75bb339c3d7227a5db4a12300564` |
| `Pointly.Tests/PersistentVoiceTests.cs` | `015c2eeaed68b1a2f94abe52c8d4804c4751ba02c2e5056341a0f9a5e1702517` |
| `Pointly.Tests/Pointly.Tests.csproj` | `4ec4a3640709d4021f3f341fcff1f2f02dfdec9afb9f0867d285f3bafb1ca23b` |
| `Pointly.Tests/PresentationTests.cs` | `11ecc829ac47a69be843261d6e4351c7a3bc6e404f79f1080271cf5283e047b6` |
| `Pointly.Tests/SpeechAudioDiagnosticsTests.cs` | `6edfed3e36358fed5cf1e480461791ab17e7273baf83293949db37d83c387ed2` |
| `Pointly.Tests/VoiceSessionTests.cs` | `b7ede9c3db5a9ee49a0adc7a3b7387aa87d0b31607d387103aebbb648eae0783` |
| `Pointly.Tests/WalkthroughTests.cs` | `d1628ea16033756437c42039c2e89b2bc1effdf56f8beefc95cf6365dec88f5e` |
| `Pointly.sln` | `32adc24ef795a3a007117fba674c0abf5d9b9b648c198331e1992033b4c2ecc6` |
| `docs/pointly-baseline/D4.md` | `b7872706d8a7a1a62fc6ab5df4e9de79c9ec26f0bffaf41d36d7239e2e5645c3` |
| `docs/pointly-baseline/D5.md` | `c3bf4016ba3d4cf3a004032b9b354ed976afc774af6951dc9569c50e2450f8cc` |
| `docs/pointly-baseline/D6.1.md` | `a7dca828e4a7c4b794d25c2459901ca0a6732a793963a72423fb6e7810c88d5e` |
| `docs/pointly-baseline/D6.2.md` | `3ab2e72afafb630a19e02cfaae1363f99002723832f2e5c6bba584a227528dfe` |
| `docs/pointly-baseline/D6.3.md` | `9a9dae60acba60cdd1f79fbe2fa4399a20d15583be4e8f50ddd6e876e061e711` |
| `docs/pointly-baseline/D7.md` | `2b1425e6d506d6270c9d3aa269d6612f3604e617debfb81846579aa5a5cee99c` |
| `docs/pointly-baseline/D8.md` | `97198cce22b9b261ab6d0a38860b7814ec2e46d9800ae1bda10b0bc70fa9dea3` |
| `docs/pointly-baseline/DELIVERABLES.md` | `b3f41900f90378a344bf60ace4f2792b20eaf04662826314ee654cc64c09d308` |
| `docs/pointly-baseline/PRODUCT.md` | `f880c89ac4d0a1c915f192e37df0e349f9473158ce85917906eee48520194327` |
