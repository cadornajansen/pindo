# Pointly

## Product

Pointly is a Windows-native AI software tutor.

Users ask Pointly how to perform something inside the application they currently have open.

Pointly sees enough of the current Windows UI to understand context, explains the next action, and visually points directly at the relevant control.

Example:

User opens Excel.

User presses the Pointly hotkey and asks:

> How do I create a PivotTable?

Pointly:

1. understands that Excel is active
2. identifies the relevant UI controls
3. says "First, open Insert"
4. highlights the Insert tab
5. observes the resulting UI state
6. gives the next instruction
7. continues until the task is complete

## Core Product Loop

```text
User invokes Pointly
        ↓
Voice/text request
        ↓
Foreground window context
        ↓
UI Automation inspection
        ↓
Can target be resolved locally?
        │
   yes ─┴─ no
    ↓       ↓
  UIA     Vision fallback
    │       │
    └───┬───┘
        ↓
Tutor reasoning
        ↓
Structured next step
        ↓
Windows overlay
        ↓
User performs action
        ↓
Observe state change
        ↓
Verify step
        ↓
Next instruction
```

## Primary Differentiation

Pointly is not just an AI chat window.

Its core value is:

> Understand what I am looking at and show me exactly what to do.

The defensible engineering layer is:

- Windows UI understanding
- target grounding
- low-latency overlays
- state verification
- tutoring state management
- hybrid semantic + visual perception

## Initial Applications

Development and testing should prioritize:

1. Microsoft Excel
2. Chrome
3. Canva in Chrome
4. CapCut

Excel and Chrome should be used first because their behavior gives us useful UI Automation test cases.

## Runtime Architecture

### Windows Client

C# / .NET 10 / WPF.

Responsibilities:

- application lifecycle
- global hotkey
- foreground window detection
- UI Automation
- screenshot capture
- overlays
- local state
- audio
- provider clients
- latency instrumentation

### UI Automation

Primary semantic perception mechanism.

Extract useful information such as:

- Name
- AutomationId
- ControlType
- BoundingRectangle
- IsEnabled
- HasKeyboardFocus
- supported patterns

Avoid serializing gigantic UI trees unnecessarily.

### Screenshot Vision

Windows.Graphics.Capture captures the active window when visual perception is required.

Cloud vision must be event-driven rather than continuous.

### AssemblyAI

Use the existing AssemblyAI credits for:

- realtime speech-to-text
- LLM Gateway
- tutor reasoning
- structured responses

### Amazon Bedrock

Initial multimodal experiment:

Amazon Nova.

Responsibilities:

- screenshot interpretation
- visual grounding
- OCR when needed
- locating controls unavailable through UI Automation

Vision should be a fallback, not the default path.

### Jev

Use for fast, constrained decisions where appropriate.

Examples:

- did the user complete the step?
- did the UI meaningfully change?
- does this require vision?
- is expected state satisfied?
- should the request escalate?

Do not use Jev as the primary conversational model.

## Overlay

Pointly renders guidance using a transparent always-on-top WPF overlay.

Possible annotations:

- rectangle
- circle
- arrow
- tooltip
- pulse/highlight

The overlay must not prevent the user from interacting with the underlying application.

Pointly's own overlay should be excluded from screen capture when practical.

## Performance Philosophy

Never default to:

```text
screen → cloud → wait → action
```

Prefer:

```text
UIA/cache/local state
       ↓
immediate response
       ↓
vision verification only when required
```

Avoid unnecessary cloud round trips.

## Privacy

Screen information is sensitive.

Principles:

- capture only when needed
- process locally whenever possible
- do not continuously record the desktop
- do not retain screenshots by default
- clearly indicate when Pointly is active
- cloud transmission must be intentional and limited

## MVP

The MVP succeeds when Pointly can reliably guide a user through a simple task inside Excel.

Minimum demonstration:

```text
Excel open
→ invoke Pointly
→ ask "How do I create a PivotTable?"
→ Pointly highlights Insert
→ user clicks Insert
→ Pointly detects the change
→ Pointly highlights PivotTable
```

## Non-Goals for MVP

Not part of the first product:

- autonomous computer operation
- generic desktop agent
- cloud account system
- collaboration
- billing
- mobile application
- browser extension
- persistent screen recording
- complete support for every Windows application

First make the tutoring loop excellent.
