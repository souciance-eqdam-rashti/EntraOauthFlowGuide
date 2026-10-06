# Implementation plan

Status: milestone 4 started by the user request to implement the Implement section; architecture-driven setup checklists delivered, full runnable-template coverage remains in progress. Updated 2026-10-05. Requirements baseline: 2026-10-05.

This plan implements [requirements.md](requirements.md). Every numbered checkpoint ends with validation and an update to [progress.md](progress.md). Major milestone completion requires a user report and explicit approval before the next milestone starts. Maintain durable documentation during each checkpoint; if interrupted midway, record partial work and the next action before stopping where possible.

## Milestone 1 — Supported architecture and technical foundation

1.1 Define a finite coverage matrix for platforms, topology presets, resource categories, identity modes and tenant boundaries. Identify unsupported combinations and test-tenant dependencies. Document the five-component/eight-relationship limits and questionnaire treatment of edge cases.

1.2 Verify official documentation for the selected .NET, Aspire and authentication libraries. Record versions and sources. Create the solution projects and confirm build and local orchestration.

1.3 Define typed scenario facts, per-hop decisions, evaluation statuses, question dependencies, guide contracts and versioning. Document precedence and conflict handling.

Exit evidence: reviewed coverage matrix, documented contracts, working solution build and local startup. **Approval gate: approve the scope and foundation before engine implementation.**

## Milestone 2 — Deterministic engine and branching questionnaire model

2.1 Implement component classification, explicit unknown states, topology validation and per-hop identity decisions.

2.2 Implement resource compatibility prerequisites, developer-supplied authorization values, tenant boundaries, consent prerequisites and decision traces with stable rule identifiers and reviewed sources.

2.3 Implement branching questions and dependency invalidation. Test supported, contradictory, incomplete and unsupported scenarios, including the required security invariants.

Exit evidence: passing table-driven engine tests, documented rule coverage and questionnaire paths. **Approval gate: approve decision behavior before the visual journey.**

## Milestone 3 — Complete delegated-access vertical slice

3.1 Build accessible Design cards, navigation and evolving architecture preview for a server web app to API A to API B.

3.2 Build Recommendation with per-hop identity, audience, responsibilities and blockers. Implement answer edits and stale-result invalidation.

3.3 Build structured fresh-start guide templates, implementation stepper, completion state, copy feedback and Markdown export. Compile representative generated code and test the journey on desktop and narrow screens.

Exit evidence: usable Design → Recommendation → Implement → export journey, consistent snippets, tested invalidation and keyboard navigation. **Approval gate: approve the product experience before expanding coverage.**

## Milestone 4 — Remaining platforms, resources and tenant scenarios

4.1 Extend verified guides for Blazor server, Blazor WebAssembly, ASP.NET Core APIs, JavaScript/TypeScript browser clients and Windows desktop.

4.2 Complete background-service, CLI/device, API-validation-only and app-only downstream paths. Include credential choices, local-development strategy and production token caching.

4.3 Add category-level Graph, Azure and custom-resource guidance from the agreed coverage matrix; implement fan-out and mixed-identity presets.

4.4 Complete workforce multitenant, guest and verified cross-tenant paths, plus supported edge cases. Compile representative generated samples for the complete coverage matrix.

Exit evidence: complete guides for agreed MVP coverage, source/review metadata and passing engine/sample checks. **Approval gate: approve feature coverage before release validation.**

## Milestone 5 — Tenant verification and release readiness

5.1 Run end-to-end test-tenant checks for each supported scenario family, including available cross-tenant cases. Record positive and negative authorization outcomes and any unverified combinations.

5.2 Complete keyboard, screen-reader, responsive, contrast and reduced-motion checks. Validate export, reset, completion reopening and safe diagnostics.

5.3 Resolve release blockers and publish the final coverage, verification evidence, known limitations, setup instructions and maintenance procedure for rules/templates.

Exit evidence: requirements acceptance checklist with evidence, documented tenant verification and explicit remaining limitations. **Approval gate: user accepts the release milestone; deployment or additional scope requires its own authorization.**

## Resume protocol

Read requirements.md, this plan and progress.md first. Inspect the actual working tree before relying on recorded status. Resume the recorded next action only within the latest approved milestone. Re-run checks when intervening changes affect their validity. If a major milestone is awaiting approval, report its outcome and wait; do not begin the following milestone.


Milestone 3 UI refinement checkpoint: one-question screens, visible generic explanations, generic entity names, authentication/responsibility cards with labeled arrows, implementation-only architecture sidebar and AdvancedDetails=false are implemented. Other architecture recommendations expose a clearly limited setup outline rather than silently omitting an action. This refinement does not approve or complete milestone 4.

