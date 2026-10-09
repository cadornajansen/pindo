# Application skills: first checkpoint

This repository remains the native macOS application. A Windows workstation can
edit and check the skill data; it cannot build the AppKit/SwiftUI app or validate
Finder, Preview, or PowerPoint for Mac.

This checkpoint implements the first three skills from the supplied plan. It
does not complete the twelve-skill assignment or connect skills to inference.

| Skill ID | Evaluation ID | Documentation coverage | Hands-on status |
| --- | --- | --- | --- |
| `powerpoint.insert_image` | `eval.powerpoint.insert_image` | Microsoft 365, 2024, 2021 for Mac | unverified |
| `finder.create_folder` | `eval.finder.create_folder` | Finder in macOS 26; includes rename recovery | unverified |
| `preview.merge_pdfs` | `eval.preview.merge_pdfs` | Preview in macOS 15 and 26 | unverified |

Research date: **2026-10-10**. Sources are official Microsoft Support and Apple
user guides, recorded in each skill's `sources` list with their URLs and dates.
Prerequisites, expected observations, and evaluation failure conditions are
derived from those procedures; they are not measured ground truth.

## Run the Windows checks

From the repository root in PowerShell:

```powershell
py -B tools/application_skills.py
py -B -m unittest discover -s tests -v
```

Use `python3` instead of `py` on a Mac with Python installed. There are no package
dependencies or downloads. `-B` prevents Python from creating bytecode cache
files. The validator also works outside the repository's working directory:
it finds the default data root relative to its own file. `--root` can select a
different library directory for a test.

The first command must report **3 skills and 3 evaluation cases** and exit with
code zero. A malformed file produces a filename/field error and a nonzero exit.
The second command checks both valid records and deliberately invalid copies in
temporary directories. Neither command runs a model or operates another app.

## Version 1 data contract

JSON fits the app's existing JSON model messages and can be read with Python's
standard library now and Swift's JSON facilities in a later integration. The
schema is enforced by `tools/application_skills.py`; there is no separate JSON
Schema engine or second schema definition to keep synchronized.

Every record carries integer `schema_version: 1`. Unknown fields are rejected,
including coordinate and executable-command fields. Required strings and string
lists must be nonempty unless explicitly allowed below.

Skill fields:

| Field | Meaning |
| --- | --- |
| `id` | Stable lowercase dotted task ID; unique across the library |
| `application_id`, `application`, `platform` | Internal app identifier, display name, and `macos` or `windows` |
| `title`, `intents` | Task title and English/Taglish request examples |
| `prerequisites` | Conditions to confirm before guidance begins |
| `steps` | Ordered objects with unique `id`, `objective`, `target`, and `expected_result` |
| `target` within each step | Semantic `role` and `semantic_name`, never a saved location |
| `success_criteria` | Observable evidence required to report completion |
| `recovery` | Objects with a failure `condition` and user-facing `guidance` |
| `constraints` | Guidance boundaries, including user-performed actions and fresh observations |
| `sources` | Objects with `title`, HTTPS `url`, and ISO `research_date` |
| `validation` | `status`, `documented_versions`, `tested_versions`, and `notes` |

The application IDs are internal library names, **not** macOS bundle identifiers.
A future host must explicitly map the detected app to them. Supporting `windows`
in the schema permits a future separate workflow; it does not make these Mac
records usable as Windows instructions.

Evaluation fields:

| Field | Meaning |
| --- | --- |
| `id`, `skill_id` | Unique evaluation ID and the existing skill it tests |
| `application_id`, `platform` | Must agree with the referenced skill |
| `application_version` | Actual version recorded for a run; `null` while untested |
| `starting_state`, `user_instruction` | Required interface setup and the user's request |
| `expected_next_action` | An `objective` and semantic `target` for the next user action |
| `expected_result`, `failure_criteria` | Observable outcomes and reasons to fail |
| `language` | `english` or `taglish` |
| `validation` | `status` and `notes` |

Statuses are `unverified`, `partially_verified`, or `verified`. Documentation
review alone leaves a record **unverified**. Marking a skill partially or fully
verified requires nonempty `tested_versions`; marking an evaluation that way
requires `application_version`. These checks require metadata, but cannot prove
that testing actually happened. Record the hands-on evidence in the notes.

`documented_versions` means the source's applicability, not a compatibility
promise. `tested_versions` should identify the actual application build and
macOS version used. All initial tested-version lists are empty.

The validator checks every skill has at least one linked evaluation, each step
has an expected result, IDs are unique, sources are present, and nested records
follow this contract. It does not establish workflow correctness, interpret
arbitrary prose for safety, or prove model grounding accuracy.

## Execution flow and integration boundary

The implemented flow is deliberately small:

1. `load_library(root)` finds local JSON files under `skills/` and
   `evaluations/cases/`.
2. It parses their contents as data and checks each record against version 1.
3. It checks IDs, evaluation references, matching app/platform metadata, and
   evaluation coverage.
4. It returns the validated skill and case lists. The command-line entry point
   prints their counts.

Source URLs are provenance only; the loader never fetches them. No network,
model, screen, keyboard, mouse, or shell execution is part of this flow.

There is **no existing application-skills contract** in the Swift app.
`QuickBarModel.send()` currently calls `Agent.run()`, which observes Mac
Accessibility controls, calls `Ollama.nextAction()`, and executes the proposed
action. The new library is not called from that path. That action executor is a
different contract from this assignment's user-performed tutoring steps.

The proposed future boundary is:

```text
instruction + detected application_id + platform
  -> confident local skill match, or no match
  -> one bounded procedural reference
  -> tutor model + current live screen/Accessibility observations
  -> explain the next user action
  -> observe its result before advancing
```

App detection, intent matching, context size limits, and a teaching-only output
contract still need implementation and coordination with the macOS developer.
Never inject all skills, execute their prose, or attach them to the current
action executor and assume it has become a tutor. Source content and live UI
text remain untrusted data; the host's trusted instructions enforce behavior.
Uncertain or unrelated matches should use the ordinary tutoring path without
skill context. No inference code changed in this checkpoint.

## Hands-on and model evaluation protocol

On a test Mac, use synthetic documents and record exact app and OS versions.
For each of these three priority cases:

1. Confirm the starting state and follow each semantic step manually. Record
   observed controls, expected results, interface differences, and final outcome.
   Preview must duplicate every participating PDF before editing because it
   autosaves, then verify the originals separately.
2. Only after hands-on evidence supports a skill, run baseline A (no skill) and
   B (that one skill) through a working teaching-only inference interface.
3. Keep the model, parameters, instruction, screenshot/Accessibility snapshot,
   and starting app state identical. Reset state between runs. Keep cold and
   warm inference measurements separate.
4. Record target accuracy, grounding accuracy, instruction accuracy, completion
   verification, request-to-usable-guidance latency, added prompt tokens, and
   malformed or unsupported responses. Use real control bounds for grounding;
   if bounds or screenshots are unavailable, mark that metric unavailable.
5. Preserve per-run evidence, sample counts, and failures. Report median and
   P95 latency with the sample count; targets are median <=5 s and P95 <=10 s.
   Compare added context and latency before claiming any improvement.

Current results: **no hands-on validation, baseline runs, grounding measurements,
token-overhead measurements, or inference latency measurements**. Passing the
Python tests establishes data consistency only. The native macOS build was not
run from this Windows machine.

Automated checkpoint result on 2026-10-10: **3 skills and 3 cases validated;
24 tests passed** with Python 3.14.6 on Windows. The tests include malformed
records, duplicate JSON fields and IDs, coordinate/command fields, missing
evaluation coverage, and unsupported claims of verification.

## Remaining requested dataset

The other nine skills and cases are pending: PowerPoint tables, slide layouts,
and charts; Finder file organization and downloads; Preview PDF annotation and
image conversion; Excel grade charts and totals/averages. This checkpoint stops
before adding those records so the initial format and workflows can be reviewed.
