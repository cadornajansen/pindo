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

## 4. MVP scope

**In:** push-to-talk voice Q&A about the screen, pointing/annotation overlay, multi-step guide
mode, local-by-default with opt-in cloud escalation, ~6 safe voice actions, onboarding for
permissions, and a signed DMG.

**Out (post-MVP):** autonomous clicking/typing, long-term memory, Windows/iOS, accounts/billing,
a plugin system, and always-on wake word. Each one adds a lot of risk without changing the
core "it shows me where to click" magic.

> **Principle: guide, don't drive.** The MVP *shows* you where to click; it doesn't click for you.
> That makes it safer, easier to build, and better for teaching. Driving comes after the MVP.

---

## 5. Step-by-step milestones

Each milestone has a hard **exit test**. Don't start the next one until the current one passes.
The estimates assume one developer working part time.

### M0 — Foundation & model bake-off · *week 1*
- [ ] Xcode project: menu-bar app, app icon, `Info.plist` usage strings (mic, screen recording).
- [ ] Install LM Studio/Ollama and pull 4 candidate VLMs: Qwen3-VL-**4B** and **8B**, Qwen2.5-VL-7B, Gemma 3 12B (all 4-bit, *Instruct* variants).
- [ ] Build a test set: **20 screenshots**, each with a question and the correct click target
      (Gmail, Google Sheets, System Settings, Finder, VS Code, Canva, …).
- [ ] Script it: measure **time-to-first-token**, **total latency** and **pointing accuracy**
      (whether the predicted point lands inside the target box). Test each model at 2 image sizes (full display vs. active window at ~1024 px).
- **Exit:** pick the **fastest** model and image size with first `POINT` in **≤ 1.5 s** (warm model) and pointing accuracy **≥ 75%**.
  Write the results into `PLAN.md`.

### M1 — Eyes: "what am I looking at?" · *week 2*
- [ ] Global hotkey (for example ⌥Space) captures the display under the cursor with ScreenCaptureKit.
- [ ] Capture the **active window** downscaled to ~1024 px and remember the scale factor (Retina is 2×).
- [ ] Load the model at app launch and keep it warm. Use a fixed system prompt so the prefix cache hits.
- [ ] Per-stage timing log plus a debug HUD (see §3).
- [ ] Send the screenshot and a typed question to the local model, then stream the answer into a small floating panel near the cursor.
- **Exit:** asking "what does this error mean?" on 5 different apps gives a correct answer, with first text in **≤ 1.5 s**.

### M2 — Ears & mouth: voice round-trip · *week 3*
- [ ] Hold the hotkey to talk and release it to send (push-to-talk, not a wake word).
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
- [ ] **AX snap:** read the Accessibility tree near the point and snap the highlight to the real element's frame.
- [ ] Tool-tip style callouts and numbered step badges.
- **Exit:** on 10 "where do I click to …?" questions, **≥ 8 land on the right element**, with the pointer moving in **≤ 1.5 s**.

### M4 — Multi-step guide mode · *weeks 6–7*
- [ ] "Walk me through X" → model returns a short plan (3–8 steps), shown as a checklist.
- [ ] Show step N with an annotation and say it out loud.
- [ ] Detect progress: after each user click (global mouse-up monitor), send a **small crop** and ask a yes/no "is step N done?" (well under 1 s). The full plan comes from one call up front.
- [ ] Recover: if the screen doesn't match, re-plan from the current state.
- [ ] Local session memory, so the conversation keeps context for the duration of the task.
- **Exit:** completes 5 real tasks end to end without the user touching the keyboard to ask again, for example:
  1. Create a Gmail filter
  2. Add a dropdown in Google Sheets
  3. Turn on Night Shift in System Settings
  4. Make a new branch in VS Code
  5. Export a Canva design as PDF

### M5 — Cloud assist (opt-in) · *week 8*
- [ ] Settings: paste a Claude API key (stored in Keychain), plus an optional cloud voice key.
- [ ] Router rules, kept simple: escalate when (a) the user says "think hard" / "use cloud", (b) the local model's answer has no valid POINT after a retry, or (c) context exceeds the local limit.
- [ ] A visible ☁️ indicator whenever data leaves the machine, and a one-click "never use cloud" switch.
- **Exit:** a hard question (for example "why is this SQL query slow?") visibly escalates, answers better, and never escalates while cloud is off.

### M6 — "Siri on steroids" safe actions · *week 9*
- [ ] Tool calls for an allowlist only: open app, open URL, web search, run a named Apple Shortcut, create a reminder or calendar event, copy text to the clipboard.
- [ ] Any action is **announced and confirmed** ("Opening Canva. OK?") until the user trusts it.
- **Exit:** 10 voice commands, 10 correct actions, zero unintended side effects.

### M7 — Polish, onboarding, ship · *week 10*
- [ ] First-run onboarding that walks through the Screen Recording, Microphone and Accessibility permissions (PinDo should point at the toggles itself 😉).
- [ ] Settings for hotkey, voice, skill level ("new to computers" ↔ "power user") and model.
- [ ] Download the model on first run with progress, and check free disk/RAM before loading.
- [ ] Sign and notarize the DMG. Crash reporting stays opt-in.
- [ ] Dogfood for 1 week, then give it to 5 non-technical testers and record where they get stuck.
- **Exit:** a stranger installs it and gets a correct pointed answer in **under 5 minutes**.

---

## 6. Risks & mitigations

| Risk | Mitigation |
|---|---|
| Local VLM points inaccurately | AX snap, a second "zoomed crop" pass around the predicted point, and cloud fallback |
| Latency feels slow | See §3: fewer image tokens, warm model, prefix cache, `POINT`-first output, streaming STT/TTS, fast path. Run the regression script on every change |
| RAM pressure on 16 GB Macs | Ship a smaller default model (3–4B) for under 24 GB, and unload the model after it's been idle |
| Privacy concerns | Local by default, a cloud indicator, no telemetry by default, and screenshots never written to disk |
| macOS permission friction | Guided onboarding, plus detecting and re-prompting for revoked permissions |
| Model churn | The model is a setting. Re-run the M0 bake-off script whenever a new model drops |

---

## 7. Success metrics for the MVP

- Voice → first spoken word ≤ **2 s** locally, pointer ≤ **1.5 s**, fast-path commands < **0.5 s**
- Pointing accuracy ≥ **80%** on the test set (with AX snap)
- **5/5** guided tasks completed
- **$0** marginal cost per query in local mode
- 5 non-technical testers each finish one real task they couldn't do before

## 8. Open questions

1. Pricing: free + open source, or a one-time license (like ~$29)? The BYOK cloud keeps either option viable.
2. Should the MVP bundle the model runtime (mlx-swift) or require LM Studio/Ollama? Recommendation: require it for development builds and bundle it for the first public release.
3. Which tasks matter most to your first users (students? parents? small-business owners)? That decides the M4 test tasks.
