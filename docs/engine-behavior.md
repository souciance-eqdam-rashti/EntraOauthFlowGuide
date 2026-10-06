# Engine behavior and review evidence

Milestone 2, checkpoints 2.1–2.3. Reviewed 2026-10-05. Rule version 1.0.0; schema 1.1.0.

## Concrete decisions

| Architecture / supplied facts | Result |
| --- | --- |
| Server Blazor with sign-in, protected certificate credential, no API calls | OIDC authorization code with PKCE; one web registration; no custom API permission setup |
| Blazor WebAssembly or JavaScript/TypeScript → custom API as user | Public browser client, authorization code with PKCE, no confidential credentials; API validates its audience/issuer and enforces developer-supplied scopes |
| Server web → API A → API B/Graph as user | Server sign-in and resource-specific client token; certificate-backed OBO from API A using an incoming user access token; distinct audiences and delegated authorization |
| Same topology, application identity explicitly chosen downstream | Separate client credentials and application authorization for that hop; user initiation does not cause OBO |
| Azure worker with available managed identity → Azure resource | App-only acquisition with managed identity; target compatibility and least-privilege assignment remain prerequisites; no custom client registration for ordinary managed identity |
| Non-Azure worker → custom API/Graph | Certificate or explicitly selected supported workload federation; app-only access and developer-supplied authorization |
| CLI with no local browser, alternate browser available and permitting policy | Device code; absence of browser or policy permission returns an explicit boundary |
| Windows desktop with browser | Public-client interactive authentication with PKCE; platform redirect integration belongs to the guide template |
| API without downstream calls | Token validation and user-scope/application-role checks; no downstream token acquisition |
| API with an external user caller → downstream | Explicit external user-token boundary, validated API audience/issuer and user-route authorization before certificate-backed OBO |
| Mixed Blazor execution | Separate server and browser components; confidential server and public browser responsibilities remain distinct |
| Fan-out or second middle tier | Independent decisions and audiences per hop; each OBO link must reference the applicable incoming user-token source |

## Status and precedence

Malformed collections/values, conflicting stacks/execution, duplicate/reserved identifiers, missing references, cycles and incompatible supplied identities return Invalid with corrective question IDs. Known unsupported domains, specialized requirements and over-limit architectures return Unsupported. Required architectural unknowns return NeedsClarification. Only a coherent architecture returns Ready with a plan.

These phases run explicitly in code. Within successful plan assembly, trace output is ordered by phase, descending declared priority and stable identifier; code declaration order does not pick a winner between conflicting supplied facts. Each relationship receives exactly one explicit acting identity/acquisition decision. The engine has no runtime plugin rule interpreter, network call or clock dependency.

Normalization trims/canonicalizes identifiers, preserves display names and orders entities ordinally. Plan IDs hash the rule version and normalized architecture; repeat evaluations and reordered equivalent entities have byte-identical serialized plans. All derived facts are reset before reevaluation, including supplied stale security decisions marked Derived.

## Category-level authorization

Resource questions stop at Graph, Azure or custom resources. Scope/app-role and audience keys are stable placeholders tied to the target; no broad Graph permission or generic Azure Contributor role is inserted. Azure authorization uses an explicit provider-specific authorization mode rather than claiming every service uses Azure RBAC.

Ready means the architecture is complete, not that consent, policy, resource compatibility or tenant access is verified. Plans visibly carry:

- Target identity/credential compatibility and operation-appropriate authorization selection.
- Permission-definition and user/admin consent policy, with consent actor separate from registration ownership.
- Azure target-specific role/permission mechanism and assignment responsibilities.
- Distinct audiences, managed identity assignment and separate local-development strategy.
- OBO claims-challenge/consent handling through the originating client.
- Multitenant issuer restrictions, resource-tenant service principals/consent, and guest membership assumptions.

Cross-tenant topology presets require the global workforce multitenant model. Per-component mixed single/multitenant registration configuration is outside this bounded MVP. Known caller/resource tenant references must agree with the selected boundary; exact tenant IDs may remain deployment placeholders.

Ordinary managed identity cross-tenant access, managed-identity OBO and federation-backed OBO are explicit MVP boundaries. These are coverage limits, not claims that every specialized federation configuration is impossible. Certificate OBO is the implemented decision path. Federation app-only decisions carry an integration/trust prerequisite; exact code is reviewed during template implementation.

## Reviewed sources and test coverage

RuleCatalog.cs is the source of truth for stable rule IDs, phases, priorities, rationales, official Microsoft URLs and individual reviewed dates (2026-10-05). Core sources include [authorization code](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-auth-code-flow), [OBO](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-on-behalf-of-flow), [client credentials](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-client-creds-grant-flow), [device authorization](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-device-code), [token validation](https://learn.microsoft.com/en-us/entra/identity-platform/access-tokens), [multitenant configuration](https://learn.microsoft.com/en-us/entra/identity-platform/howto-convert-app-to-be-multi-tenant), [managed identities](https://learn.microsoft.com/en-us/entra/identity/managed-identities-azure-resources/overview) and [consent](https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/user-admin-consent-overview).

The final milestone build has zero warnings/errors; 71 automated tests pass. Tests cover every supported platform family and editable preset, delegated/app-only chains and fan-out, browser credential/PKCE safeguards, explicit unknowns, unsupported identity environments, resource-mode incompatibility, topology integrity, external API caller boundaries, issuer/tenant-model constraints, deterministic plans, reviewed rule metadata, answer dependency invalidation and checklist preservation/reset.

These are model and contract tests. They do not validate an actual token, grant consent, prove Azure service compatibility or verify a tenant. Invalid-audience, missing-scope/role, denied-consent and 401/403 runtime tests belong to generated sample/tenant validation in milestones 3–5. The visual questionnaire, generated code and end-to-end tenant checks are not implemented or verified at this milestone.
