using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EntraAdvisor.Engine.Contracts;
using EntraAdvisor.Engine.Rules;

namespace EntraAdvisor.Engine;

/// <summary>Explicit, versioned decisions. No UI, network access, clock or incidental rule ordering.</summary>
public sealed class ArchitectureEvaluator : IArchitectureEvaluator
{
    public EvaluationResult Evaluate(ArchitectureScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var shape = ScenarioValidation.Shape(scenario);
        if (!shape.IsEmpty) return Invalid(shape);
        var normalized = ScenarioNormalizer.Normalize(scenario);
        var integrity = ScenarioValidation.Integrity(normalized);
        if (!integrity.IsEmpty) return Invalid(integrity);
        var unsupported = Boundaries(normalized);
        if (!unsupported.IsEmpty) return EvaluationResult.Unsupported(unsupported);
        var missing = RequiredFacts(normalized);
        if (!missing.IsEmpty)
            return EvaluationResult.NeedsClarification(missing.Select(issue => QuestionId(issue.Fact!)).Distinct().ToImmutableArray(), missing);
        return EvaluationResult.Ready(Assemble(normalized));
    }

    public static string QuestionId(FactReference fact) => $"{fact.EntityId}.{fact.FactName.ToLowerInvariant()}";
    private static EvaluationResult Invalid(ImmutableArray<DecisionIssue> issues) => EvaluationResult.Invalid(issues,
        issues.Select(issue => QuestionId(issue.Fact ?? new("architecture", "Structure"))).Distinct().ToImmutableArray());

    private static ImmutableArray<DecisionIssue> Boundaries(ArchitectureScenario scenario)
    {
        var issues = new List<DecisionIssue>();
        void Issue(string code, string explanation, string next, FactReference? fact = null) => issues.Add(new(code, explanation, next, fact));
        if (scenario.SchemaVersion is not ("1.0.0" or RuleCatalog.SchemaVersion))
            Issue("boundary.schema", "This architecture schema version is unsupported.", "Use the current scenario schema.");
        if (scenario.Components.Length > ArchitectureLimits.MaxApplicationComponents || scenario.Relationships.Length > ArchitectureLimits.MaxAccessRelationships)
            Issue("boundary.topology", "The MVP supports at most five application components and eight connections.", "Simplify the topology or evaluate bounded sections separately.");
        if (scenario.Tenants.Domain.State == FactState.Known && scenario.Tenants.Domain.Value != IdentityDomain.Workforce)
            Issue("boundary.identity", "This MVP covers workforce identities in the public cloud.", "Use the official documentation for the selected identity environment.");
        foreach (var requirement in scenario.SpecializedRequirements)
            Issue("boundary.specialized", $"{requirement} is outside verified MVP coverage.", "Choose a supported Entra architecture or follow the provider's verified setup guidance.");
        foreach (var hop in scenario.Relationships)
        {
            if (hop.ResourceCompatibility.State == FactState.Known && hop.ResourceCompatibility.Value == CompatibilityConfirmation.Incompatible)
                Issue("boundary.resource-mode", "The selected resource does not support the intended acting identity.", "Change the acting identity or choose a compatible target.", new(hop.Id, "Identity"));
            var caller = scenario.Components.Single(c => c.Id == hop.CallerComponentId);
            var usesManaged = IsManaged(caller, hop);
            if (usesManaged && hop.TenantBoundary.State == FactState.Known && hop.TenantBoundary.Value == TenantBoundary.CrossTenant)
                Issue("boundary.managed-cross-tenant", "Ordinary managed identity access across tenants is not a supported MVP path.", "Choose a supported confidential-client credential and verify cross-tenant authorization.", new(caller.Id, "Credential"));
            if (caller.Kind.State == FactState.Known && caller.Kind.Value == ComponentKind.Api && hop.Identity.State == FactState.Known && hop.Identity.Value == ActingIdentity.DelegatedUser &&
                caller.Credential.State == FactState.Known && caller.Credential.Value != CredentialCapability.Certificate)
                Issue("boundary.obo-credential", "OBO credential coverage currently uses a certificate; ordinary managed identity is not delegated OBO.", "Choose a certificate for this middle tier. Federation-backed OBO needs a separately verified template.", new(caller.Id, "Credential"));
        }
        foreach (var component in scenario.Components)
        {
            var needsCredential = NeedsCredential(component, scenario);
            if (needsCredential && ScenarioValidation.IsFalse(component.CanProtectCredentials))
                Issue("boundary.confidential", "The selected server authentication requires protected credentials.", "Use a server capable of protecting a certificate or supported federation.", new(component.Id, "CanProtectCredentials"));
            if (component.Kind.State == FactState.Known && component.Kind.Value == ComponentKind.ServerWeb && ScenarioValidation.IsTrue(component.UserSignIn) &&
                component.Credential.State == FactState.Known && component.Credential.Value == CredentialCapability.ManagedIdentity)
                Issue("boundary.signin-managed", "Ordinary managed identity does not replace the confidential web client's sign-in credential.", "Choose certificate or supported client federation for web sign-in.", new(component.Id, "Credential"));
            if (component.Kind.State == FactState.Known && component.Kind.Value is ComponentKind.WindowsDesktop or ComponentKind.CliDevice && ScenarioValidation.IsTrue(component.UserSignIn) && ScenarioValidation.IsFalse(component.LocalBrowserAvailable))
            {
                if (ScenarioValidation.IsFalse(component.AlternateBrowserAvailable))
                    Issue("boundary.device-browser", "Device code needs a browser where the user can complete sign-in.", "Provide another browser or use a different architecture.", new(component.Id, "AlternateBrowserAvailable"));
                if (ScenarioValidation.IsFalse(component.DeviceCodePermitted))
                    Issue("boundary.device-policy", "Tenant policy prohibits device-code sign-in.", "Use a permitted interactive sign-in method or ask the policy owner for an alternative.", new(component.Id, "DeviceCodePermitted"));
            }
        }
        return issues.OrderBy(issue => issue.Code, StringComparer.Ordinal).ThenBy(issue => issue.Fact?.EntityId, StringComparer.Ordinal).ToImmutableArray();
    }

    private static ImmutableArray<DecisionIssue> RequiredFacts(ArchitectureScenario scenario)
    {
        var issues = new List<DecisionIssue>();
        void Require<T>(Fact<T> fact, string entity, string name, string explanation)
        {
            if (fact.State != FactState.Known)
                issues.Add(new("facts.required", explanation, "Answer the highlighted architecture question.", new(entity, name)));
        }
        if (scenario.Components.IsEmpty)
            issues.Add(new("facts.required", "Choose an application architecture.", "Choose an editable quick-start preset.", new("architecture", "Structure")));
        Require(scenario.Tenants.Domain, "tenants", "Domain", "Which identity environment is used?");
        Require(scenario.Tenants.Model, "tenants", "Model", "Is sign-in limited to one workforce tenant or open to multiple workforce tenants?");
        Require(scenario.Tenants.IncludesGuestUsers, "tenants", "IncludesGuestUsers", "Will guest users sign in through a workforce tenant?");
        foreach (var resource in scenario.Resources)
            Require(resource.Category, resource.Id, "Category", "Select Graph, Azure resources or custom resources.");
        foreach (var component in scenario.Components)
        {
            Require(component.Kind, component.Id, "Kind", "What kind of application is this component?");
            Require(component.Stack, component.Id, "Stack", "Where does the chosen application stack execute?");
            Require(component.UserSignIn, component.Id, "UserSignIn", "Will a user sign in to this component?");
            if (component.Kind.State != FactState.Known) continue;
            if (component.Kind.Value == ComponentKind.Api && component.IncomingIdentity.State != FactState.Known && !Incoming(scenario, component.Id).Any())
                Require(component.IncomingIdentity, component.Id, "IncomingIdentity", "Does the external caller present a user token or application token?");
            if (NeedsCredential(component, scenario))
                Require(component.CanProtectCredentials, component.Id, "CanProtectCredentials", "Can this server keep its credential protected?");
            if (component.Kind.Value == ComponentKind.ServerWeb && ScenarioValidation.IsTrue(component.UserSignIn))
                Require(component.Credential, component.Id, "Credential", "Choose the confidential web client's production credential.");
            if (component.Kind.Value is ComponentKind.WindowsDesktop or ComponentKind.CliDevice && ScenarioValidation.IsTrue(component.UserSignIn))
            {
                Require(component.LocalBrowserAvailable, component.Id, "LocalBrowserAvailable", "Can this application open a browser for sign-in?");
                if (ScenarioValidation.IsFalse(component.LocalBrowserAvailable))
                {
                    Require(component.AlternateBrowserAvailable, component.Id, "AlternateBrowserAvailable", "Can the user sign in using another browser?");
                    if (ScenarioValidation.IsTrue(component.AlternateBrowserAvailable))
                        Require(component.DeviceCodePermitted, component.Id, "DeviceCodePermitted", "Does tenant policy permit device code?");
                }
            }
        }
        foreach (var hop in scenario.Relationships)
        {
            Require(hop.Identity, hop.Id, "Identity", "Should this call act as the user or as the application?");
            Require(hop.TenantBoundary, hop.Id, "TenantBoundary", "Are caller and resource in the same tenant or different tenants?");
            var caller = scenario.Components.Single(c => c.Id == hop.CallerComponentId);
            if (hop.Identity.State != FactState.Known || caller.Kind.State != FactState.Known) continue;
            if (hop.Identity.Value == ActingIdentity.DelegatedUser)
            {
                if (caller.Kind.Value == ComponentKind.Api)
                {
                    Require(hop.IncomingRelationshipId, hop.Id, "IncomingRelationshipId", "Which incoming user-token connection supplies this middle tier's user context?");
                    if (hop.IncomingRelationshipId.State == FactState.Known && hop.IncomingRelationshipId.Value == AccessRelationship.ExternalUserToken)
                        Require(caller.IncomingIdentity, caller.Id, "IncomingIdentity", "Does this external caller present a user token accepted by the API?");
                    Require(caller.Credential, caller.Id, "Credential", "Choose a certificate to authenticate this OBO client.");
                }
                else if (caller.Kind.Value == ComponentKind.ServerWeb)
                    Require(caller.Credential, caller.Id, "Credential", "Choose this confidential client's credential.");
            }
            else
            {
                Require(caller.Hosting, caller.Id, "Hosting", "Where will the application run?");
                if ((caller.Credential.State != FactState.Known || caller.Credential.Value == CredentialCapability.ManagedIdentity) && caller.Hosting.State == FactState.Known && caller.Hosting.Value == HostingEnvironment.Azure)
                    Require(caller.ManagedIdentityAvailable, caller.Id, "ManagedIdentityAvailable", "Can this Azure host use managed identity?");
                if (!IsManaged(caller, hop) && !(caller.Hosting.State == FactState.Known && caller.Hosting.Value == HostingEnvironment.Azure && caller.ManagedIdentityAvailable.State == FactState.Unknown && caller.Credential.State == FactState.Unknown))
                    Require(caller.Credential, caller.Id, "Credential", "Choose a certificate or supported workload federation.");
            }
        }
        return issues.DistinctBy(issue => issue.Fact).ToImmutableArray();
    }

    private static bool NeedsCredential(ApplicationComponent component, ArchitectureScenario scenario) =>
        component.Kind.State == FactState.Known && component.Kind.Value is not (ComponentKind.BrowserSpa or ComponentKind.WindowsDesktop) &&
        !(component.Kind.Value == ComponentKind.CliDevice && ScenarioValidation.IsTrue(component.UserSignIn)) &&
        (component.Kind.Value == ComponentKind.ServerWeb && ScenarioValidation.IsTrue(component.UserSignIn) || scenario.Relationships.Any(hop => hop.CallerComponentId == component.Id));

    private static bool IsManaged(ApplicationComponent caller, AccessRelationship hop) =>
        hop.Identity.State == FactState.Known && hop.Identity.Value == ActingIdentity.Application &&
        (caller.Credential.State == FactState.Known ? caller.Credential.Value == CredentialCapability.ManagedIdentity :
            caller.Hosting.State == FactState.Known && caller.Hosting.Value == HostingEnvironment.Azure && ScenarioValidation.IsTrue(caller.ManagedIdentityAvailable));

    private static IEnumerable<AccessRelationship> Incoming(ArchitectureScenario scenario, string componentId) =>
        scenario.Relationships.Where(hop => scenario.Resources.Single(resource => resource.Id == hop.TargetResourceId).ApiComponentId == componentId);

    private static SignInApproach SignIn(ApplicationComponent component)
    {
        if (!ScenarioValidation.IsTrue(component.UserSignIn)) return SignInApproach.None;
        return component.Kind.Value switch
        {
            ComponentKind.ServerWeb => SignInApproach.OpenIdConnectAuthorizationCode,
            ComponentKind.WindowsDesktop or ComponentKind.CliDevice when ScenarioValidation.IsFalse(component.LocalBrowserAvailable) => SignInApproach.DeviceCode,
            _ => SignInApproach.InteractivePublicClient
        };
    }

    private static CredentialMechanism Credential(ApplicationComponent caller, AccessRelationship? hop = null) =>
        hop is not null && IsManaged(caller, hop) ? CredentialMechanism.ManagedIdentity : caller.Credential.Value switch
        {
            CredentialCapability.Certificate => CredentialMechanism.Certificate,
            CredentialCapability.WorkloadFederation => CredentialMechanism.WorkloadFederation,
            _ => CredentialMechanism.None
        };

    private static OAuthPlan Assemble(ArchitectureScenario scenario)
    {
        var trace = new List<DecisionTraceEntry>();
        void Match(string id, params FactReference[] facts) => trace.Add(new(id, facts.ToImmutableArray(), RuleCatalog.Get(id).Rationale));
        Match("input.integrity"); Match("boundary.mvp"); Match("facts.required");
        var components = new List<ComponentDecision>();
        foreach (var component in scenario.Components)
        {
            var signIn = SignIn(component);
            if (component.Execution.Origin == FactOrigin.Derived)
                Match("classification.execution", new FactReference(component.Id, "Stack"));
            var client = component.Kind.Value is ComponentKind.BrowserSpa or ComponentKind.WindowsDesktop || component.Kind.Value == ComponentKind.CliDevice && ScenarioValidation.IsTrue(component.UserSignIn)
                ? ClientClassification.Public : NeedsCredential(component, scenario) ? ClientClassification.Confidential : ClientClassification.NotApplicable;
            if (client != ClientClassification.NotApplicable)
                Match(client == ClientClassification.Public ? "classification.public" : "classification.confidential", new(component.Id, "Execution"), new(component.Id, "CanProtectCredentials"));
            if (signIn != SignInApproach.None)
                Match(signIn switch { SignInApproach.OpenIdConnectAuthorizationCode => "signin.oidc", SignInApproach.DeviceCode => "signin.device", _ => "signin.public" }, new(component.Id, "UserSignIn"), new(component.Id, "Stack"));
            if (signIn == SignInApproach.OpenIdConnectAuthorizationCode)
                Match(Credential(component) == CredentialMechanism.Certificate ? "credential.certificate" : "credential.federation", new FactReference(component.Id, "Credential"));
            components.Add(new(component.Id, client, signIn, signIn is SignInApproach.OpenIdConnectAuthorizationCode or SignInApproach.InteractivePublicClient,
                component.Kind.Value == ComponentKind.Api, signIn == SignInApproach.OpenIdConnectAuthorizationCode ? Credential(component) : CredentialMechanism.None));
        }
        var decisions = new List<RelationshipDecision>();
        foreach (var hop in scenario.Relationships)
        {
            var caller = scenario.Components.Single(c => c.Id == hop.CallerComponentId);
            var target = scenario.Resources.Single(r => r.Id == hop.TargetResourceId);
            var app = hop.Identity.Value == ActingIdentity.Application;
            var obo = !app && caller.Kind.Value == ComponentKind.Api;
            var acquisition = app ? TokenAcquisition.ClientCredentials : obo ? TokenAcquisition.OnBehalfOf : SignIn(caller) switch
            {
                SignInApproach.DeviceCode => TokenAcquisition.DeviceCode,
                SignInApproach.InteractivePublicClient when caller.Kind.Value != ComponentKind.BrowserSpa => TokenAcquisition.InteractivePublicClient,
                _ => TokenAcquisition.AuthorizationCode
            };
            var credential = app || caller.Kind.Value is ComponentKind.Api or ComponentKind.ServerWeb ? Credential(caller, hop) : CredentialMechanism.None;
            var ruleIds = new List<string> { app ? "hop.application" : obo ? "hop.obo" : "hop.delegated", "authorization.category" };
            if (credential != CredentialMechanism.None)
                ruleIds.Add(credential switch { CredentialMechanism.ManagedIdentity => "credential.managed", CredentialMechanism.Certificate => "credential.certificate", _ => "credential.federation" });
            foreach (var id in ruleIds) Match(id, new(hop.Id, "Identity"), new(hop.Id, "TargetResourceId"), new(caller.Id, "Credential"));
            var mode = target.Category.Value == ResourceCategory.AzureResource ? PermissionMode.AzureResourceAuthorization : app ? PermissionMode.ApplicationPermissions : PermissionMode.DelegatedScopes;
            var valueKey = mode == PermissionMode.AzureResourceAuthorization ? $"{target.Id}.{hop.Id}.authorization" : $"{target.Id}.{(app ? "app-role" : "scope")}";
            var guidance = mode == PermissionMode.AzureResourceAuthorization
                ? "Select the target's supported least-privilege role/permission and assignment scope for this acting identity; verify its authorization mechanism."
                : app ? "Choose only the target application permissions or custom app roles required; assign them to the calling service principal."
                : "Choose only the target delegated permissions or custom scopes required; confirm applicable user/admin consent policy.";
            decisions.Add(new(hop.Id, hop.Identity.Value, acquisition, credential, $"{target.Id}.audience", new(mode, valueKey, guidance), ruleIds.ToImmutableArray()));
        }
        var validation = new List<ApiValidationDecision>();
        foreach (var api in scenario.Components.Where(c => c.Kind.Value == ComponentKind.Api))
        {
            var resource = scenario.Resources.FirstOrDefault(r => r.ApiComponentId == api.Id);
            var requirements = Incoming(scenario, api.Id).Select(hop =>
            {
                var decision = decisions.Single(d => d.RelationshipId == hop.Id);
                return new ApiAuthorizationRequirement(hop.TargetResourceId, decision.Identity, decision.Authorization.Mode, decision.Authorization.DeveloperValueKey);
            }).Distinct().ToImmutableArray();
            if (requirements.IsEmpty || api.IncomingIdentity.State == FactState.Known)
            {
                var identities = api.IncomingIdentity.Value == IncomingTokenIdentity.Both ? new[] { ActingIdentity.DelegatedUser, ActingIdentity.Application } :
                    new[] { api.IncomingIdentity.Value == IncomingTokenIdentity.DelegatedUser ? ActingIdentity.DelegatedUser : ActingIdentity.Application };
                requirements = requirements.Concat(identities.Where(identity => !requirements.Any(requirement => requirement.AcceptedIdentity == identity)).Select(identity => new ApiAuthorizationRequirement(resource?.Id ?? api.Id, identity,
                    identity == ActingIdentity.DelegatedUser ? PermissionMode.DelegatedScopes : PermissionMode.ApplicationPermissions,
                    $"{resource?.Id ?? api.Id}.{(identity == ActingIdentity.DelegatedUser ? "scope" : "app-role")}"))).ToImmutableArray();
            }
            validation.Add(new(api.Id, $"{resource?.Id ?? api.Id}.audience", $"{api.Id}.issuer", requirements));
            Match("api.validation", new FactReference(api.Id, "IncomingIdentity"));
        }
        var prerequisites = new List<PlanPrerequisite>();
        var assumptions = new List<string>
        {
            "Workforce identities in the public cloud; no consumer or External ID customer configuration.",
            "Exact Graph permissions, Azure authorization details and custom scopes/app roles are developer-supplied values.",
            "Ready describes architectural completeness, not verified permissions, tenant state or resource compatibility."
        };
        foreach (var hop in scenario.Relationships)
        {
            var decision = decisions.Single(d => d.RelationshipId == hop.Id);
            var target = scenario.Resources.Single(resource => resource.Id == hop.TargetResourceId);
            if (hop.ResourceCompatibility.State != FactState.Known || hop.ResourceCompatibility.Value != CompatibilityConfirmation.Confirmed)
                prerequisites.Add(new($"{hop.Id}.compatibility", PrerequisiteKind.ResourceCompatibility,
                    $"Confirm {target.Name} supports {decision.Identity} access with {decision.Credential} credentials.",
                    "Check the target's official authentication documentation before implementing this conditional recommendation.", hop.Id));
            prerequisites.Add(new($"{hop.Id}.authorization", PrerequisiteKind.Assignment,
                decision.Authorization.SelectionGuidance,
                $"Supply {decision.Authorization.DeveloperValueKey}; configure and test the target's least-privilege authorization.", hop.Id));
            if (target.Category.Value != ResourceCategory.AzureResource)
                prerequisites.Add(new($"{hop.Id}.consent", PrerequisiteKind.Consent,
                    decision.Identity == ActingIdentity.Application ? "Application permissions require appropriate administrator authorization/assignment; registration ownership is a separate privilege." : "Delegated consent availability depends on selected permission definitions and user/admin consent policy.",
                    "Identify the consent actor and verify the permission definition and tenant policy; do not assume an app-registration owner can grant consent.", hop.Id));
            else
                prerequisites.Add(new($"{hop.Id}.resource-policy", PrerequisiteKind.TenantPolicy,
                    "Azure target authorization may use Azure RBAC, resource permissions or another supported mechanism; app-registration consent alone does not authorize all Azure resources.",
                    "Follow the target's authorization documentation and identify who can assign the required access.", hop.Id));
            prerequisites.Add(new($"{hop.Id}.audience", PrerequisiteKind.DeploymentValue,
                "A token must be acquired for this resource's audience. It cannot be reused for a different downstream resource.",
                $"Supply {decision.AudienceValueKey} and the corresponding target scope/resource endpoint.", hop.Id));
            if (hop.TenantBoundary.Value == TenantBoundary.CrossTenant)
                prerequisites.Add(new($"{hop.Id}.cross-tenant", PrerequisiteKind.TenantPolicy,
                    "Caller and resource tenant differ; verify target compatibility, trusted issuers, client service principal and consent/assignments in the applicable resource tenant.",
                    "Record caller/home and resource tenant IDs, restrict accepted issuers, and obtain appropriate resource-tenant authorization.", hop.Id));
            if (decision.Credential == CredentialMechanism.ManagedIdentity)
                prerequisites.Add(new($"{hop.Id}.managed", PrerequisiteKind.Assignment,
                    "Managed identity availability does not grant target access. Its service principal must receive the target's supported assignment.",
                    "Enable/identify the hosted managed identity and assign supported resource access; document a separate local-development credential.", hop.Id));
            if (decision.Credential == CredentialMechanism.WorkloadFederation)
                prerequisites.Add(new($"{hop.Id}.federation", PrerequisiteKind.ResourceCompatibility,
                    "Federation requires a supported external issuer/subject/audience trust and compatible token-acquisition integration.",
                    "Verify the exact platform/library integration and configure a federated credential without storing a secret.", hop.Id));
            if (decision.Acquisition == TokenAcquisition.OnBehalfOf)
                prerequisites.Add(new($"{hop.Id}.challenge", PrerequisiteKind.TenantPolicy,
                    "Downstream consent and Conditional Access may require interaction through the initiating client.",
                    "Implement supported claims-challenge/consent handling and never forward the incoming API token downstream.", hop.Id));
            if (hop.IncomingRelationshipId.State == FactState.Known && hop.IncomingRelationshipId.Value == AccessRelationship.ExternalUserToken)
                prerequisites.Add(new($"{hop.Id}.external-user", PrerequisiteKind.TenantPolicy,
                    "This OBO call explicitly uses an external caller's user access token issued for the middle-tier API.",
                    "Validate the API audience/issuer and enforce user scopes on this route; app-only callers must not enter its delegated OBO path.", hop.Id));
            Match("tenant.boundaries", new(hop.Id, "TenantBoundary"), new(hop.CallerComponentId, "HomeTenantReference"), new(target.Id, "ResourceTenantReference"));
            Match("consent.policy", new FactReference(hop.Id, "Identity"));
        }
        if (scenario.Tenants.Model.Value == WorkforceTenantModel.Multitenant)
        {
            assumptions.Add("Workforce multitenant access requires tenant-aware issuer validation and an explicit accepted-tenant policy; /common is not permission to trust arbitrary issuers.");
            prerequisites.Add(new("tenants.multitenant", PrerequisiteKind.TenantPolicy,
                "Each applicable tenant needs the client service principal and appropriate consent; home-tenant registration is not sufficient.",
                "Use workforce account audience, tenant-aware validation and documented onboarding/consent for allowed resource tenants."));
        }
        foreach (var component in components.Where(c => c.SignIn != SignInApproach.None))
            prerequisites.Add(new($"{component.ComponentId}.signin-policy", PrerequisiteKind.TenantPolicy,
                "Sign-in and consent must be permitted by the workforce tenant's policies.",
                "Verify tenant policy, redirect handling and applicable consent before testing sign-in."));
        if (scenario.Tenants.IncludesGuestUsers.Value)
        {
            assumptions.Add("Guest membership and sign-in use the applicable workforce resource-tenant context, not a consumer identity flow.");
            prerequisites.Add(new("tenants.guests", PrerequisiteKind.TenantPolicy,
                "Guest membership, resource-tenant policy and resource assignments must permit the intended access.",
                "Verify guest membership and consent/authorization in the resource tenant using a guest test identity."));
        }
        var registrations = components.Where(c => c.SignIn != SignInApproach.None || c.ValidatesIncomingTokens || decisions.Any(d => scenario.Relationships.Single(h => h.Id == d.RelationshipId).CallerComponentId == c.ComponentId))
            .Select(c => new RegistrationResponsibility(c.ComponentId,
                !decisions.Where(d => scenario.Relationships.Single(h => h.Id == d.RelationshipId).CallerComponentId == c.ComponentId).All(d => d.Credential == CredentialMechanism.ManagedIdentity) || c.SignIn != SignInApproach.None || c.ValidatesIncomingTokens,
                true, scenario.Tenants.Model.Value == WorkforceTenantModel.Multitenant ? "AzureADMultipleOrgs" : "AzureADMyOrg", "Fresh-start application identity and configuration.")).ToImmutableArray();
        Match("registration.fresh"); Match("plan.assembly");
        var orderedTrace = trace.OrderBy(entry => RuleCatalog.Get(entry.RuleId).Phase).ThenByDescending(entry => RuleCatalog.Get(entry.RuleId).Priority)
            .ThenBy(entry => entry.RuleId, StringComparer.Ordinal).ThenBy(entry => string.Join('/', entry.RelevantFacts.Select(f => f.EntityId)), StringComparer.Ordinal).ToImmutableArray();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(RuleCatalog.RuleVersion + JsonSerializer.Serialize(scenario)))).ToLowerInvariant();
        return new()
        {
            Id = "plan-" + hash, ScenarioId = scenario.Id, Scenario = scenario, Versions = new(RuleCatalog.SchemaVersion, RuleCatalog.RuleVersion),
            Components = components.ToImmutableArray(), Relationships = decisions.ToImmutableArray(), ApiValidation = validation.ToImmutableArray(),
            Registrations = registrations, Trace = orderedTrace,
            Prerequisites = prerequisites.OrderBy(prerequisite => prerequisite.Id, StringComparer.Ordinal).ToImmutableArray(), Assumptions = assumptions.ToImmutableArray(),
            Sources = orderedTrace.SelectMany(entry => RuleCatalog.Get(entry.RuleId).Sources).Distinct().OrderBy(source => source.Url.AbsoluteUri, StringComparer.Ordinal).ToImmutableArray()
        };
    }
}
