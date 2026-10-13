# Utility baseline cleanup — October 13, 2026

## What changed

Pindo is now a Windows utility chat with OpenRouter planning and local reviewed execution. The active solution and namespaces use Pindo. Tutoring, voice, visual observation, local models and old scaffolding are removed. The maintained source, scripts and documentation use Pindo only. The local checkout folder rename is pending because Windows denied access while the directory was in use.

Checkpoint commits: `fbf2032` (names/layout), `986d604` (retired features), and `0502fda` (utility/shortcut integration). The prior uncommitted experiment is recoverable locally at `recovery/pre-pindo-utility-cleanup`, commit `153900a`; it is not part of the main push.

## Size comparison at commit `0502fda`

Measured against the local recovery snapshot; runtime C#/XAML under the app/src directories, excluding tests, docs and generated outputs. UTF-8 bytes normalize line endings. These are source-size measures, not startup/memory benchmarks.

| Measure | Before | After |
| --- | ---: | ---: |
| Runtime source files | 111 | 49 |
| Runtime source lines | 12,237 | 4,861 |
| Runtime source bytes | 590,981 | 271,759 |
| Runtime NuGet package references | 2 | 0 |

About 60% fewer runtime source lines. Native image/PDF/video tools remain separately installed dependencies; .NET/WPF is still required.

## How it works

1. Ctrl+Windows or the tray opens the compact composer on the cursor monitor; no target application is required.
2. A typed request and selected-path metadata go to OpenRouter. The response must be clarification, tool proposal or unsupported.
3. The host validates tool arguments and selected paths and prepares a single-use preview.
4. Only Run starts the local utility. Cancel, changed selection and request identities prevent stale proposals from executing.
5. Results include output paths and partial failures. Cancellation does not roll back completed writes.

The modifier hook runs on a dedicated message-loop thread. It tracks physical key states and ignores injected events. A standalone chord uses an unassigned menu-mask key before modifier release to prevent Start-menu activation while forwarding every physical modifier release. Other keys cancel recognition. Hook documentation: [Microsoft LowLevelKeyboardProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc).

## Validation

- Debug build: zero warnings/errors.
- Normal desktop suite: 43 passed, 0 failed, 2 opt-in checks skipped (cloud and native input).
- Retained tools suite: 239 passed, 0 failed, 6 skipped (four Unix process fixtures, Windows symlink privileges, opt-in live video). The retired scaffold contract test accounts for the previous 240-to-239 difference.
- Live OpenRouter ZIP smoke: 1 passed. A real cloud proposal produced a valid preview; disposing the preview created nothing; approving a fresh preview created the archive; existing output was rejected and input stayed unchanged.
- Native input smoke, separately: 1 passed across 16 modifier-side/order combinations. Synthetic input plus the actual Win32 menu-mask implementation preserved the existing foreground window and left all modifiers released.
- WPF smoke tests: composer opens without a target app, busy inputs are disabled, cancellation rejects a late reply, removed controls are absent, actual review Cancel and Run buttons produce distinct outcomes.
- Render inspected at `%TEMP%/pindo-utility-qa/composer.png`.
- Hook recognition covers left/right modifiers, both press/release orders, repeats, extra keys, injected input and reset. Native hook installation/reset/disposal passed.

An initial native check incorrectly required Windows to grant a new test window foreground focus; Windows denied that in subsequent runs. The final test preserves and checks the already-active window instead, without stealing focus. It remains opt-in because it sends desktop-wide synthetic input. This does not replace a physical-keyboard/device check; Windows shortcut combinations are covered by the state machine rather than creating/removing real virtual desktops.

No broad model-reliability, physical multi-monitor, installer, memory or startup-performance claim is made. Normal tests use fake model transport; only the stated ZIP smoke used the live cloud service.

## Historical file inventory

The full pre-cleanup inventory is preserved in this document at commit `0502fda`. Use `git show 0502fda:docs/CLEANUP.md` to inspect it. The maintained documentation uses Pindo throughout; Git history retains original paths and source attribution.
