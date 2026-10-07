# Hybrid architecture transition: checkpoint evidence

Completed 2026-10-07 on branch `codex/hybrid-feature-slices`. The refactor preserves the standalone WebAssembly hosting model, evaluator decisions, generated authentication samples, and export contracts. See [current architecture](hybrid-architecture.md) and [transition plan](hybrid-architecture-transition-plan.md).

## Checkpoint record

| Checkpoint | Implemented | Checks and observed result |
| --- | --- | --- |
| 0 — Baseline | Captured generated artifact SHA-256 hashes; inspected resource names, imports, shared state, and component lifecycle | Locked restore; solution build: zero warnings/errors; full suite: 120 passed. Ran app at localhost:5077 and verified topology picker/question navigation. Generated and compiled Web, ApiA, ApiB: zero warnings/errors. |
| 1 — Exports | Feature folders for Markdown, validation, provisioning; shared BrowserDownload adapter; paired dialog stylesheet move | Web build: zero warnings/errors. Ran checkpoint app; browser/API journey reached Implementation with existing export controls. Markdown/Bash actions invoked without error; Bicep dialog rendered and ZIP generation reported Download started. Browser download-event capture timed out; later filesystem verification confirmed actual text downloads. |
| 2 — Design | Design component, local cards, scoped DesignJourneyState, workspace relocated to State | Build: zero warnings/errors; 22 existing questionnaire/workspace tests passed; two new Design state tests passed after correcting preset/equality assumptions. Ran checkpoint app; Continue advances, Back restores committed selection, reset returns to picker. Browser check caught injected-state reset not rerendering the child; added an explicit revision parameter and rechecked successfully. |
| 3 — Implementation | Implementation component, generation workflow, renderer/explainer folders, architecture display feature, shared core-concepts dialog | Solution build: zero warnings/errors; seven workflow/workspace tests passed. Ran checkpoint app; ready browser/API plan rendered its eight steps, completion advanced and remained visible when revisiting, step-heading focus moved correctly, switching to Design retained the existing Open implementation action. |
| 4 — Libraries | Guide feature folders and Shared; Engine Evaluation folder; explicit logical names for embedded PowerShell templates | Solution build: zero warnings/errors; full suite: 124 passed. All 78 baseline generated files remained byte-identical after regeneration. Ran checkpoint app; complete browser/API journey and setup PowerShell ZIP succeeded; no browser console errors observed. |
| 5 — Tests/docs and final integration | Feature/Engine/CrossFeature/Fixtures test organization; download adapter payload/error coverage; README, onboarding, architecture and progress updated | Full suite: 125 passed, zero failed/skipped. Regenerated Web, ApiA, ApiB compiled with zero warnings/errors. Release publication succeeded with the SDK workaround below. Ran published app on a plain static server at localhost:5080; root and /EntraOauthFlowGuide/ hosting loaded. API-only, delegated-chain, worker/app-only, and mixed server/browser journeys reached Implementation; browser/API was checked during earlier checkpoints. Mobile-width layout and keyboard-operated diagram dialog checked. Actual Markdown, Bash curl, Windows curl, and PowerShell files verified on disk as nonempty export content. |

## Final verification details

- Baseline test count: 120. Added five meaningful tests: draft/commit/history ownership, Design reset, incomplete guide generation, same-plan completion preservation, and browser delivery payload/error behavior. Final test count: 125.
- The exhaustive GuideConsistencyMatrixTests ran in baseline, library, and final suites. In this environment full suites completed in about 27–46 seconds; README estimates are not guaranteed timings.
- The 78 compared files are generated source/configuration, Markdown, and validation artifacts captured before sample compilation. Ignored binaries, timestamps, and restore output were excluded from the baseline capture.
- The generated projects retain their existing API audience/scope enforcement and delegated/OBO configuration. Library source was relocated without changing engine/generator decisions or public namespaces.
- Provisioning templates keep their original logical resource names despite new physical paths. Regression tests and browser ZIP generation exercise resource loading.
- Published root and repository-subpath checks used the same Release static assets. The temporary local static server served the index with the corresponding base href for each mount; no cloud site was deployed.
- Browser checks included reset, Back/Continue, implementation navigation, completion retention, step focus, dialogs, four text export actions, provisioning download initiation, and a 390 × 844 viewport inspection. The temporary viewport override was reset afterward.
- Downloaded text files were inspected on disk; Markdown describes separate Browser Component/Web App responsibilities for the mixed scenario, and downloaded script files contain text rather than an HTML error response. The complete generated artifact comparison supplies before/after output evidence.
- `git diff --check` passed. Existing invariant tests continue to exercise changed-answer invalidation, no-op preservation, security boundaries, and deterministic plans.

## Resolved issues and verification limits

The initial Release publish through Publish-StaticSite.ps1 failed when the SDK could not start/connect to an out-of-process ComputeWasmBuildAssets task host. Retrying with a single MSBuild worker and shared compilation disabled succeeded:

```powershell
dotnet publish src/EntraAdvisor.Web -c Release -o .artifacts/hybrid-static --no-restore -m:1 -p:UseSharedCompilation=false
```

The publisher script and application settings were not changed to hide this environment issue. The Release artifact was subsequently exercised on a plain static server.

Browser download-event capture was not reliable through the browser tool. Export actions were checked in the browser and resulting text files verified in the user's Downloads directory. Provisioning ZIP generation reported success and automated exporter tests checked bundle content; no exported setup or validation script was executed against a tenant.

These are basic browser/layout/keyboard checks, not a full screen-reader audit or real Entra authentication verification. No tenant resources, permissions, credentials, deployment configuration, or public hosting were changed. The transition does not claim new platform/template coverage or completion of the product's remaining milestone 4 work.

## Review and rollback

The branch contains checkpoint commits for export extraction, Design extraction/reset rendering, Implementation extraction, library relocation, and test/documentation alignment. Each checkpoint can be reviewed separately. Reverting changes in reverse dependency order requires no data migration; browser state remains temporary. The main checkout is not merged or switched by this implementation.
