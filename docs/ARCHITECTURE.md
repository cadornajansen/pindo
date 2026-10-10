# Pindo macOS architecture

This describes the code as it is now. [PLAN.md](../PLAN.md) is the original plan and is kept for history.

```text
Fn + Space → QuickBar (SwiftUI panel) → IntentPolicy ──┬─ question / Do → Agent (Accessibility actions)
                                                        ├─ Guide        → GuideSession → Grounder (AX + screenshot) → PointerOverlay
                                                        └─ Teach        → TutorSession (skills library)
                       all model calls → Ollama, local MAI-UI 8B  (or GPT Luna via OpenRouter when switched on)
```

## Modules

| File | Role |
|---|---|
| `PinDoApp.swift` | Menu bar app and Settings window. Fn + Space event tap, single-instance guard, cleanup on quit. |
| `QuickBar.swift` | The panel and `QuickBarModel`, the one request lifecycle (see below). Voice, speech and cloud-answer fallback. |
| `IntentPolicy.swift` | Deterministic routing of the wording to question / guide / act / teach / clarify (English, Taglish). |
| `Agent.swift` | Do mode: Accessibility snapshot → one model action → validate → execute → repeat. Also `Ollama` and `Skills`. |
| `Grounding.swift` | Guide mode targeting. Collects AX elements, captures the window (ScreenCaptureKit), asks the model, validates the target. |
| `GroundingGeometry.swift` | Pure coordinate maths: model 0–1000 points → image → global screen → AppKit; Retina and multi-display. |
| `GuideSession.swift` | Guide's step loop: point, wait for the user's click in the same app, look again. |
| `PointerOverlay.swift` | The click-through pointer window (excluded from screen capture). |
| `TutorSession.swift`, `TutorClient.swift`, `TutorObservation.swift`, `TutorView.swift` | Teach mode. |
| `SkillLibrary.swift` | Loads and validates `Resources/ApplicationSkills.json`; app matching by bundle id or web host. |
| `Cloud.swift` | Optional providers (AssemblyAI, ElevenLabs, OpenRouter), the Keychain credential store, recorder and player. |
| `Config.swift` | Every non-secret setting, defaults and validation. |

## Models

- **Local (default):** `maternion/mai-ui:8b` through Ollama `/api/generate`.
  - Raw Qwen chat format with an empty `<think>` block, because the qwen3vl family otherwise reasons for tens of
    seconds.
  - The output is constrained by a JSON schema grammar, with one variant per action.
- **Cloud (opt-in):** `openai/gpt-6-luna` through OpenRouter chat completions in JSON mode.
  - It gets the same system prompt and user content. Guide's screenshot is attached as an image.
  - The model call is the only provider-specific code: `Grounder.ask` and `Ollama.nextAction` pick the transport.
    Validation and execution are shared.

## Grounding (Guide)

1. **Collect.** Read up to 150 labelled controls of the front window, plus the menu bar, through Accessibility.
2. **Choose what the model gets.**
   - **Enough readable controls** (at least 4, not counting menu-bar items): a text-only request.
   - **Too few, a web editor matched by a skill (Canva), or the same target came back after the user acted:**
     ScreenCaptureKit captures that one window (excluding Pindo's own windows), at most 1280 px, as JPEG.
   - **Web editors:** the model gets *no* control list and must point at what it sees.
3. **Validate.**
   - An element answer must use an id from this snapshot, and its frame is re-read.
   - A point must fall inside the captured image, not in letterbox padding, and the window must not have moved.
   - Anything else is rejected.

## Action safety (Do)

Before each action, Pindo checks:

- **Accessibility permission.** Without it, Pindo explains how to grant it instead of guessing.
- **Same app.** The front app must be the one that was observed, both before and after the model answers.
- **A real id.** The id must be in the current snapshot. Invented ids (like `<id of a textfield>`) are rejected.
- **The right kind of control.** `type` only targets text fields, areas and combo boxes. It falls back to key
  events only when that field really has focus.
- **Questions never act.** This is enforced by the schema locally and in code for the cloud.
- **Opening things.** `open_app` and `open_url` only open what the task names.
- **Risky steps.** Send, Delete, Buy, Quit and risky shortcuts need the user's Allow.
- **Loops stop.** An identical repeat, or 3 rejected proposals in a row, ends the task with the real reason.

Some apps get dedicated handling:

- **Excel:** the Name Box selects cells, and the formula bar is filled with real keystrokes (Tab between cells,
  Return between rows), because Excel ignores Accessibility text there.
- **Finder:** a new item's "untitled" name field is replaced, then committed.

## Request lifecycle

`QuickBarModel` owns one `generation` counter:

- **Every new request, cancel or quit increments it.** Each async result carries the value it started with, and
  stale results are dropped before they touch the UI or act.
- **No overlap.** A request is refused while another is running. Recording, transcription and speech never overlap.
- **Guide has its own counter**, and it pauses when the user switches apps.

## Credentials

Keys are stored only in the Keychain (generic passwords, service `com.pindopro.PinDo`).

- **Reads are cached per launch** and always happen off the main thread, so an authorization prompt can't freeze the UI.
- **Ad-hoc signing:** every rebuild is a new identity, so macOS asks again for the first read after a rebuild.
  Stable signing removes this.
- **Debug only:** builds may use a chmod-600 developer key file, but only when no Keychain entry exists.

## Tests

The tests are pure Swift checks run with `swiftc`, plus the Python skill validators:

- Intent routing.
- Coordinate geometry and parser.
- Provider response handling.
- Settings validation.
- Skill matching.

CI runs them on `macos-26` (`.github/workflows/skills.yml`).
