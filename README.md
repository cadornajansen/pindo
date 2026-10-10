# Pindo (macOS)

A screen-aware AI assistant for macOS that helps teachers, students and everyday users find their way around
unfamiliar apps. It answers questions, **points** at the next control to click (Guide), **does** simple steps for
you (Do), and teaches step-by-step lessons (Teach), in English, Filipino and Taglish.

It runs on your Mac: the default model is **MAI-UI 8B**, served locally by Ollama. Cloud services are optional,
off by default, and visibly marked when on.

**Status:** demo-ready prototype with known limitations (see [Verified limitations](#verified-limitations) and
[the release report](docs/RELEASE_REPORT.md)). This is the macOS rebuild; the original Windows app lives in
[cadornajansen/pindo](https://github.com/cadornajansen/pindo).

## Requirements

- Apple Silicon Mac, macOS 15 or later (developed on a MacBook Pro M4 Pro, 24 GB, macOS 26.6.2)
- Xcode 26 to build
- [Ollama](https://ollama.com) 0.12.7 or later (developed on 0.40.2)
- About 6 GB free memory for the model

## Set up the local model (MAI-UI 8B)

```bash
ollama pull maternion/mai-ui:8b      # 6.1 GB, qwen3vl architecture, Q4_K_M
```

Pindo talks to Ollama at `http://127.0.0.1:11434` and keeps the model loaded. Nothing else is needed for local use.

## Build and run

```bash
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer xcodebuild -project PinDo.xcodeproj -scheme PinDo -derivedDataPath build build
open build/Build/Products/Debug/PinDo.app
```

Or open `PinDo.xcodeproj` in Xcode and press ⌘R. Pindo lives in the menu bar (hand icon).

### macOS permissions

| Permission | Needed for | When macOS asks |
|---|---|---|
| Accessibility | Reading app controls, Do mode, the Fn + Space hotkey | First launch |
| Screen Recording | Guide/Teach when an app's controls aren't readable (Canva, drawn UIs) | First screenshot |
| Microphone | Voice input only | First recording |

**Rebuilds and permissions:** the project is ad-hoc signed (no signing certificate on the development Mac), so
macOS treats every rebuild as a new app. Permissions then stop matching (`tccd: Failed to match existing code
requirement`). After a rebuild, reset and grant again:

```bash
tccutil reset ScreenCapture com.pindopro.PinDo; tccutil reset Accessibility com.pindopro.PinDo
```

The permanent fix is a stable signing identity: choose a Team (a free Apple ID works) under *Signing &
Capabilities* in Xcode. Grants then survive rebuilds.

## Using it

Press **`Fn + Space`** anywhere (or menu bar → *Open Quick Bar*) and type. There's no mode to pick; Pindo decides
from the wording ([`IntentPolicy`](PinDo/IntentPolicy.swift)):

- **Questions** ("What is…", "Ano ang…"): answered without touching the computer.
- **Show me where** ("How do I…", "Where is…", "Paano…", "Saan…"): **Guide** points at the control with a yellow
  pointer. You click, then Pindo looks again and shows the next step.
- **Do it** ("Type … in the document", "Open Excel", "Create a dropdown in B2…"): **Do** acts through
  Accessibility, one checked step at a time, and asks before Send, Delete, Quit and similar actions.
- **Teach me** ("Teach me…", "Walk me through…", "Turuan mo ako…"): a lesson from the skills library.
- **Unclear** ("Help me with this"): one question, *Show me* or *Do it for me*.

`Esc` or ✕ stops a request at any time.

## Optional cloud features (all off by default)

Menu bar → **Settings…** shows the local model's status and the opt-in switches:

| Switch | Provider | What is sent |
|---|---|---|
| Voice input | AssemblyAI | Your recording, only between clicking the mic and **Done** |
| Speak answers | ElevenLabs | The final answer text (≤ 600 characters) |
| Cloud answers when the local model is down | OpenRouter | Only a typed plain question |
| **Use cloud model for guidance** | OpenRouter, **GPT Luna** (`openai/gpt-6-luna`) | The prompt, the app's control list and, for Guide, a screenshot of the window |

While the cloud model is on, the bar shows **☁︎** next to the model name. Cloud is never switched on
automatically; a failed local request is never silently retried in the cloud.

### API keys

Paste each key into its field in Settings and press **Save**. Keys are stored only in your **Keychain**
(service `com.pindopro.PinDo`). They are never written to settings, files, logs or the app bundle. Pindo reads each
key from the Keychain at most once per launch, off the main thread.

Debug builds only: a developer key file `~/Library/Application Support/PinDo/<assemblyai|elevenlabs|openrouter>.key`
(chmod 600) is used **only when the Keychain has no entry**. It is not compiled into Release builds.

### Configuration

All non-secret settings are in [`Config`](PinDo/Config.swift). Change them in Settings or with
`defaults write com.pindopro.PinDo <key> <value>`. Invalid values are logged at launch and ignored.

| Key | Default | Notes |
|---|---|---|
| `model` | `maternion/mai-ui:8b` | Any Ollama model name |
| `ollamaEndpoint` | `http://127.0.0.1:11434/api/` | Must be on this Mac (loopback) |
| `inferenceTimeout` | `60` | Seconds, 5–300 |
| `cloudModelEnabled` | `false` | Settings switch |
| `cloudModel` | `openai/gpt-6-luna` | OpenRouter model id |
| `groundingDebug` | `false` | Debug: save what Guide saw to `$TMPDIR/pindo-grounding/` (includes screenshots) |

Precedence: a valid stored value, otherwise the default. No environment variables are read.

## What works, and what doesn't

Verified on the development Mac (details in [the benchmark](docs/PINDO_FINAL_BENCHMARK.md) and
[the release report](docs/RELEASE_REPORT.md)):

- **Local questions** in English, Tagalog and Taglish, about 1 s.
- **Typing** into a text field (TextEdit), checked by reading the field back.
- **Safety:**
  - Questions never act.
  - Switching apps mid-task stops it.
  - Non-text targets and invented ids are refused.
  - A cancelled request never acts later.
  - Missing permissions give a clear message.
- **Pointing at a drawn button** from a screenshot (4/4 in tests).
- **Voice input** (AssemblyAI → normal request) and **spoken answers** (ElevenLabs, with Stop).

### Verified limitations

- **Multi-step guidance in real apps is unreliable.**
  - In the benchmark, Finder, PowerPoint and Canva failed.
  - Office ribbon buttons aren't exposed to Accessibility.
  - Chrome exposes no page content.
- **Canva, Excel table fill and Finder folder creation** have fixes that were checked on saved prompts and
  screenshots, not yet live.
- **No file tools** (PDF merge, ZIP, conversion) are implemented in the macOS app.
- **PowerPoint** opened decks read-only on the development Mac (Office licence).
- **Permissions reset after every rebuild** (ad-hoc signing; see above).

## Application skills and Teach mode

[`PinDo/Resources/ApplicationSkills.json`](PinDo/Resources/ApplicationSkills.json) holds **113 lessons** with
**342 evaluation cases** across Office, Canva, Finder, Figma, Adobe apps, CapCut and more.
- **Teach** follows them step by step.
- **Do and Guide** use the matching lesson as a known procedure.

Most lessons are written from documentation and are marked unverified until tested hands-on. See
[the skill contract](docs/APPLICATION_SKILLS.md) and [catalogue](docs/SKILL_CATALOGUE.md).

## Developer checks

```bash
swiftc -swift-version 6 PinDo/IntentPolicy.swift tests/IntentPolicyTests.swift -o build/intent-tests && build/intent-tests
swiftc -swift-version 6 PinDo/GroundingGeometry.swift tests/GroundingTests.swift -o build/grounding-tests && build/grounding-tests
swiftc -swift-version 6 PinDo/Config.swift PinDo/Cloud.swift tests/CloudTests.swift -o build/cloud-tests && build/cloud-tests
swiftc -swift-version 6 PinDo/Config.swift tests/ConfigTests.swift -o build/config-tests && build/config-tests
swiftc -swift-version 6 PinDo/SkillLibrary.swift tests/SkillLibraryTests.swift -o build/skill-tests && build/skill-tests PinDo/Resources/ApplicationSkills.json
python3 -B tools/application_skills.py --check-bundle PinDo/Resources/ApplicationSkills.json
python3 -B -m unittest discover -s tests
```

`tests/InaccessibleTarget.swift` opens a window whose buttons are invisible to Accessibility (vision test).
See [ARCHITECTURE.md](docs/ARCHITECTURE.md) and [DISCLOSURES.md](docs/DISCLOSURES.md).
[PLAN.md](PLAN.md) is the original (historical) plan.
