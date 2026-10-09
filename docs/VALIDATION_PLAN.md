# Validation plan

LocalTutor targets AppBuildersPH and a teacher inserting a local picture into a PowerPoint lesson. This plan verifies the [PRD](PRD.md) within the [product scope](PRODUCT_SCOPE.md). **No application tests or model benchmarks were run as part of this documentation change.**

## Evidence already available

The [README](../README.md) and [implementation notes](IMPLEMENTATION.md) record the bootstrap results; these are historical checks, not new results:

| Check | Recorded evidence | Limit |
|---|---|---|
| Restore and build | Passed; zero warnings and errors | Bootstrap environment only |
| Unit tests | Three passed: contract JSON round trip, invalid tool input, cancellation before tool execution | No model, UI Automation, or highlight coverage |
| Interactive smoke check | 22 assertions passed; launch/focus, input, Enter, hide/reopen, shortcut collision, exit/cleanup | Windows 11 build 26200, 125% scaling |
| Ollama unavailable | Mock assistant remained usable | Availability checking is not inference |

Real inference, UI Automation capture, highlighting, offline tutoring, Windows 10, and mixed-DPI behavior remain **unverified**. Current `TutorRequest.Elements` is empty and the response identifies itself as a mock.

## Repeat the current scaffold check

From the repository root, run the existing verification commands:

```powershell
dotnet restore LocalTutor.slnx
dotnet build LocalTutor.slnx --no-restore
dotnet test LocalTutor.slnx --no-build --no-restore
dotnet run --project src/LocalTutor.Desktop/LocalTutor.Desktop.csproj
```

With another application focused, press `Ctrl + Space`, type a question, and press Enter. Expect a labeled mock response; Escape should hide the assistant when its shortcut is registered. Reopen, close, then relaunch to check shortcut release. Test blank input separately. A second instance should report its shortcut collision and remain reachable. These checks cover FR01–FR02 and part of NFR04; rerunning them does not establish future MVP readiness.

## Planned teacher demonstration

1. Record Windows build, PowerPoint version/build and UI language, display resolution/scaling, and monitor arrangement. Use desktop PowerPoint with an editable local presentation; avoid an online-only fixture.
2. Prepare `LocalTutor-demo/lesson.pptx` with one blank slide and `LocalTutor-demo/images/lesson-photo.png` with a non-sensitive classroom illustration. Record their actual absolute paths. These are fixtures to prepare, not files supplied by this documentation change.
3. Start on slide 1, with the Home ribbon visible and PowerPoint foreground. Open the assistant once, then hide it so PowerPoint can regain focus through a teacher click.
4. Press `Ctrl + Space`; ask, “Paano ako maglagay ng picture sa PowerPoint para sa lesson ko?” Future behavior: capture PowerPoint's accessible controls, return one useful next step, and highlight its validated control.
5. The teacher clicks the highlighted control. Repeat the question or ask for the next step after each action, invoking the assistant from the now-current PowerPoint window or its verified owned file dialog. Each turn needs a fresh snapshot.
6. Navigate to the fixture folder, select `lesson-photo.png`, and insert it manually. Finish when the picture appears on slide 1. LocalTutor must never click, type a path, or insert a file for the teacher (FR06).

Record the actual control sequence before scoring. Ribbon layouts and file dialogs vary by PowerPoint version and UI language; expected names below describe intent, not guaranteed accessibility labels. A tester must identify the correct accessible element and its bounds in each installed version. If the control cannot be captured, record that failure rather than inventing an ID.

## Planned labeled target set

Prepare each state independently and reset it before each attempt. Use all three prompts per row: **10 distinct task states × 3 languages = 30 cases**. The teacher acts only after recording the result. The normal demo follows T01, T04–T08; the remaining states exercise variants and recovery.

| Case / prepared state | Expected next control | English prompt | Filipino prompt | Taglish prompt |
|---|---|---|---|---|
| T01: Home ribbon open | Insert tab | Where do I start inserting a picture? | Saan ako magsisimulang maglagay ng larawan? | Saan ako mag-start mag-insert ng picture? |
| T02: Design ribbon open | Insert tab | Which tab lets me add a picture? | Aling tab ang para sa paglalagay ng larawan? | Anong tab para mag-add ng picture? |
| T03: Ribbon collapsed | Insert tab | Open the picture insertion options. | Ipakita ang mga pagpipilian para maglagay ng larawan. | Buksan ang options para mag-insert ng picture. |
| T04: Insert ribbon open | Pictures command | Show me the picture command. | Ituro ang utos para maglagay ng larawan. | Nasaan ang Pictures button? |
| T05: Picture source menu open | Local-device source | Use a picture saved on this computer. | Gamitin ang larawang nasa computer na ito. | Picture sa laptop ko ang gagamitin. |
| T06: Local file dialog in fixture parent folder | Images folder | Open the images folder. | Buksan ang folder na images. | Open natin ang images folder. |
| T07: File dialog in images folder, nothing selected | lesson-photo.png item | Select lesson-photo.png. | Piliin ang lesson-photo.png. | I-select ang lesson-photo.png. |
| T08: Fixture image selected | Insert/confirm button | Insert the selected picture. | Ilagay ang napiling larawan sa slide. | I-insert na ang selected picture. |
| T09: Full fixture path entered in file-name field | Insert/confirm button | Use the image at the path I entered. | Gamitin ang larawan sa inilagay kong lokasyon. | I-insert ang picture sa path na tinype ko. |
| T10: Wrong filename entered, no error dialog open | File-name field | Where can I correct the filename? | Saan ko maitatama ang pangalan ng file? | Saan ko i-edit ang wrong filename? |

Store each case ID, literal prompt, fixture state, expected accessible control/ID, snapshot identity, captured bounds, output, actual target, and pass/fail reason. IDs are valid only within their snapshot; see [data model](DATA_MODEL.md). A qualified reviewer also checks whether the instruction is understandable, useful for that state, and in the requested language (FR09).

## Planned failure and desktop checks

| Situation | Required observation | PRD |
|---|---|---|
| Assistant takes focus | Source `HWND` still identifies the teacher's prior window, not LocalTutor | FR01, FR03 |
| Teacher focuses a PowerPoint-owned file dialog and summons again | Fresh capture targets that verified dialog; unrelated applications are not treated as PowerPoint | FR03, FR06 |
| Empty/inaccessible snapshot; disabled or zero-size target | Explain that guidance is unavailable; no fabricated target or highlight | FR03, FR05, FR07 |
| Window closes, ribbon changes, or target moves during inference | Reject stale snapshot/target and ask for a fresh invocation; no stale rectangle | FR05–FR07 |
| Unknown target ID, malformed/empty output, missing instruction, failure response | Recoverable message; no highlight and no computer action | FR04–FR07 |
| Ollama unavailable or selected model missing | Explain local setup failure, keep input/window usable, allow retry; never silently download or use cloud | FR07, NFR03 |
| Slow response, declared timeout, or cancellation | UI responds; pending state ends; cancelled/late response cannot show an obsolete step | FR08, NFR04 |
| 100%, 125%, 150% scaling; different-DPI monitors; monitor left of primary | Highlight matches physical target after DPI conversion; negative coordinates remain valid | NFR05 |
| Repeated open/hide, shortcut collision, close/relaunch | Input focus, reachability, cancellation, and native shortcut cleanup remain correct | NFR04 |
| Network disconnected after manual model setup | Full teacher flow works locally; document traffic evidence and any application-level network dependencies | NFR03 |

These tests are planned, not features already implemented. For deterministic invalid-output and stale-target tests, use controlled future test inputs once those components exist; do not rely on a model happening to fail. Windows 10 needs its own run before claiming that support. The current 125% single-display result does not cover mixed DPI.

## Planned local-model benchmark

After the user finishes installing Ollama and manually obtains an approved model, Member 1 records the actual model tag, digest/hash, quantization, Ollama version, CPU/GPU/RAM/VRAM, power mode, Windows build, and display/app versions. `qwen3:1.7b` is only the initial candidate. Record prompt/template, snapshot filter/size, generation settings, context limit, response format, and timeout policy with the Git commit; do not assume defaults are comparable across runs.

Time all 30 labeled cases once for initial accuracy and representative pipeline latency. Then repeat a fixed representative picture-insertion case for **at least 30 warm attempts and 5 cold attempts**, resetting its UI state every time. Warm means the same model is already loaded; cold means verified unloaded before that attempt. Record the method used to establish those states and include model loading in cold timing. Report the diverse-case run and fixed-case repeats separately; keep cold and warm samples separate. Repeats do not expand task diversity.

Measure three intervals with the same monotonic clock: (1) submission to validated instruction **and** visible highlight, (2) inference request to completed model response, and (3) capture/validation/render overhead. Report p50 and p95 for the whole pipeline separately from inference-only timing, per cold/warm group, with counts. Use nearest-rank percentiles (`ceil(percentile × sample count)`). Thirty warm samples and five cold samples give a rough feasibility estimate, not a reliable tail-latency claim.

- **NFR01 target:** whole-pipeline p50 ≤ 5 seconds and p95 ≤ 10 seconds on the demo laptop, using **all attempts**. At least 50% must complete both the validated instruction and visible highlight within 5 seconds, and at least 95% within 10 seconds. Unsuccessful or missing-highlight attempts are non-completions (conceptually infinite duration) for threshold acceptance; a fast error is not a completion. Report completion counts, timeouts, errors, and success-only duration percentiles alongside inference-only diagnostics, separately for cold and warm runs. Never remove failed attempts from the acceptance denominator.
- **NFR02 target:** correct validated target / all attempted labeled cases ≥ 90%; for 30 cases this requires at least 27 correct. Unknown IDs, unavailable controls, stale targets, empty/invalid output, and timeouts count as failures. Report counts per language and failure category; never replace failed cases with easier ones.
- Report instruction quality separately from target accuracy. Reusing a correct ID with an unsafe, contradictory, or wrong-language instruction does not pass the complete scenario. Do not infer full application support or educator usefulness from this small dataset.

## Evidence and ownership

Member 4 prepares fixtures, ground-truth labels, prompts, and educator observations; Member 1 verifies capture, inference, validation, highlights, and timing. Member 3 records dated results, environment, sample counts, limitations, and links to retained non-sensitive evidence. Distinguish observed outcomes from acceptance targets in every report.

Member 2's utilities remain a separate, deferred checkpoint (FR10): test typed input validation, approved operation boundaries, success/error output, and cancellation before/during execution for each actual utility. The two existing base-class tests prove neither real conversions nor authorization. No utility is required to complete the PowerPoint picture demo.

Release evidence must contain a passing scaffold check, the complete labeled-case results, separate cold/warm measurements, failure/DPI/offline checks, and a teacher-run demonstration. Mark missing checks **not run** or **blocked** with the reason; a mock response or runtime-availability result cannot satisfy the local-AI acceptance gates.
