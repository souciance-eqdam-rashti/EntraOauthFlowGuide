using System.Collections.Immutable;
using System.Text.RegularExpressions;
using EntraAdvisor.Engine.Contracts;

namespace EntraAdvisor.Engine;

internal static partial class ScenarioValidation
{
    [GeneratedRegex("^[a-z][a-z0-9-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeIdentifier();

    public static ImmutableArray<DecisionIssue> Shape(ArchitectureScenario scenario)
    {
        var issues = new List<DecisionIssue>();
        void Issue(string message) => issues.Add(new("input.shape", message, "Choose a supported architecture preset and correct the referenced facts.", new("architecture", "Structure")));
        if (string.IsNullOrWhiteSpace(scenario.Id)) Issue("The scenario needs a stable identifier.");
        if (scenario.Components.IsDefault || scenario.Resources.IsDefault || scenario.Relationships.IsDefault || scenario.SpecializedRequirements.IsDefault)
            Issue("Architecture collections must be initialized.");
        if (scenario.Tenants is null) Issue("Tenant context is missing.");
        if (issues.Count > 0) return issues.ToImmutableArray();
        foreach (var entity in scenario.Components.Cast<object>().Concat(scenario.Resources).Concat(scenario.Relationships).Append(scenario.Tenants!))
        {
            if (entity is null) { Issue("Architecture collections cannot contain null entries."); continue; }
            foreach (var property in entity.GetType().GetProperties())
            {
                if (!property.PropertyType.IsGenericType || property.PropertyType.GetGenericTypeDefinition() != typeof(Fact<>)) continue;
                var fact = property.GetValue(entity);
                if (fact is null) { Issue($"{property.Name} must use an explicit Unknown fact rather than null."); continue; }
                var state = (FactState)fact.GetType().GetProperty("State")!.GetValue(fact)!;
                var valueType = property.PropertyType.GenericTypeArguments[0];
                var value = fact.GetType().GetProperty("Value")!.GetValue(fact);
                if (state == FactState.Known && (value is null || value is string text && string.IsNullOrWhiteSpace(text) || valueType.IsEnum && !Enum.IsDefined(valueType, value!)))
                    Issue($"{property.Name} has an invalid supplied value.");
            }
        }
        foreach (var component in scenario.Components.Where(c => c is not null))
            if (string.IsNullOrWhiteSpace(component.Id) || string.IsNullOrWhiteSpace(component.Name)) Issue("Every component needs an identifier and display name.");
        foreach (var resource in scenario.Resources.Where(r => r is not null))
            if (string.IsNullOrWhiteSpace(resource.Id) || string.IsNullOrWhiteSpace(resource.Name)) Issue("Every resource needs an identifier and display name.");
        foreach (var hop in scenario.Relationships.Where(h => h is not null))
            if (string.IsNullOrWhiteSpace(hop.Id) || string.IsNullOrWhiteSpace(hop.CallerComponentId) || string.IsNullOrWhiteSpace(hop.TargetResourceId)) Issue("Every connection needs an identifier, caller and target.");
        if (scenario.SpecializedRequirements.Any(requirement => !Enum.IsDefined(requirement))) Issue("A specialized requirement is unrecognized.");
        return issues.ToImmutableArray();
    }

    public static ImmutableArray<DecisionIssue> Integrity(ArchitectureScenario scenario)
    {
        var issues = new List<DecisionIssue>();
        void Issue(string code, string explanation, string entity, string fact, string next = "Correct the architecture answers.") =>
            issues.Add(new(code, explanation, next, new(entity, fact)));

        var ids = scenario.Components.Select(c => c.Id).Concat(scenario.Resources.Select(r => r.Id)).Concat(scenario.Relationships.Select(h => h.Id));
        foreach (var id in ids)
            if (!SafeIdentifier().IsMatch(id)) Issue("input.identifier", "Identifiers use letters, digits and hyphens, starting with a letter.", "architecture", "Structure");
        if (ids.Any(id => id is "architecture" or "tenants"))
            Issue("input.reserved-id", "Architecture and tenants are reserved question identifiers.", "architecture", "Structure");
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Count())
            Issue("input.duplicate", "Identifiers must be unique across components, targets and connections.", "architecture", "Structure");
        if (issues.Count > 0) return issues.ToImmutableArray();

        var components = scenario.Components.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var resources = scenario.Resources.ToDictionary(r => r.Id, StringComparer.Ordinal);
        var hops = scenario.Relationships.ToDictionary(h => h.Id, StringComparer.Ordinal);
        foreach (var resource in scenario.Resources)
        {
            if (resource.ApiComponentId is not null && (!components.TryGetValue(resource.ApiComponentId, out var api) || api.Kind.State == FactState.Known && api.Kind.Value != ComponentKind.Api))
                Issue("input.api-reference", "A custom API resource must reference an API component.", "architecture", "Structure");
            if (resource.Category.State == FactState.Known && resource.Category.Value != ResourceCategory.CustomResource && resource.ApiComponentId is not null)
                Issue("input.provider-registration", "Provider-owned resources must not reference a custom API registration.", resource.Id, "Category");
            if (resource.Category.State == FactState.Known && resource.Category.Value == ResourceCategory.CustomResource && resource.ApiComponentId is null)
                Issue("input.custom-api", "Add the custom API component to provide its fresh-start setup guide.", "architecture", "Structure");
        }
        if (scenario.Resources.Where(r => r.ApiComponentId is not null).GroupBy(r => r.ApiComponentId).Any(group => group.Count() > 1))
            Issue("input.api-resource", "Represent an API once as a target resource; its scopes can serve multiple callers.", "architecture", "Structure");
        foreach (var component in scenario.Components)
        {
            if (component.Kind.State == FactState.Known && component.Stack.State == FactState.Known && component.Kind.Value != ScenarioNormalizer.KindFor(component.Stack.Value))
                Issue("input.stack-kind", "The selected framework executes as a different application type.", component.Id, "Stack");
            if (component.Execution.State == FactState.Known && component.Stack.State == FactState.Known && component.Execution.Value != ScenarioNormalizer.ExecutionFor(component.Stack.Value))
                Issue("input.execution", "Framework and execution location conflict; model mixed execution as separate components.", component.Id, "Execution");
            if (component.Execution.State == FactState.Known && component.Execution.Value is ExecutionLocation.Browser or ExecutionLocation.WindowsDesktop && IsTrue(component.CanProtectCredentials))
                Issue("input.public-secret", "Browser and desktop public clients cannot protect confidential client credentials.", component.Id, "CanProtectCredentials");
            if (component.Kind.State == FactState.Known && component.Kind.Value is ComponentKind.Api or ComponentKind.BackgroundService && IsTrue(component.UserSignIn))
                Issue("input.api-signin", "Represent interactive user sign-in in a client component, not in the API or background service.", component.Id, "UserSignIn");
            if (component.Credential.State == FactState.Known && component.Credential.Value == CredentialCapability.ManagedIdentity &&
                (component.Hosting.State == FactState.Known && component.Hosting.Value != HostingEnvironment.Azure || IsFalse(component.ManagedIdentityAvailable)))
                Issue("input.managed-host", "Managed identity was selected where this host says it is unavailable.", component.Id, "Credential");
        }
        foreach (var hop in scenario.Relationships)
        {
            if (hop.TenantBoundary.State == FactState.Known && hop.TenantBoundary.Value == TenantBoundary.CrossTenant && scenario.Tenants.Model.State == FactState.Known && scenario.Tenants.Model.Value == WorkforceTenantModel.SingleTenant)
                Issue("input.cross-tenant-audience", "The cross-tenant access preset requires workforce multitenant registration coverage.", "tenants", "Model", "Select workforce multitenant coverage and verify service principals/consent in the applicable tenants.");
            if (!components.ContainsKey(hop.CallerComponentId) || !resources.ContainsKey(hop.TargetResourceId))
            { Issue("input.hop-reference", "A connection references a missing caller or resource.", "architecture", "Structure"); continue; }
            var caller = components[hop.CallerComponentId];
            var target = resources[hop.TargetResourceId];
            if (hop.Identity.State == FactState.Known && target.ApiComponentId is not null && components.TryGetValue(target.ApiComponentId, out var receivingApi) && receivingApi.IncomingIdentity.State == FactState.Known && receivingApi.IncomingIdentity.Value != IncomingTokenIdentity.Both &&
                (receivingApi.IncomingIdentity.Value == IncomingTokenIdentity.DelegatedUser) != (hop.Identity.Value == ActingIdentity.DelegatedUser))
                Issue("input.incoming-mode", "This API's accepted identity contradicts an incoming connection.", receivingApi.Id, "IncomingIdentity");
            if (hop.Identity.State == FactState.Known && hop.Identity.Value == ActingIdentity.Application && caller.Kind.State == FactState.Known && caller.Kind.Value is ComponentKind.BrowserSpa or ComponentKind.WindowsDesktop)
                Issue("input.public-app-only", "A public user client cannot perform confidential app-only authentication.", hop.Id, "Identity", "Use user identity or add a server workload component.");
            if (hop.Identity.State == FactState.Known && hop.Identity.Value == ActingIdentity.Application && caller.Kind.State == FactState.Known && caller.Kind.Value == ComponentKind.CliDevice && IsTrue(caller.UserSignIn))
                Issue("input.cli-app-only", "A CLI signed in as a public user client cannot also act as a confidential application on this connection.", hop.Id, "Identity", "Use user identity or represent a separate workload identity component.");
            if (hop.Identity.State == FactState.Known && hop.Identity.Value == ActingIdentity.DelegatedUser && caller.Kind.State == FactState.Known && caller.Kind.Value != ComponentKind.Api && IsFalse(caller.UserSignIn))
                Issue("input.user-missing", "Delegated access needs a signed-in user.", caller.Id, "UserSignIn");
            if (hop.IncomingRelationshipId.State == FactState.Known)
            {
                if (hop.IncomingRelationshipId.Value == AccessRelationship.ExternalUserToken)
                {
                    if (caller.Kind.State == FactState.Known && caller.Kind.Value != ComponentKind.Api || caller.IncomingIdentity.State == FactState.Known && caller.IncomingIdentity.Value == IncomingTokenIdentity.Application)
                        Issue("input.external-user", "The external user-token boundary requires an API that accepts user access tokens.", caller.Id, "IncomingIdentity");
                }
                else if (!hops.TryGetValue(hop.IncomingRelationshipId.Value!, out var incoming) || !resources.TryGetValue(incoming.TargetResourceId, out var incomingTarget) || incomingTarget.ApiComponentId != caller.Id)
                    Issue("input.obo-reference", "The chosen incoming token connection must target this middle-tier API.", hop.Id, "IncomingRelationshipId");
                else if (hop.Identity.State == FactState.Known && hop.Identity.Value == ActingIdentity.DelegatedUser && incoming.Identity.State == FactState.Known && incoming.Identity.Value != ActingIdentity.DelegatedUser)
                    Issue("input.obo-app-token", "An application token does not carry delegated user identity for OBO.", hop.Id, "IncomingRelationshipId");
            }
            if (caller.Kind.State == FactState.Known && caller.Kind.Value == ComponentKind.Api && hop.Identity.State == FactState.Known && hop.Identity.Value == ActingIdentity.DelegatedUser &&
                caller.IncomingIdentity.State == FactState.Known && caller.IncomingIdentity.Value == IncomingTokenIdentity.Application)
                Issue("input.obo-incoming", "An API accepting only app-only tokens cannot use delegated OBO.", caller.Id, "IncomingIdentity");
            if (hop.TenantBoundary.State == FactState.Known && caller.HomeTenantReference.State == FactState.Known && resources[hop.TargetResourceId].ResourceTenantReference.State == FactState.Known)
            {
                var same = caller.HomeTenantReference.Value == resources[hop.TargetResourceId].ResourceTenantReference.Value;
                if (same != (hop.TenantBoundary.Value == TenantBoundary.SameTenant))
                    Issue("input.tenant-conflict", "The tenant references contradict the selected tenant boundary.", hop.Id, "TenantBoundary");
            }
        }

        if (issues.Count == 0)
        {
            var visited = new HashSet<string>();
            var active = new HashSet<string>();
            bool HasCycle(string id)
            {
                if (active.Contains(id)) return true;
                if (!visited.Add(id)) return false;
                active.Add(id);
                foreach (var target in scenario.Relationships.Where(h => h.CallerComponentId == id).Select(h => resources[h.TargetResourceId].ApiComponentId).Where(id => id is not null))
                    if (HasCycle(target!)) return true;
                active.Remove(id);
                return false;
            }
            if (scenario.Components.Any(c => HasCycle(c.Id))) Issue("input.cycle", "Cyclic API access is outside the acyclic topology model.", "architecture", "Structure", "Choose a supported acyclic topology preset.");
        }
        return issues.OrderBy(issue => issue.Code, StringComparer.Ordinal).ThenBy(issue => issue.Fact?.EntityId, StringComparer.Ordinal).ToImmutableArray();
    }

    internal static bool IsTrue(Fact<bool> fact) => fact.State == FactState.Known && fact.Value;
    internal static bool IsFalse(Fact<bool> fact) => fact.State == FactState.Known && !fact.Value;
}
