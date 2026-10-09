# Mac continuation and validation

## Get and build

The branch is `feat/application-skills`. Keep it separate from main until review completes.

```bash
git fetch origin
git switch feat/application-skills
git pull --ff-only
python3 -B tools/application_skills.py --check-bundle PinDo/Resources/ApplicationSkills.json
python3 -B -m unittest discover -s tests -v
```

If no local branch exists, use `git switch --track origin/feat/application-skills`. Preserve unrelated local changes before switching.

Requirements: macOS 15+, Xcode 26, and the existing local runtime configured according to the README. Teach requires image input and structured responses. No cloud service is added.

```bash
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer xcodebuild \
  -project PinDo.xcodeproj -scheme PinDo -derivedDataPath build build
open build/Build/Products/Debug/PinDo.app
```

Allow **Accessibility** and **Screen Recording** for PinDo in System Settings → Privacy & Security. A permission change or ad-hoc rebuild may require reopening the app and renewing the grant. CI's unsigned build establishes compilation; build locally for interactive testing.

## First focused test

```bash
python3 -B tools/practice_assets.py
```

This creates `build/practice-assets`. Use another `--output` directory for a fresh session; existing directories are not overwritten. The eight assets are original synthetic fixtures. The WAV contains tones, not speech. Record permitted spoken counting for caption tests as described in the fixture setups.

1. Open Finder at the disposable practice directory.
2. Press Fn+Space, enable **Teach**, and enter `Paano gumawa ng folder na Lesson Files?`.
3. Confirm the app/task if prompted, provide the parent directory and name, and choose **Requirements met — begin**.
4. Follow one instruction at a time. The panel should stay visible. Inspect **Why this step?** and **What to check**.
5. Confirm the new folder's name and parent. Clicking New Folder alone is insufficient evidence.
6. Repeat while switching windows and stopping during a response. No late response should advance a paused or stopped lesson.

## Available-app validation

Record exact macOS version, application build, browser, account plan, language and hardware. Use the relevant subset of `evaluations/fixtures/setups.json`.

| App | Start here | Then test |
| --- | --- | --- |
| PowerPoint | `powerpoint.insert_image` | slide masters and PDF output |
| Excel | `excel.totals_averages` | PivotTables and Mac CSV import |
| Word | `word.headings` | section numbering and citation layout |
| CapCut Mac | `capcut.import_trim` | captions, tracked blur and export gates |
| Figma web | `figma.nested_auto_layout` | interactive states and variable modes |
| Canva web | `canva.frames_crop` | video timing and accessible PDF output |
| Photoshop Mac | `photoshop.layer_mask` | Smart Filters, edge refinement and export |

Run each task's English, Taglish and recovery cases. Begin with one lesson per app. Generated guidance can follow the request language; stored objectives/explanations currently provide an English fallback. Test language quality rather than assuming intent matching proves translation quality.

Run the shared scenarios in `evaluations/runtime_cases.json`. Some require a controlled local response; there is no automatic live-app test driver. A predicate unit test does not establish that a live runtime scenario passed.

## Record evidence

Copy `evaluations/run_template.json` into a local results directory for each real run. Use null for unavailable metrics and explain why. Do not commit screenshots of personal documents.

- Record actual controls, expected-result evidence, failed steps, permissions and UI differences.
- **Response timing** reports observation-to-response latency and returned token counts. It excludes debounce and time before observation starts; measure request-to-usable-guidance separately.
- Separate cold and warm runs. Report sample count, median and P95. Performance targets remain hypotheses until measured.
- Compare baseline teaching without a task reference against teaching with the bounded reference using the same synthetic observation, instruction and runtime configuration. The existing action mode is not a comparable tutoring baseline. A controlled replay harness is still needed for this experiment; do not claim improvement from unrelated runs.
- Update `tested_versions`, case `application_version` and validation notes only after real evidence. Preserve failures and blocked-version results.

## Read the implementation

1. `skills/finder/create_folder.json` defines inputs, steps and evidence.
2. `tools/application_skills.py` validates and bundles the data.
3. `PinDo/SkillLibrary.swift` decodes, matches and supplies progression predicates.
4. `PinDo/QuickBar.swift` selects Teach or the existing action path.
5. `PinDo/TutorSession.swift` owns the step, pause/resume and stale-result rejection.
6. `PinDo/TutorObservation.swift` captures the pinned window and receives activity notifications.
7. `PinDo/TutorClient.swift` sends bounded context and parses non-executable guidance.
8. `PinDo/TutorView.swift` shows requirements, explanations and controls.

Task files describe procedures; they are not macros. The host owns progression while inference supplies guidance and evidence. Preserve this separation while continuing development.

## Known limits to investigate

- Single-window capture may exclude menus; menu availability is not proof of an open menu. Affected seed steps require confirmation.
- Modal windows pause watching; Resume can adopt a window in the same app. Return from an external viewer before further visual checks, or confirm a designated manual-review step while paused.
- Domain detection depends on Accessibility metadata. Same-title tabs without exposed URLs are not reliably distinguishable.
- Figma tasks target the web editor; native Figma needs separate surface validation.
- Creative canvases and audio have limited Accessibility representation. Visual judgments remain unverified; listening and subjective checks require the user.
- Watching reacts to input and supported Accessibility notifications, not continuous video. A background render with no notification may require fresh user activity.
- Recovery cases check the next guidance and pause behavior; full end-to-end workflows require manual runs.

See [CHANGED_FILES.md](CHANGED_FILES.md) for every created/modified file and its purpose. No project redesign or dependency migration is included.
