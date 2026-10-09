# LocalTutor product requirements

Status: product specification for the next tutoring milestone, dated 2026-10-09. The application is still a bootstrap; future requirements below are not completed features.

## Goal and audience

Help a Filipino educator complete a small software task by showing one understandable instruction and highlighting the next accessible control, with the educator performing every action.

The primary user is a teacher preparing lesson slides who can describe a goal but does not know where to find a control. English, Filipino, and Taglish are intended input and instruction languages; language quality still needs educator review. LocalTutor is the working app name; `pindo` is the repository name.

The first complete demonstration is **inserting an existing local picture into a PowerPoint lesson slide**. The starting slide, PowerPoint version, ribbon state, and sample picture must be recorded. See [product scope](PRODUCT_SCOPE.md) for priorities and exclusions. AppBuildersPH is the team's stated target; this document makes no claim about official event requirements.

## What exists today

| Area | Verified bootstrap behavior | Missing product behavior |
|---|---|---|
| Desktop | WPF palette, trimmed input, Submit/Enter, visible mock response | Real teaching instructions and a highlight |
| Window context | Native hotkey captures the original foreground window; collision reporting and cleanup | UI Automation capture and source-window freshness validation |
| Tutoring contracts | Request, response, element ID, and screen rectangle types | Structured model inference and response validation |
| Local runtime | Central Ollama settings and asynchronous availability boundary | Model installation checks, inference, accuracy, and performance evidence |
| Utilities | Typed input/result contracts and validation/cancellation base class | Concrete utility, allowlist, confirmation, and desktop integration |

Existing evidence: three automated tests and 22 Windows 11 interactive smoke assertions are recorded in [implementation notes](IMPLEMENTATION.md). They establish bootstrap behavior only. This documentation update does not rerun those checks or establish product acceptance.

## Functional requirements

“Future” means unimplemented. “Partial” identifies existing plumbing, not an accepted tutoring feature. IDs map to the [validation plan](VALIDATION_PLAN.md).

| ID | Requirement | Status | Acceptance condition |
|---|---|---|---|
| FR01 | Summon the palette and retain the source application | Hotkey implemented; PowerPoint acceptance pending | From PowerPoint, `Ctrl + Space` opens the palette and focuses input; the remembered window belongs to PowerPoint, not LocalTutor. Reopening from the palette retains the last external source. |
| FR02 | Accept a written goal and submit once | Bootstrap implemented | Whitespace-only input shows a useful prompt without calling the tutor. Enter and Submit use the same path; duplicate submission while pending is ignored. The current response explicitly says it is a mock. |
| FR03 | Capture a bounded, relevant UI Automation snapshot | Future | Capture only the remembered source application's relevant controls; a fresh summon may select a validated PowerPoint-owned dialog. Each included element has a unique snapshot-local ID, accessible name/type, and actual screen bounds. LocalTutor's own controls are excluded. An empty or inaccessible snapshot follows FR07. |
| FR04 | Produce one locally grounded next step | Future | The local model returns a parseable `TutorResponse`: one short instruction, success/error, and at most one target ID. A successful actionable step identifies an element in the supplied snapshot. Model text never becomes shell commands or automatic mouse/keyboard actions. |
| FR05 | Validate the target before showing a highlight | Future | Reject unknown IDs, disabled controls, unusable bounds, and stale window/element context. Resolve actual bounds from the validated element, not coordinates invented by the model. Show at most one highlight and the matching instruction; make it possible for the teacher to click the application control. |
| FR06 | Let the teacher advance the task manually | Future | The teacher clicks each suggested control. The next request takes a fresh snapshot after the UI changes; a previous target is never reused as current evidence. Completion is teacher-confirmed unless an explicit, tested application signal supports it. |
| FR07 | Fail without guessing | Partial: general submission errors only | If source context, capture, inference, or response validation fails, remove any old highlight, show the reason and one recovery action, and leave the app usable. Never substitute an invented target, silently switch to cloud inference, or claim completion. |
| FR08 | Keep pending requests recoverable | Partial: async service and lifetime cancellation | Show a pending state, retain responsive window controls, and prevent duplicate submission. Provide cancellation; cancellation, close, or a superseding request prevents late instruction/highlight delivery. A request without a usable result stops after the proposed 30-second timeout and offers retry. |
| FR09 | Teach in English, Filipino, or Taglish | Future quality validation | For each recorded picture-insertion scenario, language variants preserve the same intended next action. Instructions use the chosen language and recognizable installed UI labels. An educator reviews clarity; do not infer language support from accepting Unicode text. |
| FR10 | Keep an optional local utility independent | Contracts implemented; concrete tool deferred | Only an explicitly agreed utility may be added through typed validated inputs, an approved tool ID, cancellation, and confirmation appropriate to its effect. Invalid input prevents execution; outputs do not overwrite originals without explicit choice. Tutoring remains usable if the utility is absent. |

FR01–FR09 describe the primary tutoring path. FR10 is a conditional utility work item, not a dependency or an additional required demo. The generic utility contract alone does not authorize an operation.

## Primary user flow

1. The teacher opens a recorded PowerPoint lesson slide with a local sample picture available.
2. The teacher presses `Ctrl + Space`; LocalTutor retains the PowerPoint window before taking focus.
3. The teacher asks, “Paano ako maglagay ng picture sa slide ko?” and submits.
4. The future collector reads a fresh, filtered snapshot from the retained PowerPoint window or its validated task dialog.
5. The local model receives the instruction, application name, and captured elements, then proposes one target ID and a brief instruction.
6. The application checks the response against that snapshot and current UI state. A valid target produces an instruction and a highlight; failure follows FR07.
7. The teacher performs the action, then requests the next step. Each request captures fresh evidence, including a relevant dialog if the scenario reaches one.
8. The teacher selects the prepared picture and confirms that it appears on the slide. LocalTutor does not click, type, select files, or save the presentation for them.

The existing path stops at a labeled mock response after step 3. The [data model](DATA_MODEL.md) distinguishes current request types from proposed orchestration data; no database is required.

## Failure and recovery behavior

| Condition | Required behavior | Teacher's recovery action |
|---|---|---|
| Original window closed or unsupported | No highlight; explain that usable controls could not be read | Return to the supported PowerPoint screen and summon again |
| No relevant or accessible control | No guessed button or screenshot fallback | Open the recorded starting screen or a supported dialog and retry |
| Malformed response, missing/unknown target for an actionable step, invalid bounds | Reject the response; do not display it as an actionable step | Retry after confirming the correct application is open |
| UI changed while inference ran | Discard stale output and remove the prior highlight | Request a fresh step from the current screen |
| Ollama unavailable or model missing | Explain that local tutoring is unavailable; keep the palette reachable | Start the installed runtime or complete the explicit model setup, then retry |
| Slow request | Show pending status, allow cancellation, stop at timeout | Cancel or retry; no automatic resubmission loop |
| Cancelled/closed request returns late | Ignore the result; no delayed highlight or success message | Submit a new request if still needed |
| Hotkey owned by another app | Keep the palette reachable and show collision status | Use the open window or taskbar; hide remains disabled |

The proposed 30-second inference timeout is a product default to confirm after benchmarking. It does not relax the latency targets below. Hotkey collision handling exists; the inference-specific failure cases do not.

## Nonfunctional requirements and evidence

| ID | Requirement | Acceptance target and evidence |
|---|---|---|
| NFR01 | Useful response speed | On the demo laptop, end-to-end p50 ≤ 5 seconds and p95 ≤ 10 seconds, from submission until both valid instruction and highlight appear. Record hardware, model/version, sample count, cold starts, timeouts, and failed requests; do not hide failures to improve timing. Unverified. |
| NFR02 | Grounded target accuracy | At least 90% correct next-control selections on a representative labeled test set. Count invalid output, unknown/stale IDs, and failed requests as failures on targetable cases. Include deliberate no-target cases separately to measure safe abstention. Unverified. |
| NFR03 | Local operation and data minimization | After explicit runtime/model setup, demonstrate tutoring with networking disabled. Process the teacher's instruction and filtered accessible controls locally; do not persist prompts, UI text, screenshots, or session history by default. Verify outbound traffic and capture scope before claiming privacy or offline tutoring. Unverified. |
| NFR04 | Responsive, controllable desktop behavior | Window input, hide/reopen, collision status, and clean exit remain usable during pending work. Late responses never act on a cancelled request. Existing mock smoke checks cover only the bootstrap subset. |
| NFR05 | Declared Windows/display compatibility | Record tested Windows and PowerPoint versions, scaling, and monitor layout. Highlight matches actual controls at 100% and 125% scaling and a mixed-DPI case before claiming that support. Bootstrap evidence covers Windows 11 at 125%; Windows 10 and mixed DPI are untested. |

`qwen3:1.7b` is the initial local model candidate, not an accuracy or speed guarantee. Historical cloud experiments are not acceptance evidence for this local pipeline. Application support claims apply only to recorded tested tasks and versions.

## Product boundaries and open decisions

- Must: one PowerPoint picture-insertion teaching path, source-window grounding, manual action, safe failure, pending/cancel behavior, language review, and measured results.
- Should: refine instruction brevity and readability after the core behavior passes.
- Later: other PowerPoint tasks and other applications after new scenario evidence; no support promise for Excel, Word, File Explorer, Settings, Notepad, Photoshop, or Canva today.
- Out of scope: autonomous computer use, arbitrary commands, model training/fine-tuning, complex RAG, screenshot/vision fallback, voice, cloud inference fallback, accounts, a database, and stored lesson/session history.

Assumptions to confirm: accessible PowerPoint controls on the demo machine, a local picture prepared for the demo, enough local hardware for acceptable inference, and educator feedback on wording. Open decisions are the final model after benchmarking, precise snapshot filtering/size limits, and whether an independent utility is worth adding. None expands the current implementation scope automatically.

See [setup and current status](../README.md), [product scope](PRODUCT_SCOPE.md), [data model](DATA_MODEL.md), [validation plan](VALIDATION_PLAN.md), and [implementation contracts](IMPLEMENTATION.md). The next code change should be agreed as one small checkpoint; this specification does not authorize building the whole product at once.
