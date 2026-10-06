# Entra OAuth Advisor

A .NET application that will help developers choose Microsoft Entra authentication approaches and work through a sequential setup guide.

The advisor runs entirely in the browser as standalone Blazor WebAssembly with MudBlazor. The .NET engine generates scenario-specific implementation guides and downloadable Markdown, Bash curl, Windows curl and PowerShell exports. No application backend is required.

## Run locally

Prerequisite: .NET SDK 10.0.400 or a later patch in that feature band.

```powershell
dotnet restore EntraAdvisor.slnx --locked-mode
dotnet build EntraAdvisor.slnx --no-restore -m:1 -p:UseSharedCompilation=false
./scripts/Start-Advisor.ps1
```

Open http://localhost:5077. This uses a development file server; production uses static hosting. Session progress resets on reload.

## Publish for static hosting

```powershell
./scripts/Publish-StaticSite.ps1
# For a repository GitHub Pages URL:
./scripts/Publish-StaticSite.ps1 -BasePath /EntraOauthFlowGuide/
```

Deploy the contents of `artifacts/static-site/wwwroot`. See [static deployment](docs/static-deployment.md) for Azure routing, GitHub base paths and verification.

## Checks and project structure

```powershell
dotnet test tests/EntraAdvisor.Tests --no-restore
```

The exhaustive questionnaire consistency check covers all concrete paths in the ten presets and takes about eleven minutes. For a focused hosting/UI iteration, use `--filter 'FullyQualifiedName!~GuideConsistencyMatrixTests'`; retain the exhaustive check for guide-content changes.

- `EntraAdvisor.Web`: standalone Blazor WebAssembly UI and browser session state.
- `EntraAdvisor.Engine`: deterministic decisions and branching questions.
- `EntraAdvisor.Guide`: guide content and exports.
- `EntraAdvisor.Tests`: engine, guide and workspace regression checks.
- `EntraAdvisor.AppHost` and `EntraAdvisor.ServiceDefaults`: retained optional local Aspire tooling; excluded from the deployed client.

## Delivery and resuming work

Read [requirements](docs/requirements.md), [implementation plan](docs/implementation-plan.md) and [progress](docs/progress.md) before continuing. The [coverage matrix](docs/coverage-matrix.md) defines supported platforms and topology boundaries; [architecture](docs/architecture.md) defines contracts and intended rule ordering.

See [engine behavior](docs/engine-behavior.md) for concrete decision examples, coverage limits and source/test evidence, and [questionnaire model](docs/questionnaire-model.md) for the UI integration contract.

Each minor checkpoint updates durable documentation. Each major milestone requires explicit user approval before the following milestone starts. Graph, Azure and custom resources are category-level choices; the developer supplies exact permissions/roles, and guides expose compatibility prerequisites.

See [the vertical slice](docs/vertical-slice.md) for template boundaries and generated sample build commands, and [an example export](docs/evidence/milestone3-export.md) for the complete fresh-start guide. Browser session progress is temporary; export before leaving. Exports initiate browser downloads.
