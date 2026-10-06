# Entra OAuth Advisor

A .NET application that will help developers choose Microsoft Entra authentication approaches and work through a sequential setup guide.

Current status: milestone 4 setup-checklist checkpoint complete. Design → Implement is available, including a fresh-start single-tenant Blazor server → API A → API B delegated certificate guide, a two-pane step selector and reading view, modal Diagram Overview, component-specific authentication links, syntax-highlighted code, copy feedback, Markdown export and all-connection Bash curl/Windows curl/PowerShell validation scripts. Ready architectures now have exportable configuration checklists and official platform/flow links. Complete runnable templates beyond the delegated chain remain milestone 4 work; live tenant verification is milestone 5. See [progress](docs/progress.md) for the approved scope and exact resume point.

## Run locally

Prerequisite: .NET SDK 10.0.400 or a later patch in that SDK feature band. Package versions are pinned with lock files. Aspire orchestration binaries are restored through NuGet; no container runtime or separately installed Aspire CLI is needed for this foundation.

```powershell
dotnet restore EntraAdvisor.slnx --locked-mode
dotnet build EntraAdvisor.slnx --no-restore -m:1 -p:UseSharedCompilation=false
dotnet test tests/EntraAdvisor.Tests --no-build --no-restore
./scripts/Start-Advisor.ps1
```

The startup script uses loopback HTTP for local development. Open the web resource from the Aspire dashboard at http://localhost:15093, or the app at http://localhost:5066. The dashboard prints its login link at startup. The HTTPS launch profiles remain available for development with a trusted development certificate. Stop the host with Ctrl+C before rebuilding on Windows to avoid locked executable files.

In a restricted coding sandbox, package downloads and Aspire profile/socket writes can require an authorized run outside the sandbox. Single-worker compilation with shared compilation disabled avoids restricted compiler-server pipes. See [technical stack](docs/technical-stack.md) for source links and validation limits.

## Project structure

- `EntraAdvisor.AppHost`: local orchestration and dashboard.
- `EntraAdvisor.ServiceDefaults`: telemetry, health, service discovery and resilience.
- `EntraAdvisor.Web`: server-interactive Blazor and Fluent UI.
- `EntraAdvisor.Engine`: deterministic per-connection decisions, validation, reviewed rules and branching question state.
- `EntraAdvisor.Guide`: validated guide templates, shared code artifacts, Markdown export and checklist invalidation.
- `EntraAdvisor.Tests`: 98 engine, questionnaire, guide, workspace and contract safety tests.

## Delivery and resuming work

Read [requirements](docs/requirements.md), [implementation plan](docs/implementation-plan.md) and [progress](docs/progress.md) before continuing. The [coverage matrix](docs/coverage-matrix.md) defines supported platforms and topology boundaries; [architecture](docs/architecture.md) defines contracts and intended rule ordering.

See [engine behavior](docs/engine-behavior.md) for concrete decision examples, coverage limits and source/test evidence, and [questionnaire model](docs/questionnaire-model.md) for the UI integration contract.

Each minor checkpoint updates durable documentation. Each major milestone requires explicit user approval before the following milestone starts. Graph, Azure and custom resources are category-level choices; the developer supplies exact permissions/roles, and guides expose compatibility prerequisites.

See [the vertical slice](docs/vertical-slice.md) for template boundaries and generated sample build commands, and [an example export](docs/evidence/milestone3-export.md) for the complete fresh-start guide. Browser session progress is temporary; export before leaving. The export panel supplies selectable Markdown if the browser blocks downloads.
