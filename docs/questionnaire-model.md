# Branching questionnaire model

Checkpoint 2.3 — 2026-10-05. This is the UI-independent model; milestone 3 renders it in Blazor.

## Starting and scheduling

Use TopologyPresets.All/Create for editable quick starts, then QuestionnaireSession.Create. Presets cover sign-in only, client/API, API chain, API-only validation, worker, CLI/device, Windows desktop, fan-out, second middle tier and mixed Blazor. They supply architecture facts, not permission names, deployment values or assumed admin privileges.

QuestionCatalog.ForScenario returns stable definitions with card titles/descriptions, help, relevant facts and dependencies. QuestionScheduler.Next schedules applicable questions requested by the evaluator. Screens contain exactly one question, with a visible generic example explaining why the answer matters. Named sections do not promise an exact remaining count. Unsupported results expose the boundary and next action rather than an endless questionnaire.

Fact states are read explicitly. Not sure remains Unknown and blocks a definitive recommendation where architecture information is required. Structurally derived facts remain separate (for example, browser execution from Blazor WebAssembly). No question asks for an individual Azure service or Graph operation.

## Applying answers and editing

Call session.Apply with one listed option. Invalid option IDs, multi-choice payloads for a single-choice question and irrelevant answers are rejected before changing state. Use the returned session; the prior immutable session is unchanged. Navigation can render earlier definitions/answers without mutation; only an applied changed answer invalidates state.

Dependency closure removes dependent answers, resets derived facts and reevaluates the architecture. The new Evaluation replaces any stale plan. Unaffected supplied answers are preserved. Exact no-op edits preserve the same session and checklist. Resource category edits add a custom API component when needed or remove the obsolete custom API/downstream branch when changing to a provider target; provider resources never acquire custom API registrations. Replacing a topology invalidates the prior architecture's completion.

API downstream user calls identify a modeled incoming relationship or the explicit `$external-user` boundary. That marker is a model value, not a token. External user context must be accepted and validated by the API; accepting both user/application callers does not authorize application callers to enter a user-only OBO route.

## Checklist integration

After a changed answer, pass AnswerChangeImpact, the prior guide and user-reported completion to GuideCompletionInvalidator.Apply. The impact identifies the changed fact and affected connections; guide steps identify their component, related connections and prerequisite step IDs.

Reset directly affected steps and their transitive dependent steps to Pending, clearing stale blocker text. Preserve completion on unrelated branches. Architecture/tenant changes invalidate the entire guide. The visual implementation stepper must use this output and the new plan; no completion checkbox verifies actual Entra configuration.

## Tested boundaries

Every editable preset reaches Ready using relevant answers with exactly one question per screen. Tests cover unanswered facts and rejection of removed Not sure answers, browser/device branch changes, earlier-answer edits, stale-plan removal, unchanged-answer preservation, category changes, dependent checklist resets and unrelated completion preservation.

The state is session-scoped and in-memory. Browser persistence and importing arbitrary scenarios are not implemented. Full keyboard/screen-reader behavior, Copy actions, architecture rendering and navigation presentation require milestone 3/5 validation.


The questionnaire assumes Enterprise Entra workforce identities and supplies that environment when unset. It does not ask an identity-environment question. Explicit non-workforce inputs remain subject to the engine boundary checks. Answer cards have no Not sure option; unanswered required facts remain unknown until the user chooses an answer.

- Each question now has an always-visible Help with this question section. Its content is specific to the architecture fact and complements the short example above the answer cards.
