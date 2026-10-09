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
2. Press **`Fn + Space`** anywhere. The quick bar appears at the bottom of the window you're in. Type a question and press ↩. `Esc`, `Fn + Space` or clicking elsewhere closes it.
3. There's no `Fn` key on your keyboard? Use the hand icon in the menu bar → *Open Quick Bar*.

The model defaults to `maternion/mai-ui:2b`. Switch it with:

```bash
defaults write com.pindopro.PinDo model qwen3-vl:8b
```

**Signing note:** the project signs ad-hoc ("Sign to Run Locally"), so macOS may forget the Accessibility grant after a rebuild.
To fix that for good, pick your Apple ID team under *Signing & Capabilities* in Xcode (a free account works).
