using System.Collections.Immutable;
using System.Text.Json;
using EntraAdvisor.Engine;
using EntraAdvisor.Engine.Contracts;
using EntraAdvisor.Guide.Contracts;

namespace EntraAdvisor.Guide;

/// <summary>Configuration checklist for any ready plan; specialized runnable samples are separate templates.</summary>
public sealed class ArchitectureGuideGenerator : IGuideGenerator
{
    public const string TemplateVersion = "setup-1.0.0";
    private const string Learn = "https://learn.microsoft.com/en-us/entra/identity-platform/";
    private static DocumentationSource Source(string title, string url) => new(title, new Uri(url), new(2026, 10, 5));
    public static DocumentationSource FlowSource(TokenAcquisition flow) => Source(flow switch
    {
        TokenAcquisition.OnBehalfOf => "On-behalf-of flow",
        TokenAcquisition.ClientCredentials => "Client credentials flow",
        TokenAcquisition.DeviceCode => "Device code flow",
        _ => "Authorization code flow with PKCE"
    }, Learn + (flow switch
    {
        TokenAcquisition.OnBehalfOf => "v2-oauth2-on-behalf-of-flow",
        TokenAcquisition.ClientCredentials => "v2-oauth2-client-creds-grant-flow",
        TokenAcquisition.DeviceCode => "v2-oauth2-device-code",
        _ => "v2-oauth2-auth-code-flow"
    }));

    public ImplementationGuide Generate(OAuthPlan plan, ImplementationFacts facts)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var evaluated = new ArchitectureEvaluator().Evaluate(plan.Scenario).Plan;
        if (evaluated is null || JsonSerializer.Serialize(evaluated) != JsonSerializer.Serialize(plan))
            throw new ArgumentException("Generate from the current engine's complete validated plan.", nameof(plan));
        if (!facts.Values.IsEmpty) throw new ArgumentException("This setup guide uses explicit developer-supplied values.", nameof(facts));
        if (DelegatedChainGuideGenerator.CanGenerate(plan)) return new DelegatedChainGuideGenerator().Generate(plan, facts);
        var steps = new List<GuideStep>();
        var register = Source("Register an application", Learn + "quickstart-register-app");
        var permissions = Source("Configure API permissions", Learn + "quickstart-configure-app-access-web-apis");
        var expose = Source("Expose API scopes", Learn + "quickstart-configure-app-expose-web-apis");
        var roles = Source("Define API app roles", Learn + "howto-add-app-roles-in-apps");
        var tokens = Source("Validate access tokens", Learn + "access-tokens");
        var sources = plan.Sources.AddRange(new[] {register, permissions, expose, roles});
        void Add(string id, GuideSection section, string title, string component, string action, string expected,
            IEnumerable<GuideContent> content, IEnumerable<DocumentationSource> references, IEnumerable<string>? hops = null)
        {
            steps.Add(new() { Id = id, Section = section, Title = title, ComponentId = component, Purpose = action,
                Action = action, ExpectedResult = expected, Content = content.ToImmutableArray(), Sources = references.DistinctBy(s => s.Url).ToImmutableArray(),
                DependsOnStepIds = steps.Count == 0 ? [] : [steps[^1].Id], RelatedRelationshipIds = hops?.ToImmutableArray() ?? [] });
        }
        GuideContent Text(string text) => new InstructionContent(text);
        GuideContent Portal(string action, params string[] path) => new PortalActionContent(path.ToImmutableArray(), action);
        GuideContent Value(string key, string label) => new CopyableValueContent(new(key, label, "__" + key.Replace('.', '_').Replace('-', '_').ToUpperInvariant() + "__",
            GuideValueKind.DeveloperSupplied, "Record the actual value during setup. Replace this placeholder in your application configuration."));
        var first = plan.Scenario.Components[0].Id;
        Add("prepare", GuideSection.Prerequisites, "Prepare your Entra tenant", first,
            "Confirm access to the test tenant and administrator support for consent and assignments.",
            "You know which tenant owns each app and resource and who can authorize access.",
            [Text(TenantPreparation.Roles(plan))], [register, permissions]);
        foreach (var registration in plan.Registrations)
        {
            var component = plan.Scenario.Components.Single(c => c.Id == registration.ComponentId);
            var decision = plan.Components.Single(c => c.ComponentId == component.Id);
            var content = new List<GuideContent>();
            if (registration.CreateRegistration)
            {
                var audience = plan.Scenario.Tenants.Model.Value == WorkforceTenantModel.SingleTenant
                    ? "Accounts in this organizational directory only" : "Accounts in any organizational directory";
                content.Add(Portal($"Create a new registration named for {component.Name}. Choose {audience}. Record its Application (client) ID and Directory (tenant) ID.", "Entra admin center", "Entra ID", "App registrations", "New registration"));
                content.Add(Value(component.Id + ".clientId", component.Name + " client ID"));
                if (decision.SignIn != SignInApproach.None)
                {
                    var platform = component.Stack.Value switch {
                        ImplementationStack.BlazorServer => "Web",
                        ImplementationStack.BlazorWebAssembly or ImplementationStack.JavaScriptTypeScript => "Single-page application",
                        _ => "Mobile and desktop applications"
                    };
                    content.Add(Portal($"Add the {platform} platform. For browser-based sign-in, register the exact callback URI used by the authentication library. For device code, enable the supported public-client flow instead of adding a web callback. Keep implicit grants disabled.", "App registration", "Authentication"));
                }
                if (decision.Credential == CredentialMechanism.Certificate)
                    content.Add(Portal("Create a signing certificate in a protected key store. Upload only its public certificate; configure the application to load the private key securely. Plan certificate rotation.", "App registration", "Certificates & secrets", "Certificates"));
                if (decision.Credential == CredentialMechanism.WorkloadFederation)
                    content.Add(Portal("Create a federated credential for your actual workload provider. Match its issuer, subject and audience exactly. Configure the workload to obtain the assertion from that provider.", "App registration", "Certificates & secrets", "Federated credentials"));
            }
            else content.Add(Text("Use the host-provided managed identity for this application. Enable the identity on the Azure host and record its principal ID. Do not create an unnecessary app registration or upload a credential for managed identity."));
            Add("register-" + component.Id, GuideSection.ClientRegistration, "Set up " + component.Name + " in Entra", component.Id,
                "Create the application identity and configure the platform or host identity.", "The component has its own identity and the required sign-in or credential configuration.", content, [register, Source("Platform registration guidance", PlatformUrl(component.Stack.Value))]);
        }
        foreach (var validation in plan.ApiValidation)
        {
            var name = plan.Scenario.Components.Single(c => c.Id == validation.ComponentId).Name;
            var content = new List<GuideContent> { Value(validation.AudienceValueKey, "API audience"), Text("Record the API's accepted audience and issuer. For a custom API using v2 access tokens, set api.requestedAccessTokenVersion to 2 in its registration manifest, preserving all existing fields.") };
            if (validation.Authorization.Any(a => a.AcceptedIdentity == ActingIdentity.DelegatedUser))
                content.Add(Portal("Set an Application ID URI and add the delegated scopes your API will enforce. Choose scope values and consent descriptions for the actual operations; enable each scope.", "API app registration", "Expose an API"));
            if (validation.Authorization.Any(a => a.AcceptedIdentity == ActingIdentity.Application))
                content.Add(Portal("Define least-privilege app roles with Applications as an allowed member type. Use these roles to authorize application callers; an access token alone is not sufficient authorization.", "API app registration", "App roles"));
            Add("expose-" + validation.ComponentId, GuideSection.ResourceRegistration, "Define access for " + name, validation.ComponentId,
                "Define the scopes or roles this API accepts.", "The API registration exposes the authorization values its callers need.", content, [expose, roles]);
        }
        foreach (var hop in plan.Relationships)
        {
            var relationship = plan.Scenario.Relationships.Single(r => r.Id == hop.RelationshipId);
            var resource = plan.Scenario.Resources.Single(r => r.Id == relationship.TargetResourceId);
            var caller = plan.Scenario.Components.Single(c => c.Id == relationship.CallerComponentId).Name;
            var content = new List<GuideContent> { Value(hop.AudienceValueKey, "Target resource audience"), Value(hop.Authorization.DeveloperValueKey, "Required permissions or roles"), Text(hop.Authorization.SelectionGuidance) };
            if (resource.Category.Value == ResourceCategory.AzureResource)
                content.Add(Text("Use the resource's documented Entra audience and supported authorization method. Where Azure RBAC is required, assign the least-privilege data-access role to the calling principal at the appropriate resource scope. A management-plane role may not grant data access. Verify resource-specific delegated/application support; do not substitute an invented universal Azure permission."));
            else if (hop.Credential == CredentialMechanism.ManagedIdentity)
                content.Add(Text("Grant the required application roles to the managed identity's service principal for the target API or Microsoft Graph, using an authorized administrator and the provider's supported assignment procedure. Managed identity has no caller app registration on which to add API permissions. Confirm the assignment in the resource tenant."));
            else
                content.Add(Portal($"On {caller}'s registration, add a permission for {(resource.Category.Value == ResourceCategory.MicrosoftGraph ? "Microsoft Graph" : resource.Name)}. Select {(hop.Identity == ActingIdentity.DelegatedUser ? "Delegated permissions and the scopes defined on the target" : "Application permissions and the target's app roles")}. Obtain user or administrator consent as required by the permission and tenant policy. For managed identity, grant application roles to its service principal instead of configuring API permissions on an app registration.", "Caller app registration", "API permissions", "Add a permission"));
            if (relationship.TenantBoundary.Value == TenantBoundary.CrossTenant)
                content.Add(Text("Provision the caller's service principal and required consent or assignments in the resource tenant. Verify that the resource and chosen credential support this boundary. Acquire the token in the required resource-tenant context; do not assume home-tenant consent grants access elsewhere."));
            Add("permission-" + hop.RelationshipId, GuideSection.PermissionsAndConsent, "Allow " + caller + " to call " + resource.Name,
                relationship.CallerComponentId, "Grant access for this caller and target only.", "The chosen identity has the required consent or resource assignment.", content,
                [permissions, FlowSource(hop.Acquisition), .. (resource.Category.Value == ResourceCategory.AzureResource ? new[] { Source("Assign Azure roles", "https://learn.microsoft.com/en-us/azure/role-based-access-control/role-assignments-portal") } : Array.Empty<DocumentationSource>())], [hop.RelationshipId]);
        }
        foreach (var component in plan.Scenario.Components)
        {
            var decision = plan.Components.Single(c => c.ComponentId == component.Id);
            var outbound = plan.Relationships.Where(h => plan.Scenario.Relationships.Single(r => r.Id == h.RelationshipId).CallerComponentId == component.Id).ToArray();
            var validation = plan.ApiValidation.FirstOrDefault(v => v.ComponentId == component.Id);
            var content = new List<GuideContent> { Text("Use the supported authentication library for this platform. Configure the tenant authority and component client ID from the new registration, or the selected host identity. Do not put confidential credentials into browser or desktop code.") };
            if (decision.SignIn != SignInApproach.None) content.Add(Text("Configure the sign-in method selected for this component. Match the registered redirect URI to the library's callback. Handle canceled sign-in, consent and interaction-required responses through the library."));
            if (plan.Scenario.Tenants.Model.Value == WorkforceTenantModel.Multitenant)
                content.Add(Text("For multi-tenant sign-in, use the organizational authority supported by the library and validate each issuer against its tenant. Define which organizations are allowed, onboard them with consent, and enforce that policy. Do not disable issuer validation."));
            if (plan.Scenario.Tenants.IncludesGuestUsers.Value)
                content.Add(Text("For guest access, sign the guest in using the applicable resource tenant. Test an invited guest's consent, assignment and API authorization separately from a member user."));
            if (validation is not null)
                content.Add(Text("Configure bearer-token authentication using the API's own audience and trusted tenant issuer metadata. Validate signature, issuer, audience and expiry. Enforce the required scopes for user tokens and app roles or explicit application access policy for application tokens. Reject unauthorized calls before processing them."));
            foreach (var hop in outbound)
                content.Add(Text(hop.Acquisition switch {
                    TokenAcquisition.OnBehalfOf => "Use on-behalf-of token acquisition with the incoming access token issued for this API and the API's protected credential. Request the downstream delegated scopes; never forward the incoming token to another audience. Return interaction-required challenges to the initiating client.",
                    TokenAcquisition.ClientCredentials => "Acquire a token as the application using the configured certificate, federated assertion or managed identity. With client credentials, request the target resource's /.default scope and rely on its granted application permissions or resource roles.",
                    TokenAcquisition.DeviceCode => "Use the library's device-code token acquisition. Display its code and sign-in instructions and handle cancellation, expiry and tenant policy restrictions.",
                    _ => "Acquire a token for the target's delegated scopes through the authentication library using authorization code with PKCE. Use silent acquisition where possible and initiate interaction when required."
                }));
            if (outbound.Length > 0) content.Add(Text("Attach the target access token to the Authorization: Bearer header on calls to that resource. Keep separate tokens for different audiences. Use the library's token cache; for multiple server instances use a protected shared cache with a documented refresh and credential-rotation strategy."));
            Add("configure-" + component.Id, GuideSection.Configuration, "Configure " + component.Name, component.Id,
                "Implement sign-in, token acquisition and API authorization where required for this component.", "The component uses its own identity and the correct token and authorization settings.", content,
                [Source("Platform implementation guidance", PlatformUrl(component.Stack.Value)), .. outbound.Select(h => FlowSource(h.Acquisition)), .. (validation is null ? Array.Empty<DocumentationSource>() : new[] { tokens }), .. (decision.SignIn == SignInApproach.None ? Array.Empty<DocumentationSource>() : new[] { FlowSource(decision.SignIn == SignInApproach.DeviceCode ? TokenAcquisition.DeviceCode : TokenAcquisition.AuthorizationCode) })], outbound.Select(h => h.RelationshipId));
        }
        Add("verify", GuideSection.TestAndTroubleshoot, "Verify the complete flow", first,
            "Run positive and negative access checks in your test tenant.", "Allowed calls succeed; missing, invalid and unauthorized tokens are rejected.",
            [Text("Test each connection separately, then the complete chain. Confirm the intended user or application identity and target audience. Test a missing token, a token for another API, and an identity without the required permission. APIs should reject invalid authentication and denied authorization appropriately."),
             Text("For multi-tenant or guest access, test the additional organizations or guests explicitly. Record consent and assignments in each applicable tenant. Export this guide before leaving the browser session.")], [tokens, permissions], plan.Relationships.Select(h => h.RelationshipId));
        return new() { PlanId = plan.Id, Architecture = plan, Versions = new(plan.Versions.Schema, plan.Versions.Rules, TemplateVersion),
            Steps = steps.ToImmutableArray(), Assumptions = plan.Assumptions.Add("Configuration checklist; complete runnable samples for additional platform families remain in progress."),
            Sources = sources.AddRange(steps.SelectMany(s => s.Sources)).DistinctBy(s => s.Url).ToImmutableArray() };
    }

    private static string PlatformUrl(ImplementationStack stack) => stack switch {
        ImplementationStack.BlazorServer => "https://learn.microsoft.com/en-us/aspnet/core/blazor/security/blazor-web-app-with-entra?view=aspnetcore-10.0",
        ImplementationStack.BlazorWebAssembly => "https://learn.microsoft.com/en-us/aspnet/core/blazor/security/webassembly/standalone-with-microsoft-entra-id?view=aspnetcore-10.0",
        ImplementationStack.AspNetCoreApi => Learn + "scenario-protected-web-api-app-configuration",
        ImplementationStack.JavaScriptTypeScript => Learn + "scenario-spa-app-configuration",
        ImplementationStack.WindowsWpf or ImplementationStack.DotNetConsole => Learn + "scenario-desktop-app-configuration",
        _ => Learn + "scenario-daemon-app-configuration"
    };
}
