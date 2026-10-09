# Hackathon compliance and disclosures

This checklist reflects the **team-supplied organizer briefing**. It is not an official rulebook or evidence of organizer approval. Confirm the rules and submission form with organizers before submission.

## Briefing details to verify

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
| Public GitHub repository | [cadornajansen/localtutor-hackathon](https://github.com/cadornajansen/localtutor-hackathon) created public; verify final submission source on `main` before the deadline. |

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
