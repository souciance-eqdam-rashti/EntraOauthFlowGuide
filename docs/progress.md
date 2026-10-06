# Implementation progress

Updated: 2026-10-06.

## Current state

- Milestones 1–3 are complete: foundation, deterministic engine, branching questionnaire and the delegated-access product journey.
- Completed checkpoints: 1.1–1.3, 2.1–2.3 and 3.1–3.3. Current state: milestone 4 started; architecture-driven setup checklist checkpoint complete.
- Next action: extend complete runnable platform-specific templates and compile representative samples for the remaining milestone 4 coverage. Configuration checklists are available now; milestone 4 is not complete.
- Authorization: user requested implementation on 2026-10-05, authorizing milestone 1. Milestones 2 and 3 approved on 2026-10-05. The later request to implement the Implement section authorizes the current milestone 4 setup-checklist expansion.

## Planning record

Completed: brought the requirements document from the main checkout into this worktree; recorded the user's resource categories, target platforms, fresh-start guides, multitenant coverage and available test tenant. Defined bounded topology coverage and a five-milestone implementation plan with minor checkpoints and major approval gates.

Affected files: docs/requirements.md, docs/implementation-plan.md and docs/progress.md.

Validation: reviewed document consistency and relative documentation links. No application code or tenant changes were made; builds and tenant tests are not applicable to this planning checkpoint.

Decisions: resource categories are Graph, Azure and custom resources; exact operation mappings were initially proposed; superseded by the user's category-only decision recorded in checkpoint 1.1. Initial topology limit is five application components/eight directed relationships, with provider-owned resources excluded from the component count. Windows desktop is the initial native target.

Open dependencies: confirm second-tenant access before cross-tenant end-to-end tests; pin authentication package versions when templates are implemented.

## Checkpoint entry format

For every completed checkpoint, append:

- Milestone/checkpoint identifier and date.
- Completed work and affected files.
- Validation commands or manual checks, results and evidence locations.
- Decisions, assumptions, rule/template review status and tenant verification status.
- Blockers and remaining limitations.
- Exact next action and whether user approval is required.

For each major approval, record the user's approval, date and authorized next milestone. Do not record secrets or rely on chat history as the sole progress record.

## Checkpoint 1.1 — complete, 2026-10-05

Authorization: the user requested implementation on 2026-10-05; milestone 1 is authorized. Later major milestones remain gated.

Completed: aligned requirements and plan with category-only resource selection; defined platforms, topology presets, bounds, tenant coverage and explicit compatibility prerequisites in coverage-matrix.md. Exact permissions/roles are developer-supplied; no service catalog is required.

Affected files: docs/requirements.md, docs/implementation-plan.md, docs/coverage-matrix.md, docs/progress.md.

Validation: reviewed coverage against the user's platform list and original scenario families; distinguished architectural unknowns from deployment/authorization placeholders. No code or tenant validation yet.

Decisions: use WPF as representative Windows desktop target and framework-neutral JavaScript/TypeScript browser samples. Second-middle-tier and mixed-Blazor presets are bounded edge cases.

Blockers: none for foundation work. Second workforce tenant remains a later verification dependency.

Next action: checkpoint 1.2 — verify versions, scaffold projects, build and check local orchestration. No approval required at this minor checkpoint.

## Checkpoint 1.2 — complete, 2026-10-05

Completed: scaffolded all six solution projects; selected .NET SDK 10.0.400/net10.0, Aspire 13.6.0 and Fluent UI Blazor 5.0.0; configured service discovery, health checks, OTLP telemetry, server interactivity and locked package resolutions. Official sources and selected versions are in technical-stack.md.

Affected files: global.json, Directory.Build.props, EntraAdvisor.slnx, src projects, tests project, package lock files and docs/technical-stack.md.

Validation: restore succeeded; solution build with --no-restore -m:1 -p:UseSharedCompilation=false succeeded with zero warnings/errors. Aspire started its advisor-web child. HTTP checks returned 200 for the home page, /health (Healthy) and Fluent UI stylesheet; expected page title/button were present.

Environment notes: sandbox blocks outbound NuGet and Aspire profile/socket writes; authorized escalated restore and local startup succeeded. Compiler-server pipes are restricted, so use a single build worker and disable shared compilation for sandbox checks. AppHost intentionally uses SDK-restored orchestration instead of a separately installed Aspire CLI; ASPIRE010 is documented and suppressed. A DCP log-socket warning occurred, but orchestration and health checks succeeded.

Review status: foundation sources reviewed 2026-10-05. No OAuth rule/template or tenant behavior is verified yet. Full accessibility and interactive-journey checks remain later milestones.

Next action: checkpoint 1.3 — define typed domain/guide contracts, version metadata and documented rule precedence. No approval required at this minor checkpoint.

## Checkpoint 1.3 — complete, 2026-10-05

Completed: implemented immutable typed architecture facts, resources, per-hop decisions, API validation/authorization responsibilities, question definitions/dependencies, structured evaluation statuses, rule metadata, typed guide steps/content, code artifacts, developer values and version contracts. Documented normalization, precedence, conflict handling and dependency invalidation design in architecture.md. Added README setup/resume instructions and the local startup script.

Affected files: src/EntraAdvisor.Engine/Contracts, src/EntraAdvisor.Guide/Contracts, tests/EntraAdvisor.Tests/ContractSafetyTests.cs, docs/architecture.md, README.md, scripts/Start-Advisor.ps1 and checkpoint documentation.

Validation: final solution build passed with zero warnings/errors; six contract safety tests passed. Locked restore from already downloaded dependencies passed (--locked-mode --ignore-failed-sources -p:NuGetAudit=false); disabling audit for this offline check is not a vulnerability assessment. Aspire web health and liveness remained 200/Healthy during the sustained startup check. Host was stopped after checks.

Known limitation: Aspire 13.6 reported one-minute idle Container/ContainerExec watcher timeouts even with loopback excluded from the proxy. The web resource continued to serve and pass health/liveness checks. No containers are modeled in this solution; document and revisit this orchestration diagnostic before adding container resources. Do not describe the diagnostic stream as entirely clean. The initial DCP log-socket warning did not recur in the second observation.

Review status: contracts and source boundaries verified; evaluator/guide implementations, generated authentication code, complete UI accessibility and test-tenant verification are not implemented or verified yet.

Next action: milestone 1 report and approval gate. After explicit approval, checkpoint 2.1 implements component classification, topology validation and per-hop decisions. No milestone 2 work has started.

## Major milestone 1 — awaiting continuation approval

Outcome: all three foundation checkpoints complete. Requirements, coverage matrix, stack, contracts, dependency ordering and resume instructions are recorded. Build and six tests pass; local orchestration serves a healthy web resource with the watcher diagnostic noted above.

Approval requested: proceed to milestone 2 (deterministic engine and branching questionnaire model). Record the user's response here before starting it.

## Major milestone 1 approval — 2026-10-05

User response: "yes go ahead". Authorized scope: milestone 2, deterministic engine and branching questionnaire model. Milestone 3 remains gated.

Current checkpoint: 2.1 in progress. Next action: implement scenario normalization/validation, client classification and per-hop decisions with safety tests.

## Checkpoint 2.1 — complete, 2026-10-05

Completed: implemented normalized architecture validation, public/confidential classification, per-hop authorization-code/OBO/client-credential/device decisions and explicit blocked statuses. Added stable rule metadata with reviewed official Microsoft sources. Unsupported domains, topology limits, device policy constraints and ordinary managed-identity cross-tenant/OBO boundaries are explicit.

Affected files: Engine contracts (schema 1.1.0), ScenarioNormalizer.cs, ScenarioValidation.cs, ArchitectureEvaluator.cs, Rules/RuleCatalog.cs, and table-driven scenario tests.

Validation: solution build passed with zero warnings/errors; engine/contract tests cover supported platform families, unknown identity, OBO app-token rejection, public-client app-only rejection, Blazor execution conflicts, deterministic ordering and topology integrity. Test counts/results are recorded in the final milestone evidence after enrichment.

Decisions: Azure authorization is modeled as provider-specific developer-supplied authorization, rather than assuming all Azure services use the same RBAC mechanism. Certificate-backed OBO is the initial verified credential decision; federation-backed OBO returns an explicit unsupported boundary. No tenant behavior or generated guide code has been verified.

Next action: checkpoint 2.2 — enrich plans with resource compatibility, tenant/consent/assignment prerequisites and trace provenance. No user approval is required at this minor checkpoint.

## Checkpoint 2.2 — complete, 2026-10-05

Completed: category-level developer-supplied authorization values, resource compatibility and assignment prerequisites, consent actor/policy distinctions, workforce multitenant and guest assumptions, cross-tenant boundaries, managed-identity and federation prerequisites, fresh-start registration responsibilities, and versioned per-rule trace/source metadata.

Validation: solution build passed with zero warnings/errors; prerequisite tests check category-only authorization, known incompatibility, managed identity assignment, cross-tenant/guest plans, federation/OBO boundaries and reviewed source provenance. No service-specific permission catalog was introduced.

Decisions: a Ready plan is architecturally complete but conditional on the visible target/policy prerequisites; no test claims tenant verification. Ordinary managed identity cross-tenant and federation-backed OBO remain explicit MVP boundaries rather than inferred support. Sources were reviewed 2026-10-05, rule version 1.0.0/schema 1.1.0.

Next action: checkpoint 2.3 — branching question scheduling, editable presets, typed answer projection, dependency invalidation and affected checklist state. No approval required at this minor checkpoint.

## Checkpoint 2.3 — complete, 2026-10-05

Completed: editable topology presets, card definitions with explicit Not sure/help, relevant-question scheduling (one to three per screen), typed answer application, immutable session state, dependency closure, stale-derived-fact reset, full recommendation replacement and guide completion invalidation. Added explicit external-user caller boundaries for API/OBO scenarios and both-mode API authorization checks. Resource category edits create/remove only applicable custom API branches.

Affected files: Engine/Questionnaire, Guide/GuideCompletionInvalidator.cs, Engine contracts/validation/normalization/evaluator, questionnaire and invariant tests, architecture.md, engine-behavior.md, questionnaire-model.md and README.md.

Validation: final solution build succeeded with zero warnings/errors; all 71 tests passed. Every editable preset reaches Ready without screens over three questions. Tests cover unknowns, missing architectural facts, browser/device branch removal, stale security facts, source provenance, normalized deterministic plans, public-client safeguards, cross-tenant model consistency, external OBO context, fan-out identities, completion reset/preservation and rejected invalid options.

Decisions: explicit external-user token context is permitted only at an API validation boundary; both-mode APIs require both scope and application-role authorization decisions. Cross-tenant presets use the workforce multitenant model; mixed per-component account audiences remain outside the bounded MVP. Category-level compatibility/authorization prerequisites remain visible, without a service catalog.

Limitations: these tests validate architecture and state behavior, not real token validity, consent grants, deployed tenant configuration, generated code or accessible UI. Runtime negative token/consent checks remain sample/tenant work in milestones 3–5. The existing Aspire watcher diagnostic remains documented; no orchestration code changed in milestone 2.

Next action: report milestone 2 completion and wait for explicit approval for milestone 3 (complete delegated-access visual slice). No milestone 3 implementation has started.

## Major milestone 2 — awaiting continuation approval

Outcome: checkpoints 2.1–2.3 complete. Rule version 1.0.0/schema 1.1.0, reviewed official sources, concrete decision examples, branching model and validation evidence are documented. Build and 71 tests pass.

Approval requested: proceed to milestone 3, Design → Recommendation → Implement → Markdown export for the delegated server web/API chain. Record the user's response before starting it.

## Major milestone 2 approval — 2026-10-05

User response: "Go ahead with the 3rd milestone". Authorized scope: milestone 3, the complete delegated server web/API chain visual slice. Milestone 4 remains gated.

Current checkpoint: 3.1 in progress. Next action: accessible Design cards, navigation and architecture preview.

### Milestone 3 implementation checkpoint — 2026-10-05

Design cards, stage navigation, accessible native radio groups and live architecture preview are implemented. Recommendation shows per-hop decisions, registration responsibilities, prerequisites, rule rationale and editable answers. Circuit-scoped state invalidates stale guides and affected completion. Solution builds with zero warnings; 75 tests pass including new template safety, export consistency and dependency checks. Browser questionnaire traversal is in progress. Structured guide and three-project sample writer are implemented; sample compilation identified missing namespace imports, now corrected in the source template. Checkpoints 3.1/3.2 await completed browser verification; 3.3 remains in progress. Next: regenerate and compile samples, complete desktop/narrow/keyboard/edit/export checks, record evidence and ask for major milestone approval.

### Checkpoint 3.1 — 2026-10-05

Implemented native keyboard-accessible Design choice cards, Back/Continue navigation, topology presets and an evolving architecture preview. Browser traversal of the delegated chain reaches a Ready recommendation without resource-specific Azure questions. Unknown facts show no flow badges. Keyboard focus moves to the stage heading. Added visible component/connection context to questions and corrected the selected-value Razor binding found during edit verification. Desktop visual inspection completed; narrow layout verification follows at checkpoint 3.3. No external tenant configuration changed.

### Checkpoint 3.2 — 2026-10-05

Implemented per-hop recommendations, audience and authorization value keys, registration responsibilities, conditional prerequisites, rule rationale and answer editing. Browser verification: changing the Web credential to Not sure disables Recommendation/Implement, removes all definitive flow badges, and preserves the architecture. Template boundary explicitly limits guide generation to this milestone's supported chain. Completion invalidation remains covered by unit tests. Next: finish template and final browser verification for 3.3.


### Checkpoint 3.3 — 2026-10-05

Completed: structured fresh-start certificate/delegated chain guide, registration and consent breadcrumbs, v2 API manifest fragment, complete .NET 10 Blazor/API project files, expected results, local certificate instructions, positive/negative test instructions, step selection/completion/block/reopen controls, escaped local syntax highlighting, copy feedback and Markdown export with a visible/selectable fallback. Guide generation rejects forged/current-ID-but-changed plans and unsupported template scenarios. Reopened prerequisites and changed architecture invalidate dependent confirmations. No tenant registration, permission, certificate or consent changes were executed.

Affected files: Guide generator/templates/exporter, Web components/services/styles/advisor.js, guide/workspace tests, tools/SampleWriter, README and docs/vertical-slice.md. Evidence: docs/evidence/milestone3-recommendation.jpg, milestone3-implementation.jpg, milestone3-mobile.jpg and milestone3-export.md. The exported example is generated from the final template; browser fallback inspection separately confirmed complete versions, sources, project code and a completion record.

Validation:
- Final solution build: zero warnings/errors; 78 tests passed with no skips.
- Exact generated Web, API A and API B artifacts compiled with Microsoft.Identity.Web 4.16.0; zero warnings/errors. Reproduction commands are in vertical-slice.md.
- Generated API B `/data` without a bearer token returned HTTP 401 with dummy tenant identifiers. Initial direct launch from the repository root did not load its project appsettings; corrected project working directory returned the expected result.
- Generated Blazor anonymous page and sign-in link served HTTP 200. Runtime smoke test found missing `MapStaticAssets` in the first template; fixed it, added a regression assertion and confirmed `_framework/blazor.web.js` returns HTTP 200.
- Browser journey traversed Design → Recommendation → Implement. Native Space/arrow-key selection and Enter navigation worked; Back retained checked cards; heading focus moved after navigation. Editing a known credential to Not sure disabled stale recommendation/implementation actions and removed flow annotations. Known selection binding was corrected and verified.
- Desktop and narrow viewport checks: no document horizontal overflow (reported CSS widths 1422 and 433 respectively). Mobile preview collapses above the work area. Step navigation highlights the configured API. Complete, blocked and reopened states are visible. Syntax-highlighted tokens and copy success feedback verified.
- Markdown fallback contained the complete guide, versions, sources and the user's completion record. Embedded-browser download event observation timed out, and clipboard readback did not provide reliable content verification; no native download completion/readback claim is made. The UI supplies the full selectable Markdown even when saving is blocked.

Limits: tenant-backed successful sign-in/OBO, wrong-audience/missing-scope/denied-consent tests and conditional-access challenge behavior remain milestone 5. Full screen-reader/contrast/release checks remain milestone 5. Production hosting/caching/credential deployment guidance expands with milestone 4. Other engine-supported architectures explicitly lack this milestone's guide template. Aspire's previously recorded idle container watcher timeout remains an orchestration limitation; the advisor itself stayed reachable during checks.

Milestone 3 is complete. Stop at the user's required major approval gate before milestone 4. Local sample smoke servers were stopped. Use scripts/Start-Advisor.ps1 for review; stop any running AppHost before building on Windows.

Review handoff: the completed AppHost is running locally at http://localhost:5066 (execution session 3572; log .artifacts/milestone3-review.log). The browser was reloaded to a fresh Design screen after final sample changes. Stop that host before a subsequent build; milestone 4 remains unapproved.


### Milestone 3 feedback checkpoint — 2026-10-05

User feedback authorized refinements to the completed visual journey; milestone 4 remains unapproved.

Implemented an explicit light color scheme and light body/main surfaces, higher-contrast section headings, ten short 12px real-world examples, and immediate progression from topology card selection to applicable Design questions. Removed the off-screen initial Continue action and default checked card. Recommendation still requires the architecture facts; no engine assumptions or permissions changed. Architecture preview is hidden on the initial picker and explains its call/component-tracking purpose on later screens. Initial answer feedback now says Answers saved rather than implying nonexistent guide progress was invalidated.

Validation: solution build succeeded without warnings/errors; all 78 existing tests passed. Browser computed body background is rgb(245,248,250) and heading color rgb(64,91,105). All ten examples render as a single 18px line at the desktop viewport. Mouse click and keyboard Space open the delegated-chain questions, with heading focus. Initial picker has no architecture sidebar. Narrow viewport has no horizontal document overflow. Screenshot: docs/evidence/ui-feedback-picker.jpg.

Review host restarted with the updated build at http://localhost:5066 (execution session 25329, log .artifacts/ui-feedback-review.log). Older execution session 3572 was stopped. Browser left on the revised picker. Continue gathering milestone 3 feedback; do not start milestone 4 without explicit approval.

### Milestone 3 refinement checkpoint — simpler questionnaire and authentication cards (2026-10-05)

- Completed: exactly one question per page; visible generic examples explain why each answer matters; compact responsive option cards.
- Completed: generic topology examples and API 1 / API 2 entity names replace invented business scenarios. Internal identifiers are preserved for guide compatibility.
- Completed: recommendation renders Entra, application and external resource responsibility cards, followed by numbered token-request, token-return and resource-call arrows based on the evaluated plan.
- Completed: architecture sidebar appears only during implementation; Features:AdvancedDetails defaults to false and hides Edit answers / Why these decisions.
- Completed: prominent Start implementation action for the supported delegated chain. Other ready architectures expose an explicitly limited implementation outline, with detailed guide coverage explained.
- Validation: clean build, 78 tests pass (all preset questionnaire journeys now assert exactly one question per screen). Browser exercised the certificate chain through recommendation and the 12-step implementation guide; mobile checks found no horizontal overflow at 433 effective CSS pixels. Screenshots: evidence/ui-authentication-cards.jpg and evidence/ui-single-question-mobile.jpg.
- Remaining boundary: full platform/resource/tenant-specific implementation guides outside the existing delegated chain remain milestone 4 work and require the user's major-milestone approval. No Entra tenant configuration was performed.
- Final refinement: the same authentication/responsibility cards are available in an expandable reference during implementation. Final rebuild: zero warnings/errors; 78 tests pass.

### Milestone 3 refinement checkpoint — Enterprise Entra default (2026-10-05)

Removed Not sure from all question options and removed the identity-environment question. Questionnaire sessions default an unset identity domain to Workforce; explicitly unsupported domains retain their engine boundary checks. Required unanswered facts remain unknown. Single/multitenant, guest and cross-tenant questions remain relevant. Updated preset journey tests verify the default and absent environment question; removed-option input is rejected.
- Validation: zero-warning build and 78 tests pass. Browser confirmed Yes/No without Not sure and proceeds directly from application credentials to Who can sign in?, skipping environment selection. Evidence: evidence/ui-no-unknown-option.jpg. Updated app remains running.
- Wording refinement: tenant cards now read Single Entra tenant and Multi-tenant; questionnaire references use Entra tenant and the journey heading uses Enterprise Entra identities.
- Hover contrast refinement: enabled primary buttons and active journey buttons use dark teal #004b50 with white text on hover, overriding the generic pale button hover. Primary keyboard focus has a visible offset outline.
- Back navigation fix: Back is enabled on the first question and returns to the workload picker when question history is empty. Later questions still return to the previous screen. Clean build; browser verified returning to the picker and selecting a different workload. Evidence: evidence/ui-back-to-workloads.jpg.
- Question help refinement: replaced the collapsed Help with this question disclosure with an always-visible heading and explanatory text tailored to each question fact. Guest help explains invited users, single-tenant guest access, and why tenant context/consent/access checks matter. Clean build; browser verified the guest question shows its help without interaction. Evidence: evidence/ui-visible-guest-help.jpg.


### Milestone 4 setup-checklist checkpoint (2026-10-05)

User authorized implementing the Implement section while simplifying recommendation. Removed Before implementation and duplicate Registration responsibilities from Recommendation; prerequisite checks are now implementation content. Technical flow names and official Microsoft Learn links appear only in implementation. Ready plans enable the Implement navigation action; navigating there generates the current guide rather than requiring an already-generated guide.

ArchitectureGuideGenerator validates the exact engine plan and retains the existing delegated-chain runnable template. Other ready architectures receive ordered configuration steps for tenant preparation, fresh registrations or managed identity, custom API scopes/app roles, per-connection permissions/assignments, component configuration, and positive/negative verification. Platform-specific implementation guidance is linked to official Learn pages. These guides reuse completion, dependency invalidation and Markdown export. Developer permission/audience values remain explicit placeholders.

Validation: clean build; 90 tests pass, including all 10 presets generating ordered exportable steps, application-only acquisition and forged-plan rejection. Official registration, permissions, API scopes/app roles, OAuth flow and platform setup sources were reviewed. No live tenant configuration or sign-in validation performed.

Remaining milestone 4 work: complete runnable projects beyond the delegated chain, representative compilation for each platform/credential family, production cache implementations and verified tenant edge-case templates. This checkpoint does not claim milestone 4 complete. Major milestone completion still requires a user report and approval before milestone 5.
- Final validation: zero-warning build; 92 tests pass, including managed-identity registration avoidance and multitenant guest issuer handling. Browser verified both direct Implement navigation and Start implementation, plain-language recommendation without duplicate sections/tags, five-step workload checklist, completion tracking, 8,076-character Markdown export containing flow links and completion record, and no horizontal overflow at 433 CSS pixels. Evidence: evidence/ui-simple-recommendation.jpg and evidence/ui-workload-implementation.jpg. Updated host remains running (session 50634).
- Viewport/scrolling refinement: diagnosed UI-library body overflow:hidden clipping tall pages. Explicitly restored a growing document with vertical scrolling, removed the main width cap, made the sidebar width responsive and collapsible below 1100px, and added wrapping for long text/copy controls. Clean Web build. Browser confirmed Recommendation grows beyond the viewport with body overflow visible; Implementation bottom controls are reachable (scrollTop ~996px), with no horizontal overflow at 1121px or 416px CSS widths. Evidence: evidence/ui-implementation-scroll.jpg. Updated host remains running (session 32184).

### Milestone 4 validation-export checkpoint (2026-10-06)

Implemented connection-specific cURL (Bash) and PowerShell exports from the exact validated plan. Implementation lets users choose a connection, request either format, and copy/save the complete script when browser downloading is blocked. Changing the connection clears the old preview; initiating-client calls are listed before OBO calls.

Exports support certificate/federated application token requests, authorization code with PKCE/state, device-code polling and direct OBO exchange. Certificate assertions use PS256, x5t#S256 and short expiry. PowerShell 7.2+ signs using the Windows CurrentUser/My certificate; Bash uses an RSA PEM certificate/key, OpenSSL, jq and cURL. Managed-identity and API-only probes require an audience-matched access-token file from the real host/caller and explicitly verify resource access rather than pretending to acquire that identity. Sign-in-only probes check that a response was obtained and require the app/library to validate ID-token trust. Scripts do not create registrations, change consent or write tenant resources.

Validation: clean solution build, 98 tests pass; generated 16 probe pairs across all presets and parsed all 16 PowerShell scripts without errors. Offline verification confirmed PS256 signature, SHA-256 certificate hash, audience and five-minute assertion lifetime using an ephemeral in-memory certificate. Exported API-only PowerShell script executed against a temporary loopback mock: authenticated GET 200, missing-token GET 401. The mock was stopped. Browser checked the OBO PowerShell preview (9,202 characters), code-flow cURL preview (8,441 characters), correct selected mode, complete visible content and clearing on connection changes. Evidence: evidence/ui-validation-exports.jpg.

Limitations: no live Entra tokens/tenant checks performed; Bash exports were reviewed but not executed because Bash/OpenSSL tooling is unavailable in this host environment. End-to-end consent, policy, identity and authorization checks remain milestone 5. These local protocol probes do not replace testing the actual application's library integration or deployed API chain. Milestone 4's broader runnable platform-template work remains incomplete.

- Final export build remains clean; 98 tests pass. Updated review host is running (session 74048). Browser is on the delegated-chain Implementation page with Web App → API 1 selected first; both export buttons are available. See validation-exports.md for usage and verification boundaries.

## Implementation UX feedback — complete, 2026-10-06

Authorization: user requested the six implementation-page changes. The current checkout was the initial commit; the existing uncommitted application was copied from worktree 6959 into this checkout before editing. The earlier worktree was left unchanged.

Completed: scoped authentication-method links to their owning component in both implementation steps and the setup-responsibility cards; added platform labels including ASP.NET Core API; removed the duplicate architecture sidebar; removed displayed review dates and completion controls/counts/records; added Back/Next step navigation. Tenant preparation now contains only the required role statement. Local tooling instructions moved to the project/certificate steps, and detailed architecture requirements remain expandable in the responsibility view.

Validation exports moved below the current step. There is no connection dropdown. Each Bash curl, Windows curl, and PowerShell download includes all scenario connections with labeled sections and independently editable settings. Windows curl uses curl.exe hosted by PowerShell 7.2+ for JSON, PKCE and certificate signing. Function/subshell scopes isolate each connection's token response and sign-in return behavior.

Affected files: Home.razor, AuthenticationFlow.razor, ArchitecturePreview.razor, ImplementationFlows.razor, ValidationExport.razor, ArchitectureGuideGenerator.cs, DelegatedChainGuideGenerator.cs, MarkdownGuideExporter.cs, TenantPreparation.cs, ValidationScriptExporter.cs, ValidationExportTests.cs, tools/SampleWriter/Program.cs and README.md.

Validation: solution build succeeded with zero warnings/errors. Final dotnet test tests/EntraAdvisor.Tests --no-restore -m:1 -p:UseSharedCompilation=false passed 101 tests. SampleWriter generated exports for all ten presets; all 36 generated PowerShell files parsed without errors. The Windows curl API-only script was run against a local synthetic resource: authenticated GET 200, missing-token GET 401. Browser walkthrough verified the delegated chain, concise tenant step, component-specific links (API 1 OBO plus token validation; API 2 token validation; Web App authorization code), platform labels and bottom export buttons. Screenshot: .artifacts/implementation-redesign.png. Live Entra authentication was not tested. Bash execution was not verified because the available WSL launcher is unavailable in this environment.

Role reference: https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/grant-admin-consent (checked 2026-10-06). Cloud Application Administrator / Application Administrator covers general app configuration and consent; Microsoft Graph application permissions require Privileged Role Administrator. Azure resource role assignments additionally require an authorized role at the resource scope.

Next action: collect feedback on this revised page, then continue remaining milestone 4 platform template coverage. No further permission is needed for refinements within this redesign.
