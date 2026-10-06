# MVP coverage matrix

Checkpoint 1.1 — 2026-10-05. This defines intended release coverage, not implemented or tenant-verified behavior.

## Resource boundary

Users select Microsoft Graph, Azure resources or custom resources. No service/operation catalog is maintained. The advisor selects an authentication approach; the developer chooses exact Graph permissions, Azure roles or custom scopes/app roles during implementation.

Every guide exposes resource identity-mode compatibility, tenant policy and assignment requirements as prerequisites. An architecture recommendation may be Ready with these explicit prerequisites and developer-supplied deployment placeholders. Unknown architectural identity requirements cannot be Ready. Known incompatibilities return Unsupported. No guide claims every Azure service supports every identity mode or cross-tenant configuration.

Custom resources in scope are Entra-protected APIs. Other third-party authentication, consumer identities, External ID customer tenants, sovereign clouds, IWA, ROPC, implicit and specialized agent identities receive explicit boundaries.

## Platforms and implementation families

| Platform | Intended guide | Boundaries |
| --- | --- | --- |
| Blazor server / ASP.NET Core server web | OIDC sign-in; delegated or application downstream access with Microsoft.Identity.Web where supported | Server protects credentials; mixed rendering classified per execution boundary |
| Blazor WebAssembly | Browser public-client authorization code with PKCE; ASP.NET Core API validation | Never client secrets in browser; verify selected browser stack |
| JavaScript/TypeScript browser SPA | MSAL browser authorization code with PKCE; ASP.NET Core API validation | Framework-neutral browser example; no Node server guide in MVP |
| ASP.NET Core API | Audience/issuer validation, scope or app-role enforcement, optional OBO or app-only downstream | Token validation without unnecessary token acquisition |
| Windows desktop | MSAL.NET interactive public client, platform redirect handling | WPF representative sample; other native/mobile platforms outside initial coverage |
| .NET background service | Managed identity when applicable; certificate or supported federation otherwise | Exact target assignments supplied by developer; no claim of universal federation support |
| .NET CLI/input-constrained device | Device code when alternate browser and policy allow; interactive where appropriate | No alternate browser or policy prohibition produces a blocker |

## Topology presets

| Preset | Components/relationships | Decisions to verify |
| --- | --- | --- |
| Sign-in only | Server web or desktop | Sign-in/session; no unnecessary API registration |
| Client to API | Server web, SPA or desktop → custom API | Public/confidential classification, delegated or intended app identity, audience and authorization |
| Delegated API chain | Client → API A → API B or provider resource | Incoming user token, per-hop OBO, independent audiences and permissions |
| Application downstream | Client → API A → downstream resource | Downstream app-only decision despite user-triggered operation |
| API only | API with an external caller boundary | Validate caller tokens; clarify incoming identity; no acquisition without downstream calls |
| Workload access | Background service → Graph, Azure or custom resource | Hosting, managed identity compatibility and credential strategy |
| CLI/device access | CLI → Graph, Azure or custom resource | User sign-in capabilities, device-code policy and target compatibility |
| Fan-out | API → up to three downstream targets | Per-target identity; mixed delegated and application access |
| Second middle tier | Client → API A → API B → downstream | OBO prerequisites independently validated at each hop |
| Mixed Blazor | Server and browser represented as distinct components → API | Public browser boundary; no credentials crossing to browser |

Limits: five application components and eight directed relationships; provider resources are separate targets and do not consume the application-component limit. Explicitly model provider targets and custom API component references. No cycles or arbitrary graph editor. Beyond-limit architectures return Unsupported with a concrete boundary, not a truncated recommendation. Multiple incoming identities for an API require explicit relationships; never infer a user from an app-only token.

## Workforce tenants

| Tenant model | Intended behavior | Verification dependency |
| --- | --- | --- |
| Single tenant | Explicit issuer/audience, registrations and assignments | Primary test tenant |
| Guest users | Resource-tenant context and guest membership/consent assumptions | Guest test identity |
| Multitenant sign-in | Tenant-aware issuer validation, service principals and consent | Second workforce tenant |
| Cross-tenant access | Distinct caller/resource boundaries; resource-specific compatibility remains prerequisite | Second tenant and suitable consent/assignment access |

Managed identity availability does not establish cross-tenant support. Known unsupported combinations receive a boundary. No automatic provisioning or tenant changes are part of the advisor.

## Release evidence

Compile representative generated samples for each implementation family. Test engine safeguards and all topology presets. Verify common resource examples in the available test tenant as evidence of the category guidance, without implying all Azure services or Graph operations were tested. Record exact tested examples, package versions, dates and remaining prerequisites during milestone 5. Authentication setup is complete; service-specific authorization values remain developer-supplied.
