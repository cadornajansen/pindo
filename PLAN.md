# PinDo Pro — MVP Plan

> A local-first AI buddy that lives next to your cursor. It sees your screen, hears your voice,
> talks back, and **points at the thing you need to click**. Think *Clicky*, but the brain runs
> on your MacBook, so it's free to use as much as you want, private, and works offline.
> The cloud is an optional power-up, not a dependency.

---

## 1. Your requirements → design decisions

| Requirement | Decision |
|---|---|
| **Local AI model (M4 Pro, 24 GB)** | A 4-bit vision-language model (VLM) in the 7–12B range (~5–8 GB) via MLX. That leaves headroom for macOS, the browser, STT and TTS. |
| **Good / decent device for local AI** | Target Apple Silicon only, with 16 GB as the minimum and 24 GB as the reference machine. Memory budget below. |
| **Annotation based** | A transparent, click-through overlay that draws a pointer, boxes, arrows and numbered steps on top of any app. The model returns coordinates and we draw them. |
| **User assistance** | Ask anything about what's on screen. It explains, answers, or points. |
| **Technical gap** | Plain-language mode, adjustable skill level ("explain like I'm new to computers" → "just tell me the shortcut"). |
| **Affordability** | No backend and no account. Local inference costs $0 per query. Cloud is BYOK (bring your own API key), so we never pay for a user's tokens. |
| **Tutor on online tasks** | Screen capture works on *any* app, browser included, with no extension needed. Multi-step guide mode walks you through a task. |
| **Siri on steroids** | Voice commands plus a small allowlist of safe actions (open app/URL, run Shortcut, set reminder, search). |
| **Cloud assisted** | A router escalates to the cloud for hard reasoning, long context, or premium voice. Off by default, with a visible indicator when it's on. |
| **Unlimited** | Local means no rate limits and no quota. |
| **Smooth & fluid** | A pre-built, non-activating frosted panel. Spring animations at 120 fps, < 100 ms to appear. See §4. |
| **Quick text bar (`Fn + Space`)** | A tap opens a floating text bar over the current window. Holding it is push-to-talk. |
| **Toolset around the cursor** | Long-press on blank space opens a radial menu of PinDo tools plus the current app's own commands. |
| **Multi-step + voice** | An agent loop (plan → show step → watch the screen → advance), driven by push-to-talk. |

---

## 2. Architecture

```
 ┌──────────────── PinDo Pro (native macOS menu-bar app, Swift) ────────────────┐
 │                                                                               │
 │  Hotkey (push-to-talk) ──► Mic ──► STT ──┐                                    │
 │                                          ▼                                    │
 │  ScreenCaptureKit ──► screenshot ──► ORCHESTRATOR ◄── Accessibility (AX) tree │
 │                                     (agent loop)                              │
 │                                          │                                    │
 │                         ┌────────────────┴───────────────┐                    │
 │                         ▼                                ▼                    │
 │                  ROUTER: local (default)          cloud (opt-in, BYOK)        │
 │                  VLM via MLX / LM Studio          Claude API, cloud TTS       │
 │                         │                                │                    │
 │                         └──────────► response ◄──────────┘                    │
 │                                   text + [POINT x,y] tags                     │
 │                         ┌────────────────┼───────────────┐                    │
 │                         ▼                ▼               ▼                    │
 │                  Overlay (annotate)   TTS (speak)    Safe actions             │
 └───────────────────────────────────────────────────────────────────────────────┘
```

**Why native Swift instead of Electron/Python:** ScreenCaptureKit, the Accessibility API, global
hotkeys and click-through overlay windows are all first-class in Swift. Electron would need
native modules for every one of them anyway.

### Stack

| Layer | MVP choice | Upgrade path |
|---|---|---|
| App | SwiftUI + AppKit (`NSPanel` overlay), menu-bar app | — |
| Local LLM runtime | **LM Studio or Ollama** over a local OpenAI-compatible HTTP API (fastest to build on) | In-process **mlx-swift** (`MLXVLM`) to ship as a single app |
| Vision model | Bake-off in M0: **Qwen3-VL-8B** (native pointing/grounding), Qwen2.5-VL-7B, Gemma 3 12B (all 4-bit) | Whatever wins the next bake-off; the model is swappable |
| Speech-to-text | Apple **SpeechAnalyzer** (on-device, macOS 26) or **WhisperKit** (large-v3-turbo) | — |
| Text-to-speech | `AVSpeechSynthesizer` (free, built in) | Kokoro-82M via MLX locally; ElevenLabs/OpenAI voice in the cloud |
| Grounding | VLM coordinates **snapped to the nearest AX element** (more accurate than vision alone) | — |
| Cloud reasoning | Claude API (`claude-sonnet-5-5` by default, `claude-opus-5-5` for "think hard") | — |
| Key storage | macOS Keychain | — |

### Memory budget (24 GB M4 Pro)

| Item | Approx. |
|---|---|
| macOS + browser + user apps | ~9–11 GB |
| VLM 8B @ 4-bit + KV cache | ~6–7 GB |
| STT (WhisperKit turbo) | ~1 GB (SpeechAnalyzer: ~0) |
| TTS + app | <1 GB |
| **Headroom** | **~4–6 GB** ✅ |

---

## 3. Speed: a few seconds, end to end

Speed is a feature with its own budget. Every milestone has to stay inside it.

### Latency budget (local path, M4 Pro)

| Moment the user feels | Target | How |
|---|---|---|
| "It heard me" (sound + glow) | **< 100 ms** | Play an earcon and animate the buddy the moment the hotkey is released, before any AI runs |
| Speech → text | **≤ 300 ms after release** | Streaming STT transcribes *while* you talk, so only the last word is left at release |
| Pointer moves to target | **≤ 1.5 s** | The model emits `[POINT …]` **first**, and the overlay acts on it mid-stream |
| First spoken word | **≤ 2 s** | Sentence-level streaming TTS starts on sentence 1 |
| Full short answer | **≤ 4 s** | Short answers by default (1–3 sentences). "Tell me more" gives a long one |
| Cloud path ("think hard") | first word **≤ 4 s** | A "thinking ☁️" cue covers the wait, and the response streams like the local one |

> These are targets, not measurements. M0 measures the real numbers on your Mac, and the plan adjusts if a stage misses.

### What makes it fast (in order of impact)

1. **Fewer image tokens.** Image size is the #1 cost. Send the **active window** (or a crop around the cursor) instead of the full display, downscaled to ~1024 px on the long side. Re-send at higher resolution only for a second "zoom" pass if pointing is unsure.
2. **Model always warm.** Load it at login and never unload it while in use (`keep_alive` in Ollama, or a pinned model in LM Studio). A cold load costs 5–10 s.
3. **Cached system prompt.** Keep the system prompt identical on every call so the runtime's prefix/KV cache reuses it. Only the image and the question get processed fresh.
4. **Parallel, not serial.** Grab the screenshot on hotkey **down**, while the user is still talking. STT, capture and the AX-tree read all overlap.
5. **No "thinking" mode.** Use *Instruct* model variants rather than *Thinking* ones, cap `max_tokens`, and tell the model to answer in 1–3 sentences.
6. **Fast path for commands.** "Open Spotify", "set a timer for 5 min" and similar commands get matched by simple rules and run in **< 500 ms**, with no screenshot and no VLM call.
7. **Right-sized model.** The bake-off includes a **4B** model. If it's accurate enough, it's roughly 2× faster than an 8B.
8. **Guide mode does the heavy work once.** Plan all steps in one call. Checking whether a step is done uses a small crop and a yes/no answer, which takes well under 1 s.

### Keep it fast
- Every request logs per-stage timestamps (capture, STT, first token, first `POINT`, first audio, done). A debug HUD shows them on screen.
- The M0 bake-off script doubles as a **latency regression test**. Re-run it after any model, prompt or pipeline change. If a stage goes over budget, the change doesn't ship.

---

## 4. Interaction design: smooth, fluid, out of the way

PinDo floats over whatever you're doing, appears instantly, and gets out of the way just as fast.
It never steals focus from the app you're asking about.

### One shortcut, two modes: `Fn + Space`

| Gesture | What happens |
|---|---|
| **Tap** `Fn + Space` | The **quick bar** slides up over the current window. Type, press ↩, and the answer streams in below it |
| **Hold** `Fn + Space` | Push-to-talk voice. Release to send |
| `Esc` / tap again | Dismiss. The bar fades out and focus stays in your app |

**Quick bar:** a single-line, rounded, frosted-glass bar (`NSVisualEffectView`). It has the PinDo mark on the left,
the placeholder *"What can I help you with?"*, a mode picker (Ask · Point · Guide) and a send button. It appears
centered near the bottom of the **active window**, not the screen, so it feels attached to what you're doing.
The answer grows downward from the bar in the same panel, and any `POINT` tags animate the buddy cursor at the
same time.

**How it's built:**
- Detect `Fn` with a `CGEventTap`. `Fn` is a modifier flag (`.maskSecondaryFn`) that normal hotkey APIs can't
  register. The tap also **swallows** the Space keypress so no space gets typed into your app.
- The panel is a **non-activating `NSPanel`** (`.nonactivatingPanel`, `canBecomeKey = true`). It takes your keystrokes,
  but the app underneath stays frontmost. That matters because it's the app we screenshot and point at.
- Gotchas: some keyboards (many external ones) have no `Fn` key, so the shortcut is configurable, with a fallback
  of `⌥Space`. Also check that *System Settings → Keyboard → "Press 🌐 key to"* doesn't conflict.

### Radial toolset: long-press on empty space

Press and hold the left mouse button on a **blank** part of any window (empty canvas, page margin, desktop) and
a ring of tools blooms around the cursor. **Flick toward a tool and release** to run it. This is a *marking menu*,
so it becomes muscle memory: after a week you flick without looking.

```
                 [ Ask PinDo ]
      [ app tool ]           [ app tool ]
   [ Explain this ]   (•)   [ Point me to… ]
      [ app tool ]           [ app tool ]
                 [  Voice  ]
```

- **Inner slots (always the same):** Ask (opens the quick bar), Voice, Explain this area, Point me to….
- **App slots (change per app):** the current app's most useful commands, read from **its own menu bar via the
  Accessibility API** and triggered with `AXPress`. This works in nearly every macOS app (Figma, Pages, Xcode,
  Chrome…) with no per-app integration. Each app's list is precomputed and cached when you switch to that app,
  so the ring opens instantly. Ranking starts simple (a hand-picked list for the top 10 apps, then most-used
  commands) and is user-editable later.

**How it's built (the tricky parts):**
1. **Long-press detection:** a `CGEventTap` sees `leftMouseDown` and starts a ~350 ms timer. Moving more than ~6 px
   or releasing early cancels it, so normal clicks and drags are never affected.
2. **"Blank" detection:** `AXUIElementCopyElementAtPosition` checks what's under the cursor. Buttons, links, text
   fields and other interactive elements → do nothing. Canvas, group, scroll area or window background → show the ring.
3. **Don't break the app:** the app already received the `mouseDown`. Once the ring opens, PinDo **swallows the drag
   events** and delivers the `mouseUp` at the **original** position. To the app it looks like a harmless click on empty
   space, with no marquee selection and no stuck drag.
4. Apps where long-press already means something important (drawing apps, games) go on a per-app exclusion list.

### What "smooth" means here (and how to keep it)

| Rule | How |
|---|---|
| Appears in **< 100 ms**, every time | Create the panels **once at launch** and keep them hidden. Showing one is just an alpha and position change |
| 120 fps on ProMotion | Core Animation / SwiftUI springs (`.spring(response: 0.3, dampingFraction: 0.8)`), with no layout work during animation |
| Nothing jumps | The answer panel grows with an animated height. Streamed text appends without reflowing what's already there |
| Buddy cursor moves like a hand | A curved path with ease-in-out and a small overshoot. It doesn't teleport |
| Never blocks | All AI, capture and AX work runs off the main thread. The UI only receives results |
| Respects the user | Honor *Reduce Motion* and *Increase Contrast*, adapt to light/dark, and keep every action keyboard-reachable |

---

## 5. MVP scope

**In:** the `Fn + Space` quick bar (tap) and push-to-talk (hold), the long-press radial toolset, voice and text Q&A about the screen, pointing/annotation overlay, multi-step guide
mode, local-by-default with opt-in cloud escalation, ~6 safe voice actions, onboarding for
permissions, and a signed DMG.

**Out (post-MVP):** autonomous clicking/typing, long-term memory, Windows/iOS, accounts/billing,
a plugin system, and always-on wake word. Each one adds a lot of risk without changing the
core "it shows me where to click" magic.

> **Principle: guide, don't drive.** The MVP *shows* you where to click; it doesn't click for you.
> That makes it safer, easier to build, and better for teaching. Driving comes after the MVP.

---

## 6. Step-by-step milestones

Each milestone has a hard **exit test**. Don't start the next one until the current one passes.
The estimates assume one developer working part time.

### M0 — Foundation & model bake-off · *week 1*
- [ ] Xcode project: menu-bar app, app icon, `Info.plist` usage strings (mic, screen recording).
- [ ] Install LM Studio/Ollama and pull 4 candidate VLMs: Qwen3-VL-**4B** and **8B**, Qwen2.5-VL-7B, Gemma 3 12B (all 4-bit, *Instruct* variants). Also time **AX-ID selection** (text-only: candidate list + question → element ID) on the same models plus Qwen3-1.7B.
- [ ] Build a test set: **20 screenshots**, each with a question and the correct click target
      (Gmail, Google Sheets, System Settings, Finder, VS Code, Canva, …).
- [ ] Script it: measure **time-to-first-token**, **total latency** and **pointing accuracy**
      (whether the predicted point lands inside the target box). Test each model at 2 image sizes (full display vs. active window at ~1024 px).
- **Exit:** pick the **fastest** model and image size with first `POINT` in **≤ 1.5 s** (warm model) and pointing accuracy **≥ 75%**.
  Write the results into `PLAN.md`.

### M1 — Eyes: "what am I looking at?" · *week 2*
- [ ] `CGEventTap` for **`Fn + Space`** (fallback `⌥Space`). A tap opens the **quick bar** (a non-activating frosted `NSPanel`, pre-created at launch), and the active window gets captured with ScreenCaptureKit.
- [ ] The answer streams into the panel below the bar with a spring-animated height. `Esc` dismisses it and focus stays in the user's app.
- [ ] Capture the **active window** downscaled to ~1024 px and remember the scale factor (Retina is 2×).
- [ ] Load the model at app launch and keep it warm. Use a fixed system prompt so the prefix cache hits.
- [ ] Per-stage timing log plus a debug HUD (see §3).
- [ ] Send the screenshot and a typed question to the local model, then stream the answer into a small floating panel near the cursor.
- **Exit:** asking "what does this error mean?" on 5 different apps gives a correct answer, with first text in **≤ 1.5 s**.

### M2 — Ears & mouth: voice round-trip · *week 3*
- [ ] **Hold** `Fn + Space` to talk and release to send (push-to-talk, not a wake word). A tap still opens the text bar.
- [ ] **Streaming** on-device STT transcribes while the user talks. The screenshot is taken on hotkey *down*, in parallel.
- [ ] Play an earcon and glow the buddy within 100 ms of release.
- [ ] Fast path: rule-matched commands ("open X", "timer for N min") skip the VLM entirely.
- [ ] Stream the LLM output into TTS **sentence by sentence**, so speaking starts before the full answer is done.
- [ ] Barge-in: pressing the hotkey again stops speech.
- **Exit:** spoken question to first spoken word in **≤ 2 s** (fast-path commands **< 0.5 s**), and it works with Wi-Fi off.

### M3 — Pointing & annotation (the core feature) · *weeks 4–5*
- [ ] Prompt contract: the model emits tags like `[POINT 812,440 "Share button"]` and `[BOX x1,y1,x2,y2]` **before** any prose. Parse them mid-stream so the pointer moves while the answer is still generating.
- [ ] Map model coordinates to screen points using the scale factor and display origin (handle multiple monitors).
- [ ] Full-screen transparent `NSPanel` overlay that is click-through, sits on all Spaces, and stays above other windows.
- [ ] Animated buddy cursor flies to the point, shows the label, and pulses a highlight.
- [ ] **AX-first grounding (from pindo):** collect ≤ 40 actionable AX controls with request-local IDs, and have the model pick an ID in a **text-only** call. Use the VLM image only when AX is thin (canvas, web). Either way, snap the highlight to the real element's frame.
- [ ] AX reads run on a dedicated queue with `AXUIElementSetMessagingTimeout` (~250 ms). Every response carries a session + step ID, and late ones are dropped.
- [ ] Cue types: arrow for buttons, underline for inputs, outline for regions, with the rest of the target window lightly dimmed.
- [ ] Tool-tip style callouts and numbered step badges.
- **Exit:** on 10 "where do I click to …?" questions, **≥ 8 land on the right element**, with the pointer moving in **≤ 1.5 s**.

### M3.5 — Radial toolset · *week 6*
- [ ] Long-press detection in the event tap (~350 ms hold, < 6 px movement).
- [ ] Blank-area check via `AXUIElementCopyElementAtPosition` (show the ring only on non-interactive elements).
- [ ] Ring UI: blooms from the cursor with a spring animation, flick-to-select, release to run, `Esc` to cancel.
- [ ] Fixed PinDo slots (Ask, Voice, Explain this, Point me to…) plus app slots read from the app's menu bar via AX and triggered with `AXPress`. Cache per app when the user switches to it.
- [ ] Swallow drags while the ring is open and deliver `mouseUp` at the original point (no stuck drags or marquees). Add a per-app exclusion list.
- **Exit:** the ring opens in **< 100 ms** in 8 everyday apps, normal clicks and drags behave exactly as before (try 50 of them), and app tools fire correctly.

### M4 — Multi-step guide mode · *weeks 7–8*
- [ ] "Walk me through X" → model returns a short plan (3–8 steps), shown as a checklist.
- [ ] Show step N with an annotation and say it out loud.
- [ ] The plan is **semantic** (instruction, target, action, expectedResult), with no saved coordinates, and comes from one JSON-schema call up front. Missing values trigger a clarification question.
- [ ] Detect progress **event-driven** (`AXObserver` + mouse-up monitor, never polling). Verify cheapest-first: did the click land in the target, then AX state, then a small crop with a yes/no model check. Only positive evidence advances.
- [ ] Recover: if the screen doesn't match, re-plan from the current state.
- [ ] Local session memory, so the conversation keeps context for the duration of the task.
- **Exit:** completes 5 real tasks end to end without the user touching the keyboard to ask again, for example:
  1. Create a Gmail filter
  2. Add a dropdown in Google Sheets
  3. Turn on Night Shift in System Settings
  4. Make a new branch in VS Code
  5. Export a Canva design as PDF

### M5 — Cloud assist (opt-in) · *week 9*
- [ ] Settings: paste a Claude API key (stored in Keychain), plus an optional cloud voice key.
- [ ] Router rules, kept simple: escalate when (a) the user says "think hard" / "use cloud", (b) the local model's answer has no valid POINT after a retry, or (c) context exceeds the local limit.
- [ ] A visible ☁️ indicator whenever data leaves the machine, and a one-click "never use cloud" switch.
- **Exit:** a hard question (for example "why is this SQL query slow?") visibly escalates, answers better, and never escalates while cloud is off.

### M6 — "Siri on steroids" safe actions · *week 10*
- [ ] Tool calls for an allowlist only: open app, open URL, web search, run a named Apple Shortcut, create a reminder or calendar event, copy text to the clipboard.
- [ ] Any action is **announced and confirmed** ("Opening Canva. OK?") until the user trusts it.
- **Exit:** 10 voice commands, 10 correct actions, zero unintended side effects.

### M7 — Polish, onboarding, ship · *week 11*
- [ ] First-run onboarding that walks through the Screen Recording, Microphone and Accessibility permissions (PinDo should point at the toggles itself 😉).
- [ ] Settings for hotkey, voice, skill level ("new to computers" ↔ "power user") and model.
- [ ] Download the model on first run with progress, and check free disk/RAM before loading.
- [ ] Sign and notarize the DMG. Crash reporting stays opt-in.
- [ ] Dogfood for 1 week, then give it to 5 non-technical testers and record where they get stuck.
- **Exit:** a stranger installs it and gets a correct pointed answer in **under 5 minutes**.

---

## 7. Phase 2: hyper-local computer use (after the MVP)

**Goal:** "Summarize this sheet by region and turn it into 3 slides" gets done **on the Mac, by PinDo**, in
Excel, Word, PowerPoint, Canva and other everyday apps. The MVP teaches (*you* click). Phase 2 adds driving.

### Action ladder: APIs first, pixels last

The planner emits **semantic** actions ("bold A1:C1", "add a slide titled Q3", "click *Insert ▸ Table*").
The executor runs each one on the **highest rung that can do it**. Only rung 5 uses screen coordinates, and it
grounds them fresh every time.

| Rung | How | Speed | Reliability | Best for |
|---|---|---|---|---|
| 1. **App scripting** | AppleScript/JXA via `NSAppleScript`. Excel, Word, PowerPoint, Keynote, Numbers, Pages, Finder and Mail all have dictionaries | ~50–300 ms | Highest | Cell values, formulas, ranges, charts, text, styles, slides, shapes |
| 2. **Accessibility actions** | `AXPress`, set `AXValue`, and run any menu-bar command via AX | ~100 ms | High | Buttons, menus and fields in any native app |
| 3. **Keyboard shortcuts** | `CGEvent` key events | ~instant | Medium | Office, Canva and Google Docs shortcuts |
| 4. **File-level edits** (optional) | Write `.xlsx`/`.docx`/`.pptx` directly when the app isn't open | fast | High | Bulk generation. Add only when scripting hits a gap |
| 5. **Vision + synthetic mouse** | Ground with the VLM, then a `CGEvent` click or drag | 1–2 s | Lowest | Canvas UIs: Canva, Figma, web apps |

**Why this is the big speed and accuracy win:** for Office, the model reads the document **as text** (cell
values, slide text) through scripting instead of reading pixels. That means fewer tokens, no misreads and no
pointing errors. Most steps never need a screenshot.

### App coverage (what to expect)

| App | Main rungs | Expectation |
|---|---|---|
| Excel / Numbers | 1 → 2 | Strong: read and write ranges, formulas, formatting, charts, sheets |
| Word / Pages | 1 → 2 | Strong: text, styles, headings, tables, find/replace |
| PowerPoint / Keynote | 1 → 2 | Strong: slides, layouts, text, images, tables |
| Google Docs / Sheets (browser) | 2 → 3 → 5 | Medium: web content via AX plus rich shortcuts |
| **Canva** (desktop or browser) | 2 → 3 → 5 | **Hardest.** No local API, and the canvas is pixels. Expect slower, vision-driven steps and a fallback to Teach mode |
| Finder / files | Native `FileManager` with pindo-style review | Strong |

### Agent loop

`observe → plan → act → verify → next / replan`
- **Observe:** AX snapshot plus app state from scripting. A screenshot only when those are thin.
- **Plan:** semantic steps with JSON-schema-constrained output (`clarification | plan | tool | complete`). Ask a
  clarification when a required value is missing. Never invent it.
- **Act:** one step, on the highest workable rung.
- **Verify, cheapest evidence first:** script readback → AX state → screenshot + model. A step only counts as done
  with **positive evidence**. Clicking alone doesn't count.
- **Caps:** 30 steps, 3 retries per step. After that, stop and hand back in Teach mode.

### Control modes and safety rails

| Mode | Behavior |
|---|---|
| **Teach** (MVP) | Points and explains. You click |
| **Copilot** | Shows the next action as a ghost preview. Press ↩ to run it, Esc to skip |
| **Autopilot** | Runs the whole plan inside the apps you allowed, pausing at any "always ask" action |

- **Always ask:** send/share/publish, delete, purchase/payment, overwriting or saving over a file, password/sign-in
  fields, and any app not on the allowlist.
- **Kill switch:** Esc, or slamming the mouse into a screen corner, stops instantly. Any physical input from you
  pauses Autopilot.
- **Undo:** save a versioned copy of a document before Autopilot first touches it.
- **Prompt injection:** text on screen, in documents and from tools is **data, never instructions**.
- **Action log:** every executed action is listed with its rung and evidence, and can be replayed for debugging.
- macOS gotchas: app scripting needs the `com.apple.security.automation.apple-events` entitlement and a one-time
  Automation prompt per app. It doesn't work in the Mac App Store sandbox, so **ship as a notarized DMG**.

### Models for computer use (24 GB)

- Keep **one VLM resident** (the M0 winner, for example Qwen3-VL-8B) for planning, AX-ID selection (text-only, so fast)
  and verification.
- For rung 5, run a bake-off of local **GUI-specialist** models: UI-TARS-1.5-7B, OpenCUA-7B, Holo1.5-7B, and **MAI-UI-2B**
  (pindo's pick). Load only one at a time.
- Optional cloud: Claude computer use for tasks the local stack fails. It goes through the **same** executor,
  approvals and log.

### Speed targets for computer use

| Action type | Target (decide + act) |
|---|---|
| Scripting (rung 1) | ≤ 0.5 s |
| AX / shortcut (rungs 2–3) | ≤ 1 s |
| Vision click (rung 5) | ≤ 3 s |
| Typical 10-step Office task | **≤ 30 s** end to end |

### Phase 2 milestones

#### M8 — Executor + safety rails · *weeks 12–13*
- [ ] Executor for rungs 2, 3 and 5 (AX press/value, menu commands, key events, grounded click).
- [ ] Teach / Copilot / Autopilot modes, the always-ask list, the kill switch, versioned copies and the action log.
- **Exit:** 20 single actions across 5 apps all correct. The kill switch stops within 100 ms. Zero actions outside the allowlist.

#### M9 — Office through scripting · *weeks 14–15*
- [ ] About 15 typed tools for Excel, Word and PowerPoint: read/write range, format range, add sheet, insert chart, insert/format
      text, apply heading styles, add slide, set slide text, insert image, insert table. Each one reads back to verify.
- **Exit:** a 10-task suite (for example "clean this table and add totals", "chart column C", "turn this Word outline into 5 slides",
  "make all headings H2") with **≥ 8/10 done without help**, each in **≤ 60 s**.

#### M10 — Canva & web apps · *weeks 16–17*
- [ ] Canva and Google Docs/Sheets via AX, shortcuts and vision. GUI-model bake-off for rung 5.
- **Exit:** 5 Canva tasks (edit text, change color, resize design, add element, export PDF) with ≥ 3/5 succeeding. Failures stop
  cleanly and hand back to Teach mode.

#### M11 — Multi-app workflows · *week 18*
- [ ] Cross-app tasks: an Excel range → PowerPoint chart slide, a Word doc → PDF → **Mail draft** (a draft, never sent).
- **Exit:** 3 cross-app workflows run end to end, stopping only for the always-ask confirmations.

---

## 8. What we take from pindo (Windows reference)

[cadornajansen/pindo](https://github.com/cadornajansen/pindo) is a Windows C#/WPF tutor that's cloud-backed today
(OpenRouter, AssemblyAI, ElevenLabs). Its code doesn't port, but these **patterns** do, and several make PinDo faster:

| pindo pattern | PinDo (macOS) version | Why it helps |
|---|---|---|
| **Accessibility candidates + model picks an ID.** About 40 actionable controls, request-local IDs, resolved back to the real element | AX tree → compact candidate list → the model returns `element-12`, never raw coordinates, whenever AX has the target | **Text-only call, so much faster** than an image. Exact element frames, so pointing is exact |
| **Normalized 0–1000 grounding + strict validation** (point inside box, confidence ≥ 0.65, null when nothing is found) | Same contract for the vision fallback. No highlight on low confidence | Avoids confidently wrong arrows |
| **JSON-schema output** (`clarification / plan / tool / complete`) | Ollama `format` schema or MLX constrained decoding | No parse failures and fewer wasted tokens |
| **Semantic steps, no saved coordinates.** Each step has instruction, target, action, expectedResult | Plans are semantic, and each step is grounded fresh | Survives window moves and UI changes |
| **Positive-evidence verification.** A click alone never advances | Verification ladder: script readback → AX state → screenshot + model | Cheap checks first, so it's fast *and* correct |
| **Event-driven invalidation, never polling** (`TargetContextWatcher`) | `AXObserver` (focus, value, window created/moved) + `NSWorkspace` app-switch notifications | No screenshot loops, so less CPU and battery and faster reactions |
| **Session generation numbers** drop late results after cancel | Same: every request carries a session + step ID | No stale arrows after Esc |
| **Accessibility work off the UI thread with a bounded worker.** Never block on a hung provider | A dedicated AX queue plus `AXUIElementSetMessagingTimeout` (~250 ms) | A frozen app can't freeze PinDo |
| **One-shot window capture that excludes its own overlay** | `SCContentFilter` for the target window. Overlay uses `sharingType = .none` | Smaller images and no self-pointing |
| **Host-owned tool review.** The model proposes, the host builds the preview, approvals are single-use and expire after 5 min, never overwrite | Same approval layer for every Phase 2 action and tool | Safe computer use without slowing the safe actions |
| **Contextual cues:** arrow for buttons, underline for inputs, outline for regions, dim the rest of the window, static under reduced motion | Same cue vocabulary in the overlay | Clearer than a single highlight style |
| **App-specific planner hints** (for example "PowerPoint tables: use the Insert Table dialog, not the hover grid") | A small per-app hint file fed into the planner prompt | Big accuracy gain for little effort |
| Local model notes: **MAI-UI-2B** (grounding), **Qwen3-1.7B** (text planning) | Added to the M0 / M10 bake-offs | Cheap, small candidates worth measuring |

**Not taken:** the Windows code, the three cloud services as defaults, and the heavy doc process.

---

## 9. Risks & mitigations

| Risk | Mitigation |
|---|---|
| Local VLM points inaccurately | AX snap, a second "zoomed crop" pass around the predicted point, and cloud fallback |
| Latency feels slow | See §3: fewer image tokens, warm model, prefix cache, `POINT`-first output, streaming STT/TTS, fast path. Run the regression script on every change |
| RAM pressure on 16 GB Macs | Ship a smaller default model (3–4B) for under 24 GB, and unload the model after it's been idle |
| Privacy concerns | Local by default, a cloud indicator, no telemetry by default, and screenshots never written to disk |
| Long-press hijacks normal drags | Trigger only on blank areas (AX check), cancel on > 6 px movement, release the mouse at the original point, per-app exclusion list |
| No `Fn` key on external keyboards | Configurable shortcut, `⌥Space` fallback |
| macOS permission friction | Guided onboarding, plus detecting and re-prompting for revoked permissions |
| Autopilot does something harmful | Teach/Copilot by default, always-ask list, app allowlist, kill switch, versioned copies, action log (§7) |
| Office scripting gaps | Fall back down the ladder (AX → shortcuts → vision), and add file-level edits only where a gap repeats |
| Canva is canvas-only | Vision rung + shortcuts, honest expectations, clean hand-back to Teach mode |
| Model churn | The model is a setting. Re-run the M0 bake-off script whenever a new model drops |

---

## 10. Success metrics for the MVP

- Voice → first spoken word ≤ **2 s** locally, pointer ≤ **1.5 s**, fast-path commands < **0.5 s**
- Pointing accuracy ≥ **80%** on the test set (with AX snap)
- **5/5** guided tasks completed
- **$0** marginal cost per query in local mode
- 5 non-technical testers each finish one real task they couldn't do before
- **Phase 2:** ≥ 8/10 Office tasks done hands-free in ≤ 60 s each, zero unapproved always-ask actions

## 11. Open questions

1. Pricing: free + open source, or a one-time license (like ~$29)? The BYOK cloud keeps either option viable.
2. Should the MVP bundle the model runtime (mlx-swift) or require LM Studio/Ollama? Recommendation: require it for development builds and bundle it for the first public release.
3. Which tasks matter most to your first users (students? parents? small-business owners)? That decides the M4 test tasks.
