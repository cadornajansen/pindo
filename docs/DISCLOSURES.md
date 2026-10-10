# Disclosures (AppBuildersPH Hackathon 2026)

## Prior work vs. new work

- **Pre-existing:** the original Pindo for Windows (C# / WPF), in [cadornajansen/pindo](https://github.com/cadornajansen/pindo).
  - The macOS app does not share code with it.
  - It reuses the *design patterns*: model-picked Accessibility ids, 0–1000 normalized grounding with validation,
    host-owned approvals, and session generation ids. See PLAN.md §8.
- **New (macOS):** everything in this repository.
  - The first commit is 2026-10-10 00:26 (+08:00). `git log` is the record.
- **Application skills library:** contributed by Jansen Viray (Cadorna) in PR #1 (`feat/application-skills`):
  - the skill contract;
  - validators;
  - Teach mode;
  - the first 109 lessons.

  The Canva add-text, add-shape and text-effects lessons and the Excel fill-table lesson were added afterwards.
  Most lessons are documentation-derived and marked unverified.

## Models and services

| Component | Use | Where it runs |
|---|---|---|
| **MAI-UI 8B** (`maternion/mai-ui:8b`, community Ollama build, qwen3vl architecture, Q4_K_M) | Default model for everything | Locally, via Ollama |
| **Qwen3-VL 8B** (`qwen3-vl:8b`) | Earlier default; used in the benchmark (docs/PINDO_FINAL_BENCHMARK.md) | Historical |
| **GPT Luna** (`openai/gpt-6-luna`) via OpenRouter | Optional stand-in for guidance, off by default | Cloud |
| OpenRouter (`openrouter/auto`) | Optional answers to typed questions when the local model is down | Cloud |
| AssemblyAI | Optional speech-to-text | Cloud |
| ElevenLabs | Optional text-to-speech | Cloud |
| Ollama | Local inference runtime | Local |

## Frameworks

Apple frameworks only, with no third-party Swift packages:

- SwiftUI and AppKit
- Accessibility (AXUIElement)
- ScreenCaptureKit
- AVFoundation
- Security (Keychain)

The validators and tests use Python 3 standard library only.

## AI development assistants

- **Claude** (Anthropic), through Claude Code. It wrote most of the macOS implementation, tests and documentation,
  with the team directing and reviewing the work.
- **AI coding agents** were also used on the Windows repository (for example, its `codex/` branch).
