# Chat composer visual QA

final result: passed

Scope: the supplied second image's chat component, not its Figma/editor or desktop background. The app remains native WPF.

## Evidence

Source: `C:/Users/DDCic/AppData/Local/Temp/codex-clipboard-eabfb648-a626-4e71-9aed-0af1a4d7e26e.png`, component crop at approximately x=717–1226, y=593–695.

Rendered implementation: `C:/Users/DDCic/AppData/Local/Temp/pindo-chat-render.png`, rendered from the actual ChatWindow in WPF at 125% Windows scaling. Full transparent window: 535×145 physical pixels; visible card: approximately 505×98.

Combined comparison opened and inspected: `C:/Users/DDCic/AppData/Local/Temp/pindo-chat-comparison.png`. Source and rendered component were aligned at approximately 509×102 pixels each. These local QA artifacts are not committed. A direct desktop screenshot intentionally excludes Pindo because its window uses capture exclusion, so WPF rendering was used for the visual comparison; actual hotkey/input behavior was tested separately in the native app.

## Comparison

- Typography: Segoe UI; subdued placeholder with matching wording and close size/alignment.
- Layout: compact gray card, bottom-centered above the taskbar, rounded corners, plus and microphone at lower right. Input and controls have matching spacing. No unrelated page chrome was reproduced.
- Colors: solid dark gray card, gray border, white controls and muted placeholder; no blue buddy bubble.
- Assets: Windows Segoe Fluent Icons are used. The off icon intentionally uses MicOff rather than the reference's plain microphone, as subsequently requested. No raster asset was required.
- Copy: placeholder matches. The reference's truncated secondary text is omitted; actual status messages appear there when needed. A send control appears when text is entered.
- Motion: 220 ms opacity/18-DIP upward transition is implemented; respects the Windows animation preference. Native interaction checks verified reopening/focus, but did not instrument frame timing.

No actionable P0/P1/P2 visual mismatch was found in the combined stable empty-state comparison. Small border-weight/icon-shape differences from the Figma mock are P3 details. Voice, provider reliability and multi-monitor checks are functional concerns recorded in `docs/CHAT_UI.md`, not certified by this visual pass.
