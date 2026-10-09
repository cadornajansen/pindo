# Hackathon compliance and disclosures

This checklist reflects the **team-supplied organizer briefing**. It is not an official rulebook or evidence of organizer approval. Confirm the rules and submission form with organizers before submission.

## October 9 baseline reuse update

The lead supplied this rule: **“Pre-existing code is allowed only if the project is substantially built during the hackathon, with prior work disclosed.”** Reuse is therefore part of the team's stated plan; this document does not independently certify substantial new development.

Pointly's current application, tests and source notes have now been imported. Source HEAD is `960ba2e` dated September 21, 2026, with additional modified/untracked working files whose creation dates are not established here. Disclose the full imported snapshot as prior work. See [the complete inventory](POINTLY_IMPORT.md). Preserve later commits to identify actual hackathon contributions. Copying, polishing and selecting a model alone do not establish the organizer's substantial-development criterion.

The active solution is `Pointly.sln`; the original LocalTutor scaffold remains separate. The imported runtime includes cloud AssemblyAI tutoring, OpenRouter/optional Bedrock vision, and ElevenLabs speech, plus AWSSDK.BedrockRuntime 4.0.101.7 and NAudio 2.2.1. No credentials or source Git history were copied. Local inference is **not integrated yet**. Ollama 0.40.2 is installed; MAI-UI-2B (`maternion/mai-ui:2b`, community Q8 package) is selected for testing, with the user managing the download. The upstream model card identifies Apache-2.0; retain applicable notices and record exact downloaded digest before submission.

The original scaffold status below is retained as historical context. Its no-copy, missing-Ollama, UIA-not-implemented and Qwen3-candidate statements are superseded by this update and the repository README.

## Briefing details to verify

October 10 contribution update: the new pass adds shared cloud goal planning and clarification, observed-result verification, animated composer/annotations, and the PR #2 tools integration with host-owned approvals. PR #2's commit history is retained. ImageMagick, Poppler, yt-dlp and FFmpeg are installed public native dependencies; their tested versions and roles are recorded in [LIVE_TUTORING.md](LIVE_TUTORING.md). These changes remain cloud-backed; no local-inference claim is added. The historical scaffold statements below do not describe the current app.

- Build day: October 9, 2026; deadline: **October 10, 2026, 10:00 AM Philippine Time (UTC+8)**, with no extensions.
- Demo Day: October 10 at SM Makati; finalist format: 5-minute pitch/live demo and 3-minute Q&A.
- Four registered human contributors; only registered members may contribute human development work.
- Reported judging weights: usefulness 25%, local AI 25%, technical execution 20%, innovation 15%, product/demo 15%.

## Requirements and present evidence

| Requirement from briefing | Current state / required action |
|---|---|
| Meaningful inference runs locally | **Not yet satisfied:** scaffold uses a labeled mock. Implement and demonstrate local inference before submission. |
| Substantial development during build period | Fresh scaffold started October 9, 2026; preserve commits and team contribution history. Confirm exact permitted build window. |
| Disclose pre-existing code/assets | No previous application's source code or assets were copied into this project. Earlier experimentation informed the idea only. Keep reviewing new contributions. |
| Disclose models, frameworks, libraries, APIs, and AI tools | Initial inventory below; update it for every added dependency or service. |
| Human contributions only by registered members | Verify collaborator identities against registration; AI agents are development tools, not registered humans. |
| Public GitHub repository | [cadornajansen/pindo](https://github.com/cadornajansen/pindo) created public; verify final submission source on `main` before the deadline. |

Prior cloud-model experiments are background research, not new local-model results. Their earlier structural-output and latency observations did not establish complete grounding accuracy. No screenshots, datasets, code, or assets from those experiments are included in this bootstrap.

## Initial disclosure inventory

| Item | Role / status |
|---|---|
| C#, .NET 10, WPF, native Windows APIs | Application and shortcut implementation; include applicable upstream licenses/notices |
| Windows UI Automation | Intended accessibility integration; not implemented yet |
| Ollama | Planned local runtime; availability-client plumbing only; absent on bootstrap machine |
| Qwen3 `qwen3:1.7b` | Initial candidate; not downloaded or benchmarked; record exact model/license/quantization when selected |
| xUnit, Microsoft.NET.Test.Sdk, xunit.runner.visualstudio | Test dependencies; exact versions are in the test project file |
| Git, GitHub, JetBrains Rider | Version control, remote hosting, intended IDE |
| OpenAI Codex with GPT-6 and its implementation agents | AI-assisted bootstrap coding and documentation; retain accurate model/tool details from the development session |
| Context7 MCP | Current technical documentation lookup during development |

No cloud LLM is required to compile or launch this scaffold. AssemblyAI, ElevenLabs, whisper.cpp, FFmpeg, yt-dlp, PDF libraries, and other discussed utilities are **not integrated**. If added later, record their role, license, network dependency, and usage before submission.

## Submission checklist

- [ ] Confirm official requirements and exact deadline with organizers.
- [ ] Confirm repository is public and its default branch contains the final source.
- [ ] Complete project information and technology/model/AI-tool disclosures.
- [ ] Produce a demo video showing real local inference, and disclose any simulated portions.
- [ ] Publish the required social-media video post and retain its link.
- [ ] Record hardware, model version, tested tasks, and measured latency/accuracy.
- [ ] Prepare finalist pitch/demo if selected; never claim unverified offline or privacy behavior.

## Open organizer questions

- What is the exact build-period boundary, and how should prior research be disclosed?
- Is there a required format for AI-agent, model-license, and third-party dependency disclosures?
- What qualifies as sufficient meaningful local inference, and how should optional online features be demonstrated?
- What are the required video format/length, social platform/tagging rules, submission fields, and public-repository timing?

No organizer confirmation has been recorded here. Do not convert these uncertainties into assumed permission.
