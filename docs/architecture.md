# Foundation contracts and decision ordering

Current hosting and organization updated 2026-10-07. The domain contracts below originated in milestone 2; historical milestone expectations are retained as context. The visual journey and guide generators are implemented. See [hybrid architecture](hybrid-architecture.md) for current feature ownership.

## Project boundaries

AppHost optionally orchestrates Web locally. Web is standalone Blazor WebAssembly and references Engine and Guide, with no ServiceDefaults reference. Guide references Engine. Engine and Guide have no UI/Aspire dependencies. Tests reference Web, Engine and Guide. There is no advisor API or database; ASP.NET Core API examples are generated sample applications. Production deploys static browser assets.

## Facts and architecture

`Fact<T>` distinguishes Unknown, Known and NotApplicable. Supplied and Derived are separate origins; a derived value requires its rule identifier. Use State/TryGetValue before accessing Value: a default false or enum zero is never an answer. Input validation checks typed facts for semantic contradictions and unsupported combinations. Every evaluation discards stale derived facts and recomputes supported derivations from supplied inputs.

`ArchitectureScenario` contains application components, target resources, relationships and workforce tenant context. Provider resources and custom API resources are modeled separately from callers; a custom API resource references its API component. Stable entity identifiers connect questions, decisions and guide steps. No actual tokens or credentials appear in contracts.

Components capture execution, stack, sign-in capabilities, hosting and credential capabilities. Relationships capture delegated/application identity, tenant boundary, compatibility prerequisite and the incoming user-token relationship where OBO is considered. Mixed Blazor uses separate browser/server components. Sign-in is a component responsibility; token acquisition and authorization are per relationship. API validation remains a separate responsibility even with no outgoing relationships.

Authorization contracts hold developer value keys and selection guidance rather than a service-operation catalog. Unknown exact permission names are deployment placeholders. Known target incompatibility blocks the recommendation; category-level compatibility remains an explicit prerequisite. The evaluator must never turn an unknown acting identity into an app-only assumption.

## Evaluation and determinism

`EvaluationResult` factories restrict plans to Ready, preserve corrective/clarification question identifiers and require explanations for Invalid/Unsupported results. Factory creation alone does not validate a plan: ArchitectureEvaluator is the authority for semantic validation. Guide generation receives only its validated plan; milestone 3 must validate references/version compatibility at that boundary and must not change identity decisions.

Use this explicit evaluation phase order:

1. InputValidation: malformed identifiers/references, cycles, duplicate IDs and contradictory supplied facts → Invalid with corrective questions.
2. SupportedBoundary: unsupported identity domains, stacks or topology bounds → Unsupported. Establish Invalid before Unsupported if both apply.
3. RequiredFacts: unresolved architectural facts → NeedsClarification. Ask only relevant questions; deployment placeholders do not block.
4. Classification: derive public/confidential execution responsibilities from supplied facts.
5. IdentityAndFlow: select sign-in, token acquisition and credential mechanisms independently. Guardrails override convenience recommendations.
6. Authorization: select permission mode and developer value keys; attach compatibility, consent/policy and assignment prerequisites.
7. PlanAssembly: assemble only a coherent plan after all earlier phases pass. Include source links, versions and matched rule/fact trace.

Rules have stable IDs, phases, explicit priorities, rationales and individually reviewed sources. Within a phase, evaluate descending priority and stable ID for deterministic presentation. Do not silently pick a winner when rules produce incompatible decisions: report Invalid with the relevant facts and a corrective action, or a documented engine defect if valid supplied facts triggered the conflict. Rule IDs are a stable tie-breaker for output order, not security conflict resolution.

Normalize identifiers consistently, preserve display names, and order entities/decisions by stable ordinal identifiers before evaluation/export. Equivalent normalized inputs plus a rule version must yield the same plan and decision trace. Do not include current time, random IDs or network calls in evaluation. Reviewed dates belong to versioned metadata.

## Questions and invalidation

`QuestionDefinition` specifies a supplied fact, options, relevance conditions and explicit question dependencies. Relevance conditions use AND semantics. User labels describe architecture; Unknown is explicit. QuestionScheduler groups at most three related questions per screen and presents high-branching choices alone.

QuestionnaireSession applies a validated card answer, traverses dependency edges, removes stale facts and replaces the entire evaluation. `AnswerChangeImpact` reports stable IDs, the changed fact and a user explanation. GuideCompletionInvalidator resets affected steps and their dependants to Pending while preserving unrelated completion. No-op edits preserve the original session and checklist. See [questionnaire model](questionnaire-model.md) for the UI integration contract.

## Guide and checklist

`ImplementationGuide` carries the chosen plan, ordered steps, sources, assumptions and schema/rule/template versions. Each step includes component, purpose, action, expected result, dependencies and relationship IDs. Typed content supports instructions, portal breadcrumbs, copyable values, code artifacts and optional explanations. Markdown export uses the same content. Code has language and destination filename.

`ImplementationFacts` contains developer-supplied, sample and derived values. Exact permission names, tenant IDs, redirects and endpoints use labeled keys that stay consistent across snippets. Private keys, secrets and tokens must never enter persisted facts. `StepCompletion` is separate session-scoped user-reported state; it does not verify Entra state.

Current contract schema: `1.1.0`; evaluator rule version: `1.0.0`. Schema 1.0.0 inputs remain accepted but must supply the new identity-domain fact before becoming Ready. Schema 1.1.0 adds explicit identity-domain/specialized boundaries, scenario context in the plan, component sign-in credentials, Azure resource authorization placeholders and changed-fact provenance. Guide templates are implemented and versioned; inspect generator TemplateVersion constants for current values. Semantic changes require documented compatibility; never reuse old completion state across incompatible plans.
