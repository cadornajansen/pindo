# Pointly Deliverables

Only work on the current deliverable unless explicitly instructed otherwise.

---

## D0 — Project Foundation

Status: CURRENT

Deliverables:

- WPF application builds on .NET 10
- repository instructions established
- Git repository clean
- no unnecessary dependencies
- application can launch successfully

Acceptance:

```powershell
dotnet build Pointly.sln -c Debug
```

must succeed.

---

## D1 — Windows Invocation

Deliverables:

- application can remain running
- register global hotkey
- initial hotkey: Ctrl + Space
- determine foreground HWND
- obtain foreground process/window title
- safely unregister hotkey on shutdown

Acceptance:

Open Excel or Chrome.

Press Ctrl + Space.

Pointly detects the correct foreground application and logs:

- HWND
- process name
- window title

No duplicate hotkey events.

---

## D2 — UI Automation + Overlay

Deliverables:

- inspect foreground window using UI Automation
- extract useful UI elements
- query elements by name/control type
- retrieve bounding rectangle
- transparent topmost overlay
- overlay does not intercept normal clicks
- highlight arbitrary UIA element

Primary test:

Excel.

Acceptance:

Pointly finds the Excel `Insert` tab and visually highlights its actual screen bounds.

This is the first major technical proof.

---

## D3 — Screen Capture

Deliverables:

- capture foreground application
- Windows.Graphics.Capture
- correct sizing with DPI scaling
- basic multiple-monitor correctness
- prevent Pointly overlay from polluting screenshots when possible
- capture only on request

Acceptance:

Captured image corresponds accurately to the active window and coordinates align with overlay coordinates.

---

## D4 — AI Tutor

Deliverables:

- AssemblyAI LLM integration
- typed tutor request
- structured tutor response
- UIA context supplied to tutor
- select next UI target
- render returned instruction

Acceptance:

User asks:

> How do I create a PivotTable?

Pointly resolves `Insert` from the actual UI tree and highlights it based on AI reasoning.

---

## D5 — Multimodal Grounding

Deliverables:

- Amazon Bedrock client
- Nova multimodal request
- screenshot + task context
- normalized visual bounding boxes
- coordinate conversion
- UIA-first / vision-fallback routing

Acceptance:

Pointly can locate at least one target in an application where UI Automation is insufficient.

Benchmark against:

- Excel
- Chrome
- Canva
- CapCut

Measure:

- grounding accuracy
- latency
- API usage

---

## D6 — Step Verification

Deliverables:

- observe UI Automation events
- detect meaningful screen/UI changes
- maintain tutorial state
- expected state representation
- Jev integration for constrained classification where useful
- proceed automatically after successful user action

Acceptance:

Pointly can guide at least a two-step workflow without the user manually saying "next".

---

## D7 — Voice

Deliverables:

- microphone capture
- AssemblyAI realtime STT
- streaming transcript
- send completed utterance into tutor
- TTS abstraction
- interruption/cancellation behavior

Acceptance:

User completes a supported tutorial using voice without typing.

---

## D8 — MVP Hardening

Deliverables:

- latency instrumentation
- caching
- failure recovery
- multi-monitor testing
- DPI testing
- privacy controls
- logging
- installer/package
- clean startup/shutdown
- private-beta quality

MVP target:

Reliable guided workflows in selected Windows applications.

---

## Later

Not scheduled until MVP proves demand:

- autonomous actions
- mouse/keyboard control
- app-specific courses
- learner progress
- Taglish mode
- accounts
- payments
- cloud sync
- organization training
