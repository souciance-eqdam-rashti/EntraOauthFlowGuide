# Feature slices and shared domain core

Implemented on `codex/hybrid-feature-slices`, 2026-10-07. See [transition plan](hybrid-architecture-transition-plan.md) and [checkpoint evidence](hybrid-architecture-checkpoints.md).

The advisor is standalone Blazor WebAssembly. Aspire AppHost optionally orchestrates the Web resource locally; production deploys static files. The advisor has no backend API. ASP.NET Core API code is generated as sample artifacts by Guide features.

```text
src/
  EntraAdvisor.AppHost/                 Optional local orchestration
  EntraAdvisor.ServiceDefaults/         Retained server tooling
  EntraAdvisor.Web/
    Components/
      Pages/Home.razor                  Route, shell, journey transitions/reset
      App.razor, Routes.razor, Layout/   Blazor composition
      Shared/                           Core-concepts dialog
    Features/
      DesignArchitecture/               Cards, drafts, history, answer submission
      ImplementationJourney/            Guide workflow, steps, completion UI
        Rendering/                      Typed guide-content rendering
        Explanations/                   Authentication/permission explanations
      ViewArchitecture/                 Flow diagrams and architecture dialog
      ExportMarkdown/                   Markdown interaction
      ExportValidation/                 Validation export interaction
      ExportProvisioning/               Provisioning dialog and interaction
    State/AdvisorWorkspace.cs           Authoritative browser-session state
    Infrastructure/Browser/             Download interop and code highlighting
    _Imports.razor                      Imports shared by components/features
    wwwroot/                            Static shell, styles, JS interop
  EntraAdvisor.Engine/
    Contracts/                          Facts, scenarios, questions, plans, rules
    Evaluation/                         Evaluator, validation, normalization
    Questionnaire/                      Presets, questions, scheduling, mutation
    Rules/                              Stable rule metadata and reviewed sources
  EntraAdvisor.Guide/
    Contracts/                          Typed guide model
    Features/
      GenerateArchitectureGuide/         General guide and browser configuration
      GenerateDelegatedChain/            Runnable Web/API A/API B templates
      ExportMarkdown/                   Markdown generation
      ExportValidation/                 Validation scripts
      ExportProvisioning/                Provisioning data, ZIP generation
        Templates/                      Embedded PowerShell scripts
    Shared/                             Invalidation, instruction groups, tenant help
tests/EntraAdvisor.Tests/
  Features/                             Feature-specific behavior and workflows
  Engine/                               Domain/security invariants
  CrossFeature/                         Workspace and exhaustive guide consistency
  Fixtures/                             Scenario examples
```

## Ownership and interaction

`Home` owns cross-stage navigation, reset, shell-heading focus, and journey announcements. Design owns question UI and callbacks. `DesignJourneyState` is scoped to the browser app so history/drafts survive conditional disposal of Design; it stores no duplicate scenario or evaluation. `AdvisorWorkspace` remains the sole owner of the authoritative questionnaire session, guide, completion, and active step.

Selecting a card updates a draft. Continue applies relevant answers through the workspace and engine. Design reports readiness to Home, which invokes the `GenerateGuide` workflow. Guide generation consumes the current validated plan, and workspace installation reconciles completion. The Implementation component owns step rendering, step selection, completion controls, and post-render step focus/scroll behavior.

Reset clears both workspace and transient Design state. Home passes a revision parameter to Design to trigger rendering when reset changes injected mutable state while the same child instance remains mounted. Child-local events otherwise follow Blazor's normal event rendering.

Export feature components invoke the Guide exporters directly and use `BrowserDownload` for browser delivery. The adapter preserves `advisor.download`/`advisor.downloadBytes`, filenames, and payloads, and allows JS errors to reach feature-specific feedback. It does not generate artifacts or decide permissions.

Web features may compose other feature components and depend on shared state/contracts. Engine has no Web/Guide dependency; Guide depends on Engine. Neither library depends on MudBlazor or browser interop. Public Engine/Guide namespaces remain stable despite physical file relocation. Provisioning templates explicitly retain logical resource names `EntraAdvisor.Guide.Provisioning.<filename>`.

## Extending a feature

1. Locate the capability in `Web/Features`, then its generator/exporter in `Guide/Features` where applicable.
2. Keep feature components and isolated CSS together. Promote components to Shared only after actual cross-feature reuse.
3. Keep authentication decisions in Engine. Consume validated plans rather than reproducing rules in a component, exporter, or sample generator.
4. Keep one session owner. Introduce local draft/navigation state only with an explicit lifecycle and reset policy.
5. Add a workflow class when it coordinates meaningful behavior or offers useful testability. Direct calls are appropriate for simple actions; no mediator framework or universal handler interface is required.
6. Match checks to the change: browser checks for UI, domain regressions for engine, full consistency tests for shared guide decisions/content, regeneration and sample builds for templates, actual artifact checks for exports.

For runnable API samples, edit `Guide/Features/GenerateDelegatedChain/DelegatedChainTemplates.cs`, regenerate with SampleWriter, and build the ignored generated projects. They are separate applications with their own server-side authentication; the advisor's browser state stores no credentials or tokens.
