# Release report: macOS hardening pass (2026-10-10)

**Verdict: Demo Ready with Limitations.** The verdict rests on builds, automated tests and the earlier live
benchmark. The changes in this pass have **not** been re-tested live in the GUI, because the rebuild reset macOS
permissions.

## Changes in this pass

- **Config:** `Config.swift` centralizes settings with validation.
  - The default local model is MAI-UI 8B.
  - The Ollama endpoint must be on this Mac (loopback).
  - There's one shared timeout.
- **Keychain:** keys are cached per launch and read off the main thread. A Keychain prompt froze the app before.
  - The Keychain is canonical.
  - The developer key file is Debug-only and used only when no Keychain entry exists.
- **Agent:**
  - Checks Accessibility before acting.
  - Rejects invented ids with a clear reason.
  - Stops after 3 rejected proposals with the real cause.
  - Gives an honest repeat message (no "probably finished").
  - Falls back to typing keystrokes only into a focused field.
  - Replaces and commits Finder's new-item name.
- **Guide:** checks Accessibility first.
- **App:** single-instance guard, cancels in-flight work on quit, logs invalid settings at launch.
- **Docs:** README, ARCHITECTURE, DISCLOSURES; PLAN and the benchmark labelled historical.

## Not verified (blocked: permissions reset on rebuild)

- **Live GUI tests:** Finder, TextEdit, PowerPoint, Canva, cancellation.
- **Offline run** with networking off.
- **Keychain prompt behaviour** across restarts.

## Not implemented

- File tools (PDF, ZIP, conversion).
- Stable code signing: there's no certificate on the development Mac.
