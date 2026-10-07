# Transition plan: feature slices with a shared domain core

Status: completed on `codex/hybrid-feature-slices`, 2026-10-07. The phases below retain the original plan; see [checkpoint evidence](hybrid-architecture-checkpoints.md) for implementation results and [current architecture](hybrid-architecture.md) for ownership and source locations.

## Outcome and scope

Organize UI interactions and guide/export capabilities around features while preserving the Engine and Guide contracts as shared foundations. A developer should be able to locate a capability's UI, workflow, generator, and tests without tracing an oversized page or unrelated component collection.

Keep the existing solution projects and dependency direction:

```text
AppHost -> Web (optional local orchestration)
Web -> Engine + Guide
Guide -> Engine
Tests -> Web + Guide + Engine
```

The advisor remains standalone Blazor WebAssembly with static production hosting. API work remains generated ASP.NET Core application templates. This transition does not introduce an advisor backend, change OAuth decisions, change supported topologies, persist browser state, or change the public journey.

Use small independently verifiable changes. Separate file relocation from behavior extraction wherever possible. Estimates below assume one developer familiar with the repository: roughly 6–10 development days plus review, with uncertainty around splitting Home and browser regression verification. The onboarding plan remains a separate 1–2 day learning activity.

## Target ownership

| Location | Owns | Must delegate |
| --- | --- | --- |
| `Web/Features/DesignArchitecture` | Preset selection, draft answers, submission, Back/Continue, question feedback | Fact validation, relevance, scheduling, evaluation to Engine |
| `Web/Features/ImplementationJourney` | Step navigation, guide generation coordination, completion presentation, typed content rendering | Guide generation to Guide; completion mutation to workspace |
| `Web/Features/ViewArchitecture` | Architecture diagrams and related dialogs | Identity decisions to validated plan |
| `Web/Features/ExportMarkdown` | Markdown export interaction and error feedback | Content generation to Guide |
| `Web/Features/ExportValidation` | Format selection, validation export interaction | Probe/script generation to Guide |
| `Web/Features/ExportProvisioning` | Setup dialog, options, export interaction | Provisioning plan and artifact generation to Guide |
| `Web/State/AdvisorWorkspace.cs` | One authoritative browser-session state and cross-feature invalidation | Questionnaire semantics and invalidation algorithms to shared libraries |
| `Web/Infrastructure/Browser` | Browser download mechanism and browser-facing utilities | Feature policy to feature code |
| `Engine/Contracts`, `Evaluation`, `Rules`, `Questionnaire` | Shared deterministic domain behavior | No dependency on Web, MudBlazor, JS, or Guide |
| `Guide/Features/*` | Architecture guide, delegated-chain sample, Markdown, validation and provisioning generation | Identity decisions to Engine |
| `Guide/Contracts`, `Guide/Shared` | Typed guide model and truly shared content/invalidation helpers | Feature-specific generation to feature folders |

Keep `Program.cs`, App/Routes, layout, and route pages as Blazor composition infrastructure. `Home.razor` retains the `/` route and composes the two journey features. Keep a component local to its feature until actual reuse justifies moving it into `Components/Shared`. Keep component `.razor.css` files beside their components.

Workflow names such as `ApplyAnswer`, `GenerateGuide`, and `ExportValidation` are suggested responsibilities, not mandatory classes. Introduce a class when coordination or meaningful testing warrants it. Do not add pass-through wrappers, a mediator dependency, a project per feature, or a universal handler framework.

## Phase 0 — Capture the baseline (0.5–1 day)

1. Start from the latest agreed branch state and inspect local modifications. Use a dedicated transition branch, such as `codex/hybrid-feature-slices`, when implementation begins. Do not include unrelated edits.
2. Record the current solution build and full test outcome. Record pre-existing failures explicitly; existing failures must not be silently attributed to the restructuring.
3. Generate the delegated-chain samples and compile Web, ApiA, and ApiB using [vertical-slice.md](vertical-slice.md).
4. Record representative UI journeys: browser/custom API, delegated API chain, API validation only, worker/app-only, and mixed execution. Include Back/Continue, answer changes, completion reopening, reset, dialogs, and export.
5. Capture baseline plan/guide/export output from fixed fixtures. Compare complete plan/guide content and exported text after restructuring, including placeholders, versions, ordering, and generated filenames. Avoid environment-dependent paths in comparisons.
6. Inventory namespaces, Razor imports, CSS selectors, source-path assertions, resource loading, and any scripts depending on current paths. Record a move map in the PR description.

**Exit:** reproducible baseline, known failures recorded, move inventory, and agreed representative journeys. This is a planning/refactoring checkpoint; the existing product milestone approval policy in [implementation-plan.md](implementation-plan.md) remains applicable to actual major product milestones. These phases do not independently authorize new product functionality.

## Phase 1 — Extract export interactions (1–1.5 days)

Start with validation export because it is bounded and already uses a distinct component.

1. Move `ValidationExport.razor` to `Web/Features/ExportValidation`. Update namespaces/imports and callers without changing its behavior.
2. Extract a browser download adapter from repeated `advisor.download` invocations. Preserve filenames, payloads, JS behavior, and user-facing error handling. Keep `advisor.js` in `wwwroot`.
3. Move the provisioning dialog and its isolated stylesheet to `Features/ExportProvisioning`. Preserve typed dialog parameter names and CSS relationships.
4. Extract Markdown export from Home into its own feature component. Continue composing the export controls in the implementation journey; preserve current button ordering and accessibility labels.
5. Introduce workflow classes only if they coordinate a meaningful validation/generation/download sequence. Keep artifact generation pure and independently callable.
6. Ensure feature entry points receive a current usable guide/plan. If adding new stale-plan validation changes behavior, make that an explicit follow-up change with regression coverage rather than burying it in a move.

**Checks:** build; focused export tests; real browser downloads for Markdown, Bash curl, Windows curl, PowerShell, and provisioning artifacts. Compare downloaded contents/filenames with baseline. Exercise download failure feedback through a controlled adapter failure where practical. Verify dialogs, keyboard focus, and narrow layout.

**Exit:** exports live in named features, use one download mechanism, and preserve generation output. Rollback consists of reverting this phase's commits; no data migration is involved.

## Phase 2 — Split the Design journey out of Home (1–2 days)

1. Create `Features/DesignArchitecture/DesignArchitecture.razor` and move topology selection, question rendering, drafts, and question-history navigation into it.
2. Move `ChoiceCards.razor` alongside it. Extract a topology-picker component only if it makes the parent clearer.
3. Preserve the distinction between draft selection and committed answer. Continue applying only relevant answers using the returned immutable questionnaire session.
4. Keep `AdvisorWorkspace` as the state owner. Move it to `Web/State` in a separate mechanical commit; update tests and imports. Preserve its API initially.
5. Have Design communicate journey-level intent through typed callbacks or a clear shared-state operation. Avoid sibling component references and direct calls into another feature's private methods.
6. Preserve history/drafts across the same navigation paths as before. Conditional rendering can dispose a child component: explicitly decide whether transient state must survive stage changes, then verify that behavior against baseline. Reset must also reset local drafts/history.
7. Move focus logic with the owning rendered heading. Preserve post-render focus timing and question keys.

If useful, introduce `ApplyAnswer` to coordinate workspace mutation and feedback. It must not copy the engine's question relevance or dependency algorithms.

**Checks:** questionnaire/workspace tests; preset selection, draft-only selection, Continue, Back, irrelevant-answer rejection, answer changes, no-op changes, reset, and stage switching. Verify exactly one scheduled question where the current model requires it, current help text, and focus behavior.

**Exit:** Home delegates Design rendering; answer application and invalidation behavior are unchanged. No competing session state exists.

## Phase 3 — Extract Implementation and architecture display (1–2 days)

1. Create `Features/ImplementationJourney/ImplementationJourney.razor`. Move the stepper, active-step presentation, completion controls, and guide-generation coordination into it.
2. Relocate typed content renderers under its `Rendering` folder and contextual explanations under `Explanations`. Move related CSS carefully: selectors in shared `app.css` remain shared unless an intentional, verified style extraction is performed.
3. Group architecture preview/flow/diagram components in `Features/ViewArchitecture`. Keep their inputs plan-based; they do not select flows.
4. Keep guide identity, active-step bounds, completion mutation, and reset safeguards in the workspace. Preserve `GuideIsCurrent` and `SetGuide` validation.
5. Extract `GenerateGuide` or `CompleteStep` only where there is useful coordination beyond a single existing method call. Preserve template-version compatibility and completion retention rules.
6. Reduce Home to page title/shell, stage composition, and narrowly necessary journey coordination. Choose a single owner for reset and cross-stage transitions; do not duplicate them in both parent and children.

**Checks:** focused workspace/guide tests; answer change followed by regeneration; unrelated completion preservation; affected/dependent completion reset; reopening prerequisites; first/last-step navigation; plan keys; dialog rendering; step-pane scroll reset and keyboard focus. Check desktop and narrow layouts and compare exports.

**Exit:** Design and Implementation have clear ownership, Home is a composition page, and browser behavior matches baseline.

## Phase 4 — Organize Guide and Engine internally (1–1.5 days)

Make this primarily a relocation phase. Do not rewrite generator dispatch or evaluator algorithms at the same time.

| Existing Guide code | Target |
| --- | --- |
| `ArchitectureGuideGenerator`, browser configuration content | `Features/GenerateArchitectureGuide` |
| `DelegatedChainGuideGenerator`, `DelegatedChainTemplates` | `Features/GenerateDelegatedChain` |
| `MarkdownGuideExporter` | `Features/ExportMarkdown` |
| `ValidationScriptExporter` | `Features/ExportValidation` |
| `ProvisioningPlan`, `ProvisioningExporter`, PowerShell templates | `Features/ExportProvisioning`, with a `Templates` child folder |
| `GuideCompletionInvalidator`, `InstructionGroups`, `TenantPreparation` | `Shared`, subject to actual reuse |

Retain typed contracts in `Guide/Contracts`. Preserve the current validated-plan boundary and delegated-chain dispatch. Preserve generated API filenames and configuration keys.

**Embedded-resource hazard:** Guide currently includes `Provisioning/*.ps1` in its project file, and `ProvisioningExporter` loads `EntraAdvisor.Guide.Provisioning.` resource names. Moving scripts requires updating the include path and either retaining existing logical resource names explicitly or changing the loader in the same commit. Prefer preserving logical names to minimize risk. Update resource-sensitive tests and confirm artifact generation for every template; a successful build alone does not prove embedded resources can be loaded.

Move `ArchitectureEvaluator`, `ScenarioValidation`, and `ScenarioNormalizer` into `Engine/Evaluation` if it improves navigation. Keep Engine contracts, rules, and questionnaire centralized. Stable rule IDs, schema/rule/template versions, plan IDs, source metadata, and deterministic ordering should not change in a behavior-preserving move. Namespace changes alone do not justify a domain version bump.

**Checks:** full suite including the consistency matrix; complete baseline output comparisons; regenerate/build delegated-chain samples; provisioning and validation matrix tests. Inspect any source-path or namespace assertions before treating failures as domain failures.

**Exit:** generators are feature-organized, all resources load, and decision/guide/export output is equivalent.

## Phase 5 — Align tests and documentation (0.5–1 day)

1. Group feature-specific tests under `Tests/Features`; keep shared decision invariants under `Tests/Engine` and session/consistency coverage under `Tests/CrossFeature`.
2. Keep tests together when they cover several features; do not split a valuable matrix merely to match folders. Separate test relocation from new test behavior.
3. Add focused feature workflow tests only where extraction introduced a meaningful seam. Assert user outcomes, stale-state handling, or failure behavior; avoid tests that only mirror method calls.
4. Update README navigation, onboarding paths, and architecture documentation to reflect standalone WebAssembly and the new ownership boundaries. Preserve clearly labeled historical milestone evidence.
5. Record migration evidence and remaining work in durable progress documentation, without claiming new OAuth coverage or real-tenant verification.

**Checks:** final solution build/full suite, generated sample builds, published static-site smoke test, representative browser journeys, and diff review. Use the existing static publish/base-path procedure from [static-deployment.md](static-deployment.md).

**Exit:** the next developer can find one feature's UI, generation, state interactions, and tests from its name.

## Validation commands and evidence

From the repository root in PowerShell:

```powershell
dotnet restore EntraAdvisor.slnx --locked-mode
dotnet build EntraAdvisor.slnx --no-restore -m:1 -p:UseSharedCompilation=false

# Focused UI/state feedback; not the final migration gate.
dotnet test tests/EntraAdvisor.Tests --no-restore --filter 'FullyQualifiedName!~GuideConsistencyMatrixTests'

# Baseline and final checks; also use after shared generator/evaluator changes.
dotnet test tests/EntraAdvisor.Tests --no-restore

git diff --check
./scripts/Start-Advisor.ps1
```

The README estimates about eleven minutes for the exhaustive consistency matrix. Run required checks once per relevant change; repeat when additional changes or failures justify it. For generated sample commands use [vertical-slice.md](vertical-slice.md). For publishing use [static-deployment.md](static-deployment.md).

Each PR should record: scope/move map; any intentional behavior change; automated results; browser journeys; output/resource comparisons where relevant; and remaining limitations. Unit tests do not verify rendered accessibility; sample compilation does not verify Entra authentication. A purely structural migration does not require new tenant configuration unless runtime authentication behavior is intentionally changed.

## Suggested review sequence

1. Export interaction extraction and browser adapter.
2. Design extraction and explicit transient-state ownership.
3. Implementation extraction and architecture display grouping.
4. Guide relocation and embedded-resource verification.
5. Optional Engine relocation, then test/docs alignment.

Keep namespace/file moves and behavior extraction in separate commits when practical. Keep each review small enough to revert independently. Do not maintain two active implementations of a workflow during transition; replace one path at a time. If a phase fails its behavior checks, repair or revert it before extracting the next dependent feature.

## Risks and controls

| Risk | Control |
| --- | --- |
| Conditional components lose drafts/history | Explicit state ownership and stage-switch regression check |
| Reset or navigation has multiple owners | One journey coordinator; narrow callbacks from child features |
| Old guide or completion reappears | Keep workspace identity/invalidation safeguards and outcome tests |
| Security decisions diverge by feature | All generators/exporters consume the shared validated plan |
| Razor namespace/parameter changes break composition | Update imports/callers, build, verify typed dialog parameters |
| Isolated CSS or focus breaks after moves | Keep stylesheet/component pairs; real-browser checks |
| Provisioning templates disappear | Preserve logical resource names and load/export every template |
| Abstractions grow faster than benefits | Direct calls by default; introduce classes only for meaningful coordination |
| Generated code changes accidentally | Compare artifacts and compile regenerated sample projects |

## Definition of done

- Design, Implementation, diagrams, and exports have discoverable feature homes.
- Home composes the journey; the workspace is the sole authoritative session owner.
- Engine remains UI-independent and deterministic; Guide still validates plans and shares typed content across UI/exports.
- Routes, UI interactions, supported scenarios, output contracts, filenames, versions, and static hosting behavior remain equivalent unless separately reviewed as intentional changes.
- All embedded templates load, generated samples compile, required automated checks pass, and representative browser journeys pass.
- Documentation identifies the actual hosting model, ownership boundaries, and new paths.
- No unnecessary backend, mediator framework, per-feature projects, or duplicate authentication logic has been introduced.
