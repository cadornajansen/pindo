# Chat composer checkpoint — October 9, 2026

Pindo now opens an editable, bottom-centered chat composer from Ctrl+Space, matching the supplied gray rounded reference. It fades in while moving upward 18 device-independent pixels over 220 ms, unless Windows disables client-area animation. No new UI framework or package was introduced.

## Use

Exit any running Pindo instance through its tray menu, then run `powershell -ExecutionPolicy Bypass -File .\scripts\Start-Pindo.ps1` from this repository. The launcher clears stale preview/walkthrough overrides and refreshes locally saved provider keys without printing them. It defaults to the existing OpenRouter vision provider. `-Preview` explicitly selects the no-inference UI preview.

Focus PowerPoint or another external application before pressing Ctrl+Space. Type a question and press Enter, or use the send button. Shift+Enter adds a line break. Plus clears the input. Escape and Ctrl+Space dismiss the composer and cancel guidance/voice. Close/Alt+F4 hides the composer; Exit Pindo in the tray exits the app.

The microphone starts off and uses the native MicOff glyph. Clicking it starts the existing ElevenLabs voice session, provided the key is configured. Clicking during recording mutes/stops capture; clicking again unmutes. The icon reflects actual recording activity, including temporary off states during processing/playback. No recording is automatically started by opening the composer. Missing voice credentials produce a message while typing remains usable.

## Execution flow

1. The hotkey remembers the external foreground window before focusing the editable chat.
2. Enter sends a trimmed, nonempty question through the existing guidance pipeline. The composer hides and restores the external window before capture; stale, closed or minimized targets are rejected.
3. UI Automation supplies candidates to AssemblyAI. A tutor HTTP/transport failure now proceeds to the existing OpenRouter/Bedrock vision route instead of aborting the entire request. Cancellation still stops processing; text-entry walkthroughs still require UIA verification.
4. The provider response is validated, coordinates are converted, and the click-through overlay highlights the target. The chat shows the instruction without taking focus away from the external application. When the vision provider supplies only a label, the visible fallback names that label.
5. Microphone activity events update the crossed-out/on icon, status, and accessible action name. Voice continues to use the imported ElevenLabs implementation.

## Files changed

- Created `Pointly.App/Presentation/ChatWindow.xaml`: reference layout, editable question, clear/send/microphone controls, accessible names.
- Created `Pointly.App/Presentation/ChatWindow.xaml.cs`: monitor/work-area positioning, input actions, entrance animation, microphone glyph state and focus.
- Modified `Pointly.App/Presentation/GuidancePresenter.cs`: owns the composer, coordinates capture hiding/restoration, and forwards input/voice actions.
- Modified `Pointly.App/Presentation/BuddySurface.cs`: can render only click-through annotations without the old buddy/bubble.
- Modified `Pointly.App/MainWindow.xaml.cs`: hotkey/chat routing, foreground restoration, typed requests, microphone control, and tutor transport fallback.
- Modified `Pointly.App/Interop/NativeMethods.cs`: adds the native foreground-restoration call.
- Created `scripts/Start-Pindo.ps1`: live-by-default launcher with explicit preview option.
- Modified `README.md`: current usage, cloud status and composer behavior.
- Created `design-qa.md`: reference/render comparison and verification limits.
- Created `docs/CHAT_UI.md`: this execution walkthrough and verification record.

No source file was deleted. The original Pointly checkout was not changed. Keys were configured in Windows user environment settings at the user's request; their values are not source files and must never be committed.

## Verification and limits

- Destination Debug build succeeded with no warnings/errors; all 95 existing tests passed after the runtime changes.
- 13 native preview assertions passed: tray startup, input presence/focus, placeholder, compact width, send appearance, Enter submission without AI, input clearing, preview microphone behavior, plus clearing, Escape, reopening focus and hotkey dismissal.
- A live typed question in PowerPoint restored its foreground window and returned visible guidance through the cloud vision path.
- A separate real PowerPoint screenshot test returned validated OpenRouter geometry whose center landed inside the expected UIA target; provider time was 5,786 ms for that one request. This is one observation, not an accuracy benchmark or latency guarantee.
- Live microphone entered recording/Listening and returned to the crossed-out off state after its button was clicked again. ElevenLabs speech WebSocket authentication returned `session_started`. Voice transcription, long spoken conversations and TTS playback were not separately benchmarked.
- AssemblyAI model listing succeeded, but one live completion attempt had a transport failure; the new vision fallback handled the app's live request. AssemblyAI completion reliability remains unverified.
- Mixed-DPI/multiple monitors, long sessions and accessibility screen-reader navigation remain unverified. The preview comparison was at Windows 125% scaling.
- Local MAI-UI inference is still not connected. Cloud screenshot processing sends captured application content to the configured vision provider. There is no offline claim.
