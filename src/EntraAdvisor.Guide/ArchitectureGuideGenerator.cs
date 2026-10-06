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
        GuideContent Value(string key, string label, string? guidance = null) => new CopyableValueContent(new(key, label, "__" + key.Replace('.', '_').Replace('-', '_').ToUpperInvariant() + "__",
            GuideValueKind.DeveloperSupplied, guidance ?? (label.Contains("client ID") ? "From Overview, use this registration’s Application (client) ID and Directory (tenant) ID in the app configuration." : "Use the target resource’s identifier from Entra; request a separate token for each resource.")));
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
                content.Add(Portal($"Register {component.Name}; choose {audience}.", "Entra admin center", "Entra ID", "App registrations", "New registration"));
                if (decision.SignIn != SignInApproach.None)
                {
                    var platform = component.Stack.Value switch {
                        ImplementationStack.BlazorServer => "Web",
                        ImplementationStack.BlazorWebAssembly or ImplementationStack.JavaScriptTypeScript => "Single-page application",
                        _ => "Mobile and desktop applications"
                    };
                    content.Add(Portal(decision.SignIn == SignInApproach.DeviceCode ? "Enable public-client flows for device-code sign-in; no web callback is needed." : $"Add the {platform} platform with the library’s exact callback URI. Keep implicit grants disabled.", "App registration", "Authentication"));
                }
                if (decision.Credential == CredentialMechanism.Certificate)
                    content.Add(Portal("Upload the public signing certificate. Keep its private key in a protected server store and plan rotation.", "App registration", "Certificates & secrets", "Certificates"));
                if (decision.Credential == CredentialMechanism.WorkloadFederation)
                    content.Add(Portal("Add a federated credential matching the workload provider’s issuer, subject and audience; obtain assertions from that provider.", "App registration", "Certificates & secrets", "Federated credentials"));
            }
            else content.Add(Text("Use the host-provided managed identity: enable it on the Azure host and record its principal ID. No app registration or credential upload is needed."));
            Add("register-" + component.Id, GuideSection.ClientRegistration, "Register application for " + component.Name, component.Id,
                "Create the application identity and configure the platform or host identity.", "The component has its own identity and the required sign-in or credential configuration.", content, [register, Source("Platform registration guidance", PlatformUrl(component.Stack.Value))]);
        }
        foreach (var validation in plan.ApiValidation)
        {
            var name = plan.Scenario.Components.Single(c => c.Id == validation.ComponentId).Name;
            var userAccess = validation.Authorization.Any(a => a.AcceptedIdentity == ActingIdentity.DelegatedUser);
            var appAccess = validation.Authorization.Any(a => a.AcceptedIdentity == ActingIdentity.Application);
            var choice = userAccess && appAccess
                ? "Use both: scopes limit calls made for a signed-in user; app roles permit calls made by an application without a user. This API accepts both identities."
                : userAccess
                    ? "Use scopes: callers access this API on behalf of the signed-in user. Define scopes to control which operations they can perform. Application permissions are not needed for this scenario."
                    : "Use app roles: they describe what an application may do without a signed-in user. This API receives application calls, so delegated scopes are not needed for this scenario.";
            var content = new List<GuideContent> { Text(choice) };
            if (userAccess) content.Add(new ConfigurationRowsContent("Configure the API identifier and scope", ["App registrations", name, "Expose an API"], [
                new(validation.ComponentId + ".applicationIdUri", "Application ID URI", "api://<backend-client-id>", GuideValueKind.DeveloperSupplied, "Replace <backend-client-id> with this API registration’s client ID."),
                new(validation.ComponentId + ".scope", "Scope name", "Orders.Read", GuideValueKind.Sample, "Example only; no scope has been selected by this guide. Choose the operation your API will enforce, e.g. Orders.Read or Orders.Write."),
                new(validation.ComponentId + ".scopeState", "Scope state", "Enabled", GuideValueKind.Derived, "")
            ], "Add the scope and provide the consent name and description requested by Entra."));
            if (appAccess) content.Add(Portal("Add a least-privilege role such as Orders.Read.All with Applications as an allowed member type.", "API app registration", "App roles"));
            Add("expose-" + validation.ComponentId, GuideSection.ResourceRegistration, "Define access for " + name, validation.ComponentId,
                "Define the scopes or roles this API accepts.", "The API registration exposes the authorization values its callers need.", content, [expose, roles]);
        }
        foreach (var hop in plan.Relationships)
        {
            var relationship = plan.Scenario.Relationships.Single(r => r.Id == hop.RelationshipId);
            var resource = plan.Scenario.Resources.Single(r => r.Id == relationship.TargetResourceId);
            var caller = plan.Scenario.Components.Single(c => c.Id == relationship.CallerComponentId).Name;
            var content = new List<GuideContent>();
            if (resource.Category.Value == ResourceCategory.AzureResource)
                content.Add(Text("Assign the resource’s documented data-access role to the caller at the resource scope, e.g. Storage Blob Data Reader for blob reads. Use that resource’s audience and supported identity mode; a management role alone may not grant data access."));
            else if (hop.Credential == CredentialMechanism.ManagedIdentity)
                content.Add(Text("Assign the target’s application role to the managed identity’s service principal in the resource tenant. Managed identity has no caller app registration; use an authorized administrator and the provider’s assignment procedure."));
            else
                content.Add(Portal($"On {caller}, select {resource.Name} → {(hop.Identity == ActingIdentity.DelegatedUser ? "Delegated permissions: choose the target scope (for example Orders.Read for a custom API or User.Read for Graph) because this call acts for a user" : "Application permissions: choose the target app role (for example Orders.Read.All for a custom API or User.Read.All for Graph) because this call has no user")}. Choose only permissions this caller needs.", "Caller app registration", "API permissions", "Add a permission"));
            if (resource.Category.Value != ResourceCategory.AzureResource && hop.Credential != CredentialMechanism.ManagedIdentity)
                content.Add(hop.Identity == ActingIdentity.Application
                    ? Portal("Have an administrator select Grant admin consent for this tenant. Application permissions always require admin consent.", "Caller app registration", "API permissions")
                    : Portal("Check Admin consent required for the selected scopes. If Yes, or tenant policy blocks user consent, have an administrator select Grant admin consent for this tenant. Otherwise users can consent at sign-in; admin consent can also cover all users.", "Caller app registration", "API permissions"));
            if (relationship.TenantBoundary.Value == TenantBoundary.CrossTenant)
                content.Add(Text("In the resource tenant, provision the caller’s service principal and consent/assignments. Verify cross-tenant support and acquire the token in that tenant; home-tenant consent is not sufficient."));
            Add("permission-" + hop.RelationshipId, GuideSection.PermissionsAndConsent, "Allow " + caller + " to call " + resource.Name,
                relationship.CallerComponentId, "Grant access for this caller and target only.", "The chosen identity has the required consent or resource assignment.", content,
                [permissions, FlowSource(hop.Acquisition), .. (resource.Category.Value == ResourceCategory.AzureResource ? new[] { Source("Assign Azure roles", "https://learn.microsoft.com/en-us/azure/role-based-access-control/role-assignments-portal") } : Array.Empty<DocumentationSource>())], [hop.RelationshipId]);
        }
        foreach (var component in plan.Scenario.Components)
        {
            var decision = plan.Components.Single(c => c.ComponentId == component.Id);
            var outbound = plan.Relationships.Where(h => plan.Scenario.Relationships.Single(r => r.Id == h.RelationshipId).CallerComponentId == component.Id).ToArray();
            var validation = plan.ApiValidation.FirstOrDefault(v => v.ComponentId == component.Id);
            var content = new List<GuideContent>();
            if (decision.Credential != CredentialMechanism.ManagedIdentity) content.Add(Value(component.Id + ".clientId", "Configure client and tenant IDs", "From Entra → App registration → Overview, set Application (client) ID and Directory (tenant) ID in this app’s authentication configuration."));

            if (decision.SignIn != SignInApproach.None) content.Add(Text("Set the registered redirect URI in the sign-in library; handle sign-in cancellation and interaction-required responses."));
            if (plan.Scenario.Tenants.Model.Value == WorkforceTenantModel.Multitenant)
                content.Add(Text("Use an organizational authority, allow only onboarded organizations and obtain their consent. Do not disable issuer validation."));
            if (plan.Scenario.Tenants.IncludesGuestUsers.Value)
                content.Add(Text("For guest access, sign the guest in using the applicable resource tenant; verify guest consent, assignment and API access."));
            if (validation is not null)
                content.Add(Text("Validate token signature, issuer, expiry and audience using bearer middleware. For v2 tokens, audience is this API’s client ID (e.g. 11111111-2222-3333-4444-555555555555); for v1 it can be api://<api-client-id>. " + ((validation.Authorization.Any(a => a.AcceptedIdentity == ActingIdentity.DelegatedUser), validation.Authorization.Any(a => a.AcceptedIdentity == ActingIdentity.Application)) switch {
                    (true, false) => "Require the operation’s scope in scp, e.g. Orders.Read.",
                    (false, true) => "Require the operation’s app role in roles, e.g. Orders.Read.All.",
                    _ => "For user calls check scp (Orders.Read); for app calls check roles (Orders.Read.All)."
                })));
            foreach (var hop in outbound)
                content.Add(Text(hop.Acquisition switch {
                    TokenAcquisition.OnBehalfOf => "Use on-behalf-of with this API’s incoming user token and protected credential to request downstream scopes. Never forward the incoming token to another audience; return interaction-required challenges to the client.",
                    TokenAcquisition.ClientCredentials => "Acquire a token as the application using its certificate, federated assertion or managed identity. Request the target resource’s /.default, backed by granted application permissions or resource roles.",
                    TokenAcquisition.DeviceCode => "Display the library’s device-code sign-in prompt; handle cancellation, expiry and tenant policy restrictions.",
                    _ => "Use the platform’s sign-in library with authorization code + PKCE. Request the backend scope (e.g. api://<backend-client-id>/Orders.Read); acquire silently when possible."
                }));
            if (outbound.Length > 0) content.Add(Text("Send the access token as Authorization: Bearer. Use the library’s token cache."));
            Add("configure-" + component.Id, GuideSection.Configuration, "Configure " + component.Name, component.Id,
                "Implement sign-in, token acquisition and API authorization where required for this component.", "The component uses its own identity and the correct token and authorization settings.", content,
                [Source("Platform implementation guidance", PlatformUrl(component.Stack.Value)), .. outbound.Select(h => FlowSource(h.Acquisition)), .. (validation is null ? Array.Empty<DocumentationSource>() : new[] { tokens }), .. (decision.SignIn == SignInApproach.None ? Array.Empty<DocumentationSource>() : new[] { FlowSource(decision.SignIn == SignInApproach.DeviceCode ? TokenAcquisition.DeviceCode : TokenAcquisition.AuthorizationCode) })], outbound.Select(h => h.RelationshipId));
        }
        Add("verify", GuideSection.TestAndTroubleshoot, "Verify the complete flow", first,
            "Run positive and negative access checks in your test tenant.", "Allowed calls succeed; missing, invalid and unauthorized tokens are rejected.",
            [Text("Test each connection and the full chain: authorized identity succeeds; missing or wrong-audience token returns 401; insufficient permission returns 403."),
             Text("For multi-tenant or guest access, repeat tests with the intended organizations or guests and verify their consent and assignments.")], [tokens, permissions], plan.Relationships.Select(h => h.RelationshipId));
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
