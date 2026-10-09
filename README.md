# PinDo Pro

A local-first AI buddy for macOS that sees your screen, hears your voice, talks back, and
points at what to click. It runs on-device (Apple Silicon, tuned for an M4 Pro with 24 GB),
so it's private and unlimited. Cloud reasoning and voice are optional (bring your own key).

**Status:** working prototype (Guide, Do, Teach; local Qwen3-VL). See [PLAN.md](PLAN.md) for the architecture, MVP milestones (M0–M7) and Phase 2 hyper-local computer use (M8–M11) for Excel, Word, PowerPoint, Canva and more.

## Run (M1 slice: `Fn + Space` quick bar)

Requirements: macOS 15+, Xcode 26, [Ollama](https://ollama.com) running locally.

```bash
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer xcodebuild -project PinDo.xcodeproj -scheme PinDo -derivedDataPath build build
open build/Build/Products/Debug/PinDo.app
```

Or open `PinDo.xcodeproj` in Xcode and press ⌘R.

1. On first launch, allow **PinDo** in *System Settings → Privacy & Security → Accessibility*. The hotkey starts within 2 s, with no relaunch.
2. Press **`Fn + Space`** anywhere and type a request. There's no mode to pick: Pindo decides from the wording
   ([`IntentPolicy`](PinDo/IntentPolicy.swift), English and Taglish):
   - **Show me where** ("How do I…", "Where is…", "Paano…", "Saan…"): **Guide** points at the control with the yellow
     pointer; you click, then Pindo looks again and shows the next step (**Check again** re-checks, **Stop** ends).
     Accessibility first; a screenshot goes to the local model only when the window's controls aren't enough.
   - **Do it / answer** ("Make it bold", "Open Excel", "Summarize this email", questions): Pindo acts through
     Accessibility, one step at a time, and asks before Send, Delete, Buy, Quit and similar actions.
   - **Teach me** ("Teach me…", "Walk me through…", "Turuan mo ako…"): a lesson from the skills library.
   - **Unclear** ("Help me with this"): one question, *Show me* or *Do it for me*.
   While it works, the bar becomes a slim status row; `Esc` or ✕ stops. `Esc`, `Fn + Space` or clicking
   elsewhere closes the bar.
   Guide also needs **Screen Recording** (for windows whose controls aren't readable).
3. There's no `Fn` key on your keyboard? Use the hand icon in the menu bar → *Open Quick Bar*.

The model defaults to `qwen3-vl:8b` (`ollama pull qwen3-vl:8b`). Switch it with:

```bash
defaults write com.pindopro.PinDo model <ollama-model-name>
```

**Signing note:** the project signs ad-hoc ("Sign to Run Locally"), so macOS may forget the Accessibility grant after a rebuild.
To fix that for good, pick your Apple ID team under *Signing & Capabilities* in Xcode (a free account works).

## Application skills and Teach mode

Ask Pindo to **teach** you something ("teach me pivot tables") to learn one step at a time while you operate
the app. The panel stays open and watches the selected window locally. Pause, resume and result confirmation
are available.

The library contains **109 tasks across 15 application areas**, **330 task cases**
and **15 shared runtime scenarios**. Office, CapCut, Figma web, Canva web and
Photoshop are the first hands-on test set. The broader catalogue includes FL
Studio, Illustrator, Premiere, After Effects and macOS utilities. All workflows
remain hands-on unverified.

```powershell
py -B tools/application_skills.py --check-bundle PinDo/Resources/ApplicationSkills.json
py -B -m unittest discover -s tests -v
```

See [the skill contract](docs/APPLICATION_SKILLS.md), [catalogue](docs/SKILL_CATALOGUE.md)
and [Mac continuation guide](docs/MAC_HANDOFF.md). Python checks run on Windows;
the app requires macOS. GitHub CI builds it and checks Swift progression guards
and packaged resources. Teach also requires Screen Recording permission and a
local runtime supporting image input.

## Developer checks

```bash
swiftc -swift-version 6 PinDo/GroundingGeometry.swift tests/GroundingTests.swift -o build/grounding-tests && build/grounding-tests
swiftc -swift-version 6 PinDo/IntentPolicy.swift tests/IntentPolicyTests.swift -o build/intent-tests && build/intent-tests
defaults write com.pindopro.PinDo groundingDebug -bool true   # saves what Guide saw/answered to $TMPDIR/pindo-grounding/
```

`tests/InaccessibleTarget.swift` opens a window whose buttons are invisible to Accessibility (vision fallback test).
