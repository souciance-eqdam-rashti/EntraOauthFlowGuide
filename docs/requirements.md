# Entra OAuth Advisor requirements

## 1. Purpose and expectations

Build a .NET application that helps developers and architects choose an appropriate Microsoft Entra authentication approach and implement it correctly. Microsoft documentation is comprehensive but requires users to assemble flow selection, permissions, tenant configuration, libraries, code and testing themselves. This application should turn an architecture description into a clear decision and a sequential implementation guide.

The primary experience is a short visual questionnaire using selectable cards, followed by an architecture summary and an actionable setup checklist. Target users understand applications and APIs but should not need to understand OAuth terminology to answer the questions.

### User expectations

- Answer architecture questions in plain language, with at most two or three questions on a screen.
- Usually reach a recommendation after approximately five to eight questions; complex scenarios may require more.
- Understand which approach applies to each connection, why it applies and which identity reaches each API.
- Know what Entra access is needed, what to configure in each application registration and what to change in each .NET application.
- Work through the guide in order, copy relevant values and code, and track completion.
- Expand explanations when needed without reading a large document before starting.
- Edit earlier answers without restarting the entire questionnaire.

### Technical expectations

- Use ASP.NET Core and Blazor with .NET Aspire for local orchestration and observability.
- Keep decision logic in a deterministic .NET library independent of the UI, Aspire and any future agent.
- Generate guides from structured plans and maintained templates. Do not depend on an LLM to choose security architecture or invent configuration.
- Treat incomplete, contradictory and unsupported scenarios explicitly. A plausible answer is not sufficient.
- Verify technical rules and generated snippets against current official Microsoft documentation and the selected library versions before shipping them.
- Prioritize a small, complete set of supported scenarios over a claim to cover every possible Entra scenario.

This file is an implementation specification for a coding agent. Requirements marked MUST are release requirements; SHOULD indicates a preferred implementation with room for justified alternatives. Estimates of question or rule counts are planning guidance, not acceptance criteria.

## 2. MVP scope

The MVP MUST support these scenario families:

| Scenario | Required outcome |
| --- | --- |
| Server-side web application with user sign-in | OIDC sign-in using authorization code, with PKCE where supported by the selected stack; optional delegated API access |
| Browser SPA calling an API | Authorization code with PKCE using a public browser client; API token validation and scope enforcement |
| Native desktop or mobile application | Interactive public-client authentication with platform-specific redirect handling |
| CLI or input-constrained device | Device code when a user can complete authentication using another browser and tenant policy permits it |
| Background service calling an API | App-only access with appropriate application authorization; select managed identity or confidential-client credentials according to support |
| API calling a downstream API for the signed-in user | OBO for that hop, alongside the incoming API's authentication and authorization |
| API calling a downstream API as the application | Separate app-only authentication for that hop; do not infer OBO just because a user initiated the request |
| API accepting tokens without downstream calls | Token validation and authorization configuration; no unnecessary token-acquisition flow |

Include Microsoft Graph, custom Entra-protected APIs and Entra-compatible Azure resources as target categories. Third-party APIs that do not use Entra MUST receive a clear boundary explanation rather than an invented Entra configuration.

MVP tenant coverage: workforce single-tenant scenarios, workforce multitenant scenarios and guest users in a workforce tenant. Consumer identities, External ID customer tenants, sovereign clouds, specialized agent identities and unusual legacy scenarios may be identified but MUST be explicitly marked unsupported until their rules and guides are implemented and verified.

Implicit and ROPC MUST NOT be offered as normal recommended options. IWA may be recognized as a specialized requirement and routed to an explicit unsupported or separately verified path.

Not required for the MVP: conversational agents, automatic tenant changes, Microsoft Graph provisioning, storing credentials, user accounts for the advisor, a database, arbitrary topology editing, or exhaustive legacy coverage.

## 3. User experience

### 3.1 Design principles

- Simple, calm and task-focused. Use clear typography, whitespace, consistent spacing and one primary action per screen.
- Cards for choices; a diagram for architecture; concise badges for flow and permission type; a stepper for implementation; formatted blocks for code; accordions for explanations.
- Avoid presenting the generated guide as one long wall of text.
- Use plain language first, with technical names introduced in the result or optional help.
- Keep advanced settings hidden until they are relevant. Do not hide information required to make a safe decision.
- Use consistent design tokens and a Blazor component library. Microsoft Fluent UI Blazor is the preferred starting point; verify compatibility with the selected framework and accessible behavior before committing.

### 3.2 Main journey

The application MUST have three clear stages: **Design**, **Recommendation**, and **Implement**.

**Design:** Collect architecture facts through a branching questionnaire. Show a compact evolving architecture preview. Preview connections describe intent; do not label them with a definitive OAuth flow before evaluation.

**Recommendation:** Show the recommended approach, a short rationale, the annotated architecture, component responsibilities and any unresolved prerequisites. Provide “Edit answers” and “Start implementation”. Block definitive guide generation when required facts are missing or incompatible.

**Implement:** Show an ordered checklist with one active step. Keep architecture context visible on desktop and available through a compact disclosure on mobile. Highlight the component being configured.

### 3.3 Questionnaire behavior

- Each screen MUST show one to three related questions, never more than three. Prefer one question when a choice changes the remaining path substantially.
- Main choices MUST use selectable cards with an icon, a short title and one short descriptive sentence.
- Cards MUST have clear selected, hover, focus and disabled states. Selection MUST be indicated with text or an icon as well as color.
- Choosing a card MUST NOT unexpectedly navigate forward. Use a visible Continue action.
- Provide Back and preserve valid answers when navigating.
- When an answer changes, invalidate dependent answers, recommendations and affected completion states. Explain the change briefly and ask only the now-relevant questions.
- Include “Not sure” where uncertainty is meaningful; provide contextual help or a clarification question. Unknown MUST NOT silently become No.
- Use a stage indicator or named sections. Do not promise an exact number of remaining questions while branching makes that number uncertain.
- Ask for component names only when they improve the guide. Supply editable defaults such as Web App, Orders API and Inventory API.
- Support example scenarios as quick starts. Examples must remain editable.

### 3.4 Opening questions and follow-up examples

Opening screens SHOULD establish:

1. What are you building? Server web app, browser SPA, API, background service, desktop/mobile app, or CLI/device.
2. Will a user sign in?
3. What does it need to access? No API, my API, Microsoft Graph, an Azure resource, or a third-party API.

Follow-up screens establish downstream API calls, required identity per call, tenant model, hosting environment and authentication capabilities only when relevant.

“Blazor” alone is not enough information. The questionnaire MUST distinguish server execution, WebAssembly/browser execution and mixed rendering where authentication or token handling spans boundaries. Do not infer credential safety from the framework name.

Resource selection MUST stop at Microsoft Graph, Azure resources or custom resources. Do not require a service-specific resource or operation questionnaire. Derive the authentication approach from architecture facts. Exact Graph permissions, Azure roles and custom scopes/app roles are developer-supplied implementation values; explain how to select and configure them without guessing their names.

### 3.5 Recommendation presentation

The result MUST include:

- A concise recommendation card, including separate approaches for separate hops.
- A short explanation connected to the user's answers.
- An architecture diagram labeling token audiences, acting identity and relevant flow per connection.
- A compact component table showing public/confidential client status, validation responsibility, downstream access and permission type.
- Only relevant blockers or implementation cautions, each with a concrete next action.
- Optional “Why this approach?” and “What would change the recommendation?” disclosures.

Do not imply that all components share one client type or one flow. Do not require a new registration for a resource already owned by Microsoft or another provider.

### 3.6 Implementation presentation

- Use a navigable stepper/checklist with Pending, In progress, Complete and Blocked states.
- Each active step MUST show its purpose, target component, exact action, expected result and a completion control.
- Show portal paths as breadcrumbs and important values as labeled copyable fields.
- Code MUST have syntax highlighting, a language label, a destination filename and a Copy action with success feedback.
- Provide “Explain” and “Microsoft documentation” actions near relevant configuration.
- Make placeholders obvious and consistent across all steps. Distinguish sample values from values the user must supply.
- Show prerequisites and dependencies before the operation that requires them.
- Completion is user-reported in the MVP. Do not describe a checkbox as verification of actual tenant state.
- Provide a Markdown export of the complete guide, including architecture summary, assumptions and source links.

### 3.7 Accessibility and responsive behavior

MUST support keyboard selection, visible focus, accessible names, correct radio-group semantics for single-choice cards, screen-reader announcements for validation and sufficient contrast. Target WCAG 2.2 AA. Never rely only on color or icons.

On narrow screens, stack cards and move the architecture preview into a collapsible panel. Code blocks may scroll horizontally within their container; the whole page must not overflow. Respect reduced-motion preferences. Test keyboard navigation and a screen reader, not only automated accessibility checks.

## 4. Decision engine responsibilities

### 4.1 Model an architecture, not one global flow

The engine MUST model components and directed access relationships. Each relationship records its target resource, identity requirement and permission mode. A scenario can contain authorization code for sign-in, delegated access to API A and OBO or app-only access from API A to API B.

Keep these concepts separate:

- OIDC user sign-in and local session management.
- OAuth access-token acquisition for a specific resource.
- Credential mechanism, such as a certificate or supported federation.
- API authentication through token validation.
- API authorization through scopes, roles, assignments or applicable Azure RBAC.

Managed identity is a workload identity option, not a substitute for every OAuth flow. API token validation is a responsibility, not a grant type. PKCE is a protection for authorization code, not a separate grant.

### 4.2 Facts to collect or derive

The model SHOULD support approximately 18–25 categories of facts; do not force every scenario to answer all of them:

| Category | Facts |
| --- | --- |
| Application | Component type, execution location, framework/hosting model, fresh registration responsibilities |
| User | User sign-in, browser capability, alternate-device sign-in capability |
| Topology | Caller, target resource, downstream relationships |
| Identity | User delegated identity or application identity on each relationship |
| Capability | Resource category; developer confirmation of selected identity-mode compatibility |
| Client security | Ability to protect credentials; public/confidential client classification |
| Hosting | Azure/non-Azure, managed identity availability and target compatibility |
| Credentials | Supported certificate/federation options and local-development strategy |
| Tenants | Workforce tenant model, guest users, resource tenant and issuer boundaries |
| Authorization | Custom scopes/app roles, provider permissions, applicable Azure RBAC |
| Consent | Permission consent requirements and tenant policy constraints |
| Implementation | Redirect URI, endpoints, selected .NET stack and environment |

Use explicit Unknown/Not applicable states where appropriate. Record derived facts separately from supplied facts. Do not use false as the default for unanswered security questions.

### 4.3 Rule requirements

- Same normalized inputs and same rule version MUST produce the same plan.
- Rules MUST have stable identifiers, a rationale, official source links and a reviewed date.
- Document ordering, precedence and conflict handling. Do not resolve conflicting rules through incidental source-code order.
- Return a structured status: Ready, Needs clarification, Unsupported or Invalid.
- Return applicable question identifiers for missing facts and concrete explanations for blockers.
- Include the matched rule identifiers and relevant facts in an internal decision trace; expose a concise explanation to users.
- Validate known target constraints against the selected identity mode. At category level, expose resource compatibility as a developer-confirmed prerequisite. Do not assert support for every resource in a category or recommend a mode known to be incompatible.
- Provide least-privilege selection guidance and clearly labeled developer-supplied permission/role placeholders. Missing exact permission names do not block the architecture recommendation. Never silently select broad permissions.
- Treat consent requirements as dependent on permission definitions and tenant policies. Do not equate every delegated permission with user consent being available.
- Separate registration ownership/creation privileges from consent privileges and resource authorization.

### 4.4 Required decision safeguards

- A public client MUST NOT receive a client secret or confidential-client configuration.
- An incoming API token is for that API's audience. Do not forward it to a different downstream resource as the proposed solution.
- Select OBO only for supported delegated downstream access using an incoming user access token. App-only incoming tokens do not create delegated user identity.
- A user-triggered operation can still use application identity downstream if that is the explicitly intended authorization model.
- A device without a local browser does not automatically support device code: the user needs another way to complete sign-in, and policy may block the flow.
- Managed identity availability alone does not prove target support, permission assignment or cross-tenant support.
- Do not describe ordinary managed-identity app-only token acquisition as OBO. If a supported federated credential authenticates an OBO client, model and explain that credential configuration explicitly.
- User sign-in without API calls MUST NOT generate unnecessary custom API scopes or registrations.
- An external API with unsupported identity requirements MUST produce a boundary explanation.

## 5. Sequential guide generator

The generator MUST consume a validated structured plan and implementation facts. It MUST NOT independently change the engine's chosen identity model or flows. Missing deployment values may appear as clearly labeled placeholders; missing architectural facts must return clarification before a definitive guide is offered.

Generate steps in dependency order, generally configuring resource APIs before clients that reference their scopes or roles. Use this consistent outline, omitting irrelevant actions but keeping explicit applicability statements where users are likely to wonder:

| Order | Section | Required content |
| --- | --- | --- |
| 1 | Prerequisites | Tenant, components, fresh registrations to create, required access per configuration operation, consent actor, policy assumptions |
| 2 | Resource registration | Which custom APIs need registration; application ID URI; required scopes/app roles |
| 3 | Client and middle-tier registration | Platform type, account audience, redirect URIs, credential requirements |
| 4 | Permissions and consent | Who calls what, delegated/application permissions, scope/role assignments, consent and applicable Azure RBAC |
| 5 | Manifest | Exact relevant patch when required; otherwise explicitly state no manual manifest edit is required |
| 6 | .NET dependencies | Packages for each component and their purpose |
| 7 | Application configuration | Relevant appsettings sections, safe credential references, endpoints and matching identifiers |
| 8 | Authentication and authorization code | Startup configuration, middleware, token validation, scope/role enforcement |
| 9 | API calls and token handling | Appropriate acquisition method, target audience, caching and downstream error handling |
| 10 | Test and troubleshoot | Positive and negative checks, expected claims, common failures and next actions |

Additional guide requirements:

- Provide an operation-specific access matrix: creating a registration, editing an owned registration, granting consent and assigning resource permissions are distinct operations. Do not claim one Entra role can perform everything.
- Explain application registrations and enterprise applications/service principals when their difference affects an action.
- Manifest instructions MUST use verified current property names and minimal patches. Prefer supported portal actions where sufficient; do not generate a complete replacement manifest.
- Recommend Microsoft.Identity.Web for supported ASP.NET Core scenarios, MSAL.NET for appropriate native/console scenarios, the browser authentication stack for SPA execution, and Azure.Identity for supported Azure workload access. Confirm exact integration and versions during implementation.
- Never require MSAL.NET directly merely because Microsoft.Identity.Web uses it internally.
- Use certificate or supported federation guidance for confidential production clients. Keep development and production credential instructions distinct and never embed real credentials in generated files.
- Explain token caching appropriate to the selected hosting model. Mark in-memory examples as local/single-instance where applicable; provide production guidance for multi-instance deployments.
- Match startup, appsettings, scope names, audiences and registration identifiers across all snippets. Include required authorization beyond a generic authenticated-user check.
- Handle interactive consent/Conditional Access challenges where supported rather than assuming every downstream token acquisition succeeds silently.
- Test token validity and authorization server-side. Decoding claims is diagnostic only; do not ask users to paste tokens into third-party websites.
- Troubleshooting MUST distinguish 401 authentication failures from 403 authorization failures.
- Each guide records plan/rule/template versions, assumptions and documentation sources so an export can be understood later.

## 6. .NET and Aspire architecture

Start with these projects:

```text
EntraAdvisor.AppHost
EntraAdvisor.ServiceDefaults
EntraAdvisor.Web
EntraAdvisor.Engine
EntraAdvisor.Guide
EntraAdvisor.Tests
```

AppHost orchestrates the web application. ServiceDefaults provides appropriate telemetry, health checks and common hosting behavior. Engine and Guide remain ordinary .NET libraries without Aspire or UI dependencies. Blazor can call them directly in the MVP; a separate API is optional when external consumers justify it.

Prefer server-interactive Blazor for the advisor itself as a starting implementation choice. This does not restrict the types of target applications the advisor can describe. Select a supported .NET/Aspire version at implementation time and document the choice.

Core contracts SHOULD separate:

- `ArchitectureScenario`: components, relationships, supplied facts and unresolved facts.
- `QuestionDefinition`: stable identifier, prompt, options, relevance and dependencies.
- `EvaluationResult`: status, next questions, validation errors and optional plan.
- `OAuthPlan`: identity/flow/permission decisions per relationship, registration responsibilities and decision trace.
- `ImplementationGuide`: ordered typed steps, copyable values, code artifacts, sources and assumptions.

Guide steps SHOULD be structured data rendered by reusable components, not opaque Markdown injected into the UI. Export renders the same data into Markdown to prevent divergence.

Use no database initially. Session-scoped state is sufficient; preserve navigation state during the active session. If browser persistence is added, store scenario facts and checklist state only, never tokens or credentials. Support a visible reset action.

Do not log credentials, access tokens or unnecessary personal information. Record rule version, evaluation status and diagnostic errors safely. Advisor hosting authentication is a separate deployment concern from authentication recommendations generated for target applications.

## 7. Validation and acceptance criteria

### UX acceptance

- No questionnaire screen has more than three questions.
- Main scenario choices use accessible selectable cards.
- The normal supported path can be completed without OAuth knowledge.
- Back preserves valid input; changed answers remove stale dependent results.
- Architecture remains understandable on desktop and mobile.
- Users can identify the recommended identity per connection and locate the next implementation action without reading the whole guide.
- Copy actions work and announce success; code labels identify the correct component and file.
- Users can complete and reopen steps, and export a coherent guide.
- Keyboard-only completion and screen-reader checks pass.

### Engine acceptance

Create table-driven tests covering at least:

1. Server web app sign-in without API access.
2. SPA to custom API with no browser secret.
3. Web app to API A to Graph with delegated identity and OBO on the downstream hop.
4. The same topology with explicit app-only downstream access.
5. Azure worker to a supported Azure resource using managed identity and appropriate assignment.
6. Non-Azure worker to a custom API using confidential-client credentials and application authorization.
7. Device-code scenario with alternate-browser availability; blocked/unsupported policy variant.
8. Incoming API token validation without downstream acquisition.
9. Server Blazor versus browser WebAssembly classification.
10. Multitenant and guest-user paths with explicit tenant assumptions.
11. Unknown user-identity requirement returns clarification rather than a guessed flow.
12. Unsupported third-party or specialized identity scenarios return an explicit boundary.
13. Contradictory answers return Invalid with a corrective question.
14. Changes to an upstream answer invalidate affected guide completion state.

Also test invalid audiences, missing scopes/roles, denied consent and unsupported identity modes. Use property/invariant tests where helpful: public clients never receive secrets, incomplete scenarios never receive Ready, and every supported relationship has explicit identity and authorization decisions.

### Guide acceptance

- Every MVP scenario has a complete reviewed guide, including prerequisites and testing.
- Source links and reviewed dates exist for rules and templates.
- Manifest applicability is explicit.
- Role/consent guidance is operation-specific.
- Code examples compile against the documented package versions in representative sample projects.
- Generated snippets share consistent identifiers, endpoints and scope/role values.
- Perform an end-to-end check in a test tenant for each supported scenario family where feasible. Label anything not tenant-verified honestly; do not imply verification from unit tests alone.

## 8. Instructions for the coding agent

Implement in this order:

1. Define the supported scenario matrix, typed facts and engine result statuses.
2. Build and test deterministic decisions per relationship.
3. Define branching questions and dependency invalidation.
4. Build a polished vertical slice: server web app to API A to API B with delegated downstream access, from cards through export.
5. Add verified guide templates and extend to the remaining MVP scenarios.
6. Validate accessibility, responsive layout, generated code and tenant behavior.

Do not start by building a generic chat UI, a universal rules framework or a large dashboard. Keep the code easy to inspect and rules easy to test. Approximately 30–40 rules may be an initial estimate, but correctness and explicit coverage govern the actual count.

Keep UI labels brief. Put explanations behind optional disclosures. Ask users about architecture and resource categories; derive protocol details and leave exact service permissions/roles developer-supplied. Do not silently broaden permissions, invent API support, assume admin access or conflate credentials with authorization.

Future conversational input may extract candidate facts and ask clarifying questions, but MUST invoke the same deterministic engine. Any future automated tenant changes require a separately designed reviewable change plan and authorization flow.

## 9. Confirmed scope and delivery checkpoints

The following decisions were confirmed on 2026-10-05 and refine the earlier sections.

### 9.1 Resource categories and target platforms

The questionnaire MUST organize target resources into three primary categories: Microsoft Graph, Azure resources and custom resources. Third-party resources outside Entra remain subject to the explicit unsupported boundary described above. These categories organize the experience; they do not establish permission or identity-mode support on their own.

Resource selection MUST remain at these three categories; the MVP MUST NOT maintain a service/operation catalog or require questions about specific Azure services or Graph operations. Guides provide complete authentication setup with developer-supplied Graph permissions, Azure role/assignment details, or custom scopes/app roles. Explain how to select least privilege and verify the chosen resource supports the acting identity. Category-level recommendations are conditional on resource compatibility and tenant policy; record these prerequisites visibly without claiming resource-specific verification. Missing exact authorization values are implementation placeholders, not missing architectural facts.

Complete implementation guides MUST cover Blazor server-side execution, Blazor WebAssembly, ASP.NET Core APIs, JavaScript/TypeScript browser applications and Windows desktop applications. Background-service and CLI/device scenarios remain in scope as previously required. Other native/mobile platforms are outside the initial verified guide coverage.

### 9.2 Architecture and tenant coverage

The initial bounded topology model MUST support up to five application components and eight directed access relationships, plus provider-owned target resources. Provider-owned resources do not count toward the application-component limit. The questionnaire uses editable topology presets rather than an arbitrary graph editor.

Required presets cover sign-in only, client to API, client to API to downstream resource, API-only validation, background/CLI access, and API fan-out to multiple downstream resources. Edge cases include mixed delegated and app-only downstream access, a second middle-tier API where verified, and mixed Blazor execution boundaries. Each hop is evaluated independently. Cyclic call graphs, larger architectures and unsupported combinations receive an explicit boundary explanation.

Workforce multitenant coverage MUST include cross-tenant relationships, alongside single-tenant and guest-user scenarios. Record client/home tenant, resource tenant, issuer validation, service-principal provisioning, consent and assignment responsibilities when relevant. Cross-tenant support MUST be verified for the selected identity mechanism and resource; multitenant coverage MUST NOT imply every managed-identity or provider-resource combination is supported.

### 9.3 Fresh-start guides and tenant verification

Guides MUST assume a fresh start with no existing custom application registrations. They MUST include creation and configuration of all required custom registrations and service principals. Provider-owned resources retain their existing registrations. Existing-registration migration or reconciliation is outside MVP scope; users may edit generated placeholders for their deployment values.

A test tenant with Entra access will be available for end-to-end verification. Document required access and test prerequisites before using it. Cross-tenant tests require a second tenant or suitable external-tenant access; record this dependency explicitly. Record actual tenant verification separately from compilation and automated tests.

### 9.4 Checkpoint and approval protocol

Implementation MUST follow [the implementation plan](implementation-plan.md), divided into major milestones and smaller checkpoints. After every minor checkpoint, update [the progress record](progress.md) and any affected technical documentation before beginning the next checkpoint.

Each checkpoint record MUST include completed work, affected files, validation performed and results, decisions and assumptions, blockers, and an exact next action. Preserve sufficient context for another session to resume without relying on chat history. Record rule/template review and tenant verification status where relevant. Do not store tokens, credentials or sensitive tenant data in these records.

At each major milestone, finish its checks and documentation, report the concrete outcome and remaining limitations to the user, and stop implementation until the user explicitly approves continuing. Minor checkpoints do not require approval. Record each major approval and its scope in the progress record. A missing reply is not approval.

## 10. Official references for implementation verification

These are starting references, not a substitute for verifying the exact stack and scenario at implementation time:

- [MSAL authentication flows](https://learn.microsoft.com/en-us/entra/msal/msal-authentication-flows)
- [Authorization code flow](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-auth-code-flow)
- [On-Behalf-Of flow](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-on-behalf-of-flow)
- [Client credentials flow](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-client-creds-grant-flow)
- [Microsoft.Identity.Web overview](https://learn.microsoft.com/en-us/entra/msidweb/overview)
- [Grant tenant-wide admin consent](https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/grant-admin-consent)
- [User and admin consent](https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/user-admin-consent-overview)

Product requirement baseline: 2026-10-05. Technical review dates must be recorded individually when rules and templates are implemented.

## Confirmed journey refinements (2026-10-05)

Present one question per page, sized responsively without requiring browser zoom. Each question explains its purpose with a short generic example. Use generic application and resource names rather than invented business scenarios. Show the architecture sidebar only during implementation. Recommendations show entity cards for Entra, applications and resources, connected by labeled authentication and authorization steps, with setup responsibilities clearly distinguished. The supplied sequence diagram is a conceptual reference, not a required visual style. Hide Edit answers and Why these decisions behind a feature flag defaulting to false. Provide a clear implementation action and explain any detailed-guide coverage limitation.

### Additional simplification (2026-10-05)

Remove Not sure from all questionnaire answers. The guide assumes Enterprise Entra workforce identities, so do not ask users to select the identity environment. Continue asking about single/multiple organizations, guests and cross-tenant access where relevant.
- Question help must be always visible under a simple heading, with content specific to the current question explaining its meaning and why the answer is needed.
- Recommendation shows the plain-language authentication cards without duplicate registration or prerequisite sections. Move technical flow names, official Microsoft Learn links and setup checks into Implementation. Every ready recommendation must allow entering Implementation and generating its current setup steps.
- Recommendation and Implementation must fit the browser width and allow normal page scrolling for longer content, without requiring zoom changes.

## Authentication validation exports (2026-10-06)

Implementation offers cURL and PowerShell validation exports for the selected connection. Scripts use developer-supplied tenant/client IDs, scopes, credentials and a read-only resource URL; the advisor does not collect secrets or tokens. Export the selected flow without silently replacing user access with application access. Include token acquisition where supported, a resource GET and a missing-token check. Explain interactive sign-in, incoming-token and host-identity dependencies. Preserve a visible copy/save fallback.
