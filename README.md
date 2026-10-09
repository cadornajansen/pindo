# PinDo Pro

A local-first AI buddy for macOS that sees your screen, hears your voice, talks back, and
points at what to click. It runs on-device (Apple Silicon, tuned for an M4 Pro with 24 GB),
so it's private and unlimited. Cloud reasoning and voice are optional (bring your own key).

**Status:** planning. See [PLAN.md](PLAN.md) for the architecture, MVP milestones (M0–M7) and Phase 2 hyper-local computer use (M8–M11) for Excel, Word, PowerPoint, Canva and more.

## Run (M1 slice: `Fn + Space` quick bar)

Requirements: macOS 15+, Xcode 26, [Ollama](https://ollama.com) running locally.

```bash
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer xcodebuild -project PinDo.xcodeproj -scheme PinDo -derivedDataPath build build
open build/Build/Products/Debug/PinDo.app
```

Or open `PinDo.xcodeproj` in Xcode and press ⌘R.

1. On first launch, allow **PinDo** in *System Settings → Privacy & Security → Accessibility*. The hotkey starts within 2 s, with no relaunch.
2. Press **`Fn + Space`** anywhere. The quick bar appears at the bottom of the window you're in. Type a question or a task and press ↩. `Esc`, `Fn + Space` or clicking elsewhere closes it.
   - **It acts in the app you're in.** Try "type 'Hello' in the document", "select all and make it bold", or "open Excel". PinDo reads the app's controls and menu commands through Accessibility, and the local model picks one action per step (max 8 steps). It asks before Send, Delete, Buy, Quit and similar actions, and never touches password fields. While it works, the button becomes **Stop** (`Fn + Space` stops it too).
3. There's no `Fn` key on your keyboard? Use the hand icon in the menu bar → *Open Quick Bar*.

The model defaults to `qwen3-vl:8b` (`ollama pull qwen3-vl:8b`). Switch it with:

```bash
defaults write com.pindopro.PinDo model <ollama-model-name>
```

**Signing note:** the project signs ad-hoc ("Sign to Run Locally"), so macOS may forget the Accessibility grant after a rebuild.
To fix that for good, pick your Apple ID team under *Signing & Capabilities* in Xcode (a free account works).

## Application skills (first checkpoint)

The first three macOS teaching skills and evaluation cases can be checked on a
Windows development machine with Python; the native app still requires a Mac.

```powershell
py -B tools/application_skills.py
py -B -m unittest discover -s tests -v
```

See [the skills contract and validation notes](docs/APPLICATION_SKILLS.md).
These files are independent of the active agent; no hands-on or model validation
has been completed yet.
