# Product documentation

Working product name: **LocalTutor**. Shared repository: [cadornajansen/pindo](https://github.com/cadornajansen/pindo). Target event: **AppBuildersPH Hackathon 2026**, confirmed by the lead on October 9, 2026. Organizer requirements still need official confirmation in the compliance checklist.

## Start here

| Read | Document | Question it answers |
|---|---|---|
| 1 | [Product scope](PRODUCT_SCOPE.md) | What are we building for the demo, and what can wait? |
| 2 | [PRD](PRD.md) | What must the product do, and what counts as working? |
| 3 | [Data model and ERD](DATA_MODEL.md) | What data passes between components? |
| 4 | [Validation plan](VALIDATION_PLAN.md) | How will we prove the requirements are met? |
| Reference | [Implementation](IMPLEMENTATION.md) | How is the current code organized? |
| Reference | [Team tasks](TEAM_TASKS.md) | Who owns each area and how do we contribute? |
| Submission | [Hackathon compliance](HACKATHON_COMPLIANCE.md) | What must we disclose and confirm with organizers? |

## What exists today

The WPF assistant, global shortcut, labeled mock response, shared contracts, tool validation base, and optional Ollama availability check are implemented. Inference, UI Automation capture, highlighting, and real tutoring are planned. An installer download does not establish that the runtime, model, or inference is ready.

The docs describe one PowerPoint teaching flow first. Other application support and utilities require their own evidence. The ERD describes objects in memory; the MVP has no database, accounts, or stored conversation history.

## Keep the docs honest

- Mark each capability as implemented, planned, or verified with its evidence.
- Match data fields to the [Core contracts](../src/LocalTutor.Core/TutorContracts.cs); proposed invariants are labeled separately.
- Tie test results to the tested commit, hardware, application version, language, and display configuration.
- The lead owns scope and shared contract decisions. Member 3 maintains these docs; Member 4 contributes labeled scenarios under `docs/research/`.
- Keep AppBuildersPH as the target unless the lead changes it. A different event page in a screenshot does not change the submission requirements.
