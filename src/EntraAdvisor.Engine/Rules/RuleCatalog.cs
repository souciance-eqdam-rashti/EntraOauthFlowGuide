using System.Collections.Immutable;
using EntraAdvisor.Engine.Contracts;

namespace EntraAdvisor.Engine.Rules;

public static class RuleCatalog
{
    public const string SchemaVersion = "1.1.0";
    public const string RuleVersion = "1.0.0";
    private static readonly DateOnly Reviewed = new(2026, 10, 5);
    private const string IdentityPlatform = "https://learn.microsoft.com/en-us/entra/identity-platform/";
    private static DocumentationSource Source(string title, string path) => new(title, new Uri(IdentityPlatform + path), Reviewed);
    private static readonly DocumentationSource Code = Source("Authorization code and PKCE", "v2-oauth2-auth-code-flow");
    private static readonly DocumentationSource Obo = Source("On-behalf-of", "v2-oauth2-on-behalf-of-flow");
    private static readonly DocumentationSource App = Source("Application credentials", "v2-oauth2-client-creds-grant-flow");
    private static readonly DocumentationSource Tokens = Source("Access token validation", "access-tokens");
    private static readonly DocumentationSource Device = Source("Device authorization", "v2-oauth2-device-code");
    private static readonly DocumentationSource Tenants = Source("Multitenant applications", "howto-convert-app-to-be-multi-tenant");
    private static readonly DocumentationSource Consent = new("Consent and policy", new Uri("https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/user-admin-consent-overview"), Reviewed);
    private static readonly DocumentationSource Managed = new("Managed identities", new Uri("https://learn.microsoft.com/en-us/entra/identity/managed-identities-azure-resources/overview"), Reviewed);

    public static ImmutableArray<RuleDefinition> All { get; } =
    [
        new("input.integrity", RulePhase.InputValidation, 100, "References, facts and execution boundaries must be coherent.", [Code, Tokens]),
        new("boundary.mvp", RulePhase.SupportedBoundary, 100, "Apply the documented workforce/platform/topology coverage boundary.", [Tenants, Code]),
        new("facts.required", RulePhase.RequiredFacts, 100, "Unknown architectural intent needs clarification rather than a guessed identity.", [Code, Obo, App]),
        new("classification.public", RulePhase.Classification, 100, "Browser and desktop/user CLI execution cannot protect confidential credentials.", [Code, Device]),
        new("classification.execution", RulePhase.Classification, 100, "Execution location is derived from the explicitly selected implementation stack.", [Code, Tokens]),
        new("classification.confidential", RulePhase.Classification, 100, "A server client must be able to protect its credential.", [App]),
        new("signin.oidc", RulePhase.IdentityAndFlow, 80, "Server web sign-in uses OIDC authorization code with PKCE in the selected ASP.NET Core stack.", [Code]),
        new("signin.public", RulePhase.IdentityAndFlow, 80, "Interactive public clients use authorization code with PKCE through their platform library.", [Code]),
        new("signin.device", RulePhase.IdentityAndFlow, 80, "Device code requires another browser and permitting tenant policy.", [Device]),
        new("api.validation", RulePhase.IdentityAndFlow, 100, "APIs validate their own token audience and issuer and enforce authorization.", [Tokens]),
        new("hop.delegated", RulePhase.IdentityAndFlow, 80, "A signed-in client acquires a token for the target resource.", [Code]),
        new("hop.obo", RulePhase.IdentityAndFlow, 100, "A middle tier exchanges an incoming user access token for a downstream token; it never forwards a token to another audience.", [Obo, Tokens]),
        new("hop.application", RulePhase.IdentityAndFlow, 100, "Explicit app-only downstream access uses the application identity even when a user initiated the request.", [App]),
        new("credential.certificate", RulePhase.IdentityAndFlow, 80, "Confidential clients can authenticate using certificate credentials.", [App, Obo]),
        new("credential.federation", RulePhase.IdentityAndFlow, 80, "Application federation requires supported trust configuration and is not ordinary managed identity OBO.", [App]),
        new("credential.managed", RulePhase.IdentityAndFlow, 90, "Available managed identity is an application credential mechanism with target/assignment prerequisites, not a delegated grant.", [Managed, App]),
        new("authorization.category", RulePhase.Authorization, 80, "Developer-supplied least-privilege values must match the acting identity and target authorization model.", [App, Tokens, Consent]),
        new("tenant.boundaries", RulePhase.Authorization, 90, "Validate issuer/tenant boundaries; provision consent, service principals and assignments in the applicable tenants.", [Tenants, Tokens, Consent]),
        new("consent.policy", RulePhase.Authorization, 90, "Consent depends on the permission definition and tenant policy, not only delegated/application classification.", [Consent]),
        new("registration.fresh", RulePhase.PlanAssembly, 80, "Create required custom registrations; do not create replacements for provider-owned resources.", [Code, App, Managed]),
        new("plan.assembly", RulePhase.PlanAssembly, 10, "Versioned deterministic plans preserve distinct sign-in, acquisition, validation and authorization responsibilities.", [Code, Obo, App, Tokens])
    ];

    public static RuleDefinition Get(string id) => All.Single(rule => rule.Id == id);
}
