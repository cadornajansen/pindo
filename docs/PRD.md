# Product requirements

## Problem and audience

Filipino educators learning unfamiliar software often switch between instructions and the application they are using. LocalTutor aims to show the next useful action directly within that application, using English, Filipino, or Taglish instructions.

Value proposition: **Learn software directly where you're working, with private, on-device AI guidance.** This is the product intention, not a verified privacy or offline claim for the scaffold.

## Intended experience

1. The teacher opens PowerPoint and presses `Ctrl + Space`.
2. LocalTutor remembers the original foreground window before taking focus.
3. The teacher asks, “Paano ako maglagay ng picture sa PowerPoint para sa lesson ko?”
4. A future UI Automation collector reads relevant accessible controls from that application.
5. A local model chooses one captured element ID and gives a short instruction.
6. LocalTutor validates the ID and highlights the control's actual screen rectangle.
7. The teacher performs the action and asks for the next step.

The assistant teaches; it does not autonomously operate the computer.

## Current milestone and future MVP

| Capability | Bootstrap status |
|---|---|
| Floating WPF assistant, text input, Submit/Enter | Implemented and smoke-tested on Windows 11 |
| Global `Ctrl + Space`, focus, cleanup, collision status | Implemented and smoke-tested on Windows 11 |
| Typed tutoring and utility contracts | Implemented |
| Central local Ollama settings and availability boundary | Implemented; no inference |
| UI Automation collection and filtering | Future |
| Structured local inference and target ID validation | Future |
| Highlighting and step-by-step tutoring | Future |
| Verified offline tutoring and language quality | Future |
| Approved PDF/conversion/media utilities | Future; Member 2 owns implementation |

PowerPoint is the highest-priority end-to-end demonstration. Excel, Word, File Explorer, Windows Settings, and Notepad are support goals to validate progressively through the same accessibility pipeline. Photoshop and Canva are experimental; no support is promised. Canva and other third-party services may need internet.

## Acceptance targets for the future tutoring path

- Median end-to-end latency ≤ 5 seconds; 95th percentile ≤ 10 seconds.
- At least 90% correct UI targets on a representative, labeled test set.
- Timing begins at command submission and ends when both instruction and highlight appear.
- Measure on the actual demonstration laptop, report hardware and sample count, and distinguish invalid output from an incorrect target.
- Verify English, Filipino, and Taglish prompts with concrete educator tasks.

These are **unverified engineering targets**. `qwen3:1.7b` is the initial candidate, not a proven choice. Earlier cloud-model experiments are background research and do not establish local grounding accuracy.

## Scope boundaries

No model training, fine-tuning, complex RAG, autonomous computer use, screenshot/vision fallback, full Photoshop/Canva grounding, or voice integration belongs in the bootstrap. Optional future cloud voice must remain independent of the core tutoring path.

Future utilities need typed input validation, an explicit allowlist, and user confirmation appropriate to the effect. A model must never supply arbitrary shell commands. Core tutoring should eventually remain usable without online utilities or cloud APIs, but that capability has not been implemented.

## Next checkpoint

Benchmark local Ollama inference, then capture a bounded UI Automation snapshot for a small PowerPoint scenario. Record failures before claiming broader application support. Further feature work requires a new, focused scope.
