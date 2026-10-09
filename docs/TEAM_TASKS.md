# Team tasks and onboarding

Use one shared repository with direct collaborator access. The lead should invite the three registered members in **GitHub repository → Settings → Collaborators → Add people** after receiving their actual GitHub usernames. No invitations have been sent or usernames inferred.

Public repository: [cadornajansen/localtutor-hackathon](https://github.com/cadornajansen/localtutor-hackathon).

```powershell
git clone https://github.com/cadornajansen/localtutor-hackathon.git
cd localtutor-hackathon
dotnet restore LocalTutor.slnx
dotnet build LocalTutor.slnx --no-restore
dotnet test LocalTutor.slnx --no-build --no-restore
```

Open `LocalTutor.slnx` in Rider. Windows and .NET 10 are required for running the WPF app. Ollama is not required for the mock scaffold.

## Ownership and first deliverables

| Member / branch | Files owned | Expected input → output |
|---|---|---|
| 1 — technical lead / `feat/ai-desktop` | `src/LocalTutor.Desktop/**`, `src/LocalTutor.Core/**`, solution/config, shared integration tests | Scoped educator task + contract agreement → desktop tutoring integration |
| 2 — C# utilities / `feat/local-tools` | `src/LocalTutor.Tools/**` and new tool-specific files in `tests/LocalTutor.Tests/` | Explicitly approved typed tool request → validated result and focused tests |
| 3 — documentation / `docs/product` | `README.md`, `docs/PRD.md`, `docs/IMPLEMENTATION.md`, `docs/HACKATHON_COMPLIANCE.md`, `docs/TEAM_TASKS.md` | Verified implementation/rules → accurate setup, disclosures, demo/submission instructions |
| 4 — research / `research/educators` | New scoped files such as `docs/research/teacher-scenarios.md` and `docs/research/validation-prompts.md` | Sourced educator evidence → small, testable English/Filipino/Taglish scenarios and expected UI targets |

Only the lead changes Core contracts. Member 2 uses `LocalTutor.Core.Tools` and derives from `LocalTutor.Tools.LocalTool<TInput, TOutput>`; the exact API is in [implementation notes](IMPLEMENTATION.md). Request a contract change before editing shared interfaces. Utilities must not edit the desktop AI pipeline or accept arbitrary shell commands.

Member 4 should create scoped research files rather than editing Member 3's core documents concurrently. A useful scenario records application/version, starting screen, teacher instruction, expected next control, and pass/fail criteria. Existing benchmark claims must remain labeled as historical and unverified for this product.

## Branch and pull request workflow

After cloning, run **one** command matching your responsibility:

```powershell
git switch -c feat/ai-desktop
git switch -c feat/local-tools
git switch -c docs/product
git switch -c research/educators
```

1. Start from current `main` and agree on one small deliverable. Do not start the full feature backlog.
2. Keep changes within your owned files. Coordinate before editing another member's files or shared configuration.
3. Build and run relevant tests; documentation changes should be checked against actual code and evidence.
4. Commit the focused change and push your branch with `git push -u origin HEAD`.
5. Open a pull request to `main` stating the behavior, changed files, checks performed, and remaining limitations. The lead reviews/integrates.

Do not force-push `main`, commit secrets/build artifacts, or merge unrelated changes. `main` is the stable integration branch by team convention; server-side branch protection is not claimed to be enabled.

## Immediate next checkpoint

The lead benchmarks Ollama/Qwen3 on the actual laptop and begins bounded UI Automation capture. Member 2 agrees one approved utility contract before implementation. Member 3 verifies setup/disclosures against real results. Member 4 supplies a few concrete PowerPoint teacher scenarios and labeled expected targets. Further implementation starts only after a focused scope is agreed.
