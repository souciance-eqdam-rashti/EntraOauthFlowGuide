using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using EntraAdvisor.Engine.Contracts;

namespace EntraAdvisor.Guide.Provisioning;

public enum RegistrationPlatform { None, Spa, Web, PublicClient }
public sealed record ProvisioningApplication(string ComponentId, string Name, bool CreateRegistration,
    string SignInAudience, RegistrationPlatform Platform, bool DeviceCode, bool IsApi, CredentialMechanism Credential, bool UsesManagedIdentity);
public sealed record ProvisioningPermission(string Key, string ApiComponentId, PermissionMode Mode, Guid Id);
public sealed record ProvisioningAccess(string RelationshipId, string CallerComponentId, string ResourceId,
    string? ApiComponentId, ResourceCategory Category, PermissionMode Mode, string PermissionKey, TenantBoundary Boundary, CredentialMechanism Credential);
public sealed record ProvisioningPlan(string PlanId, ImmutableArray<ProvisioningApplication> Applications,
    ImmutableArray<ProvisioningPermission> Permissions, ImmutableArray<ProvisioningAccess> Access,
    ImmutableArray<string> ManualActions, ImmutableArray<PlanPrerequisite> Prerequisites);

/// <summary>Uses architecture decisions only. Never extracts settings from displayed guide prose.</summary>
public sealed class ProvisioningPlanBuilder
{
    public ProvisioningPlan Build(OAuthPlan plan)
    {
        var apps = plan.Registrations.Select(r => {
            var c = plan.Scenario.Components.Single(c => c.Id == r.ComponentId);
            var d = plan.Components.Single(d => d.ComponentId == c.Id);
            var platform = d.SignIn == SignInApproach.None ? RegistrationPlatform.None : c.Kind.Value switch {
                ComponentKind.BrowserSpa => RegistrationPlatform.Spa,
                ComponentKind.ServerWeb => RegistrationPlatform.Web,
                _ => RegistrationPlatform.PublicClient
            };
            var credentials = plan.Relationships.Where(h => plan.Scenario.Relationships.Single(r => r.Id == h.RelationshipId).CallerComponentId == c.Id)
                .Select(h => h.Credential).Append(d.Credential).Distinct().ToArray();
            var credential = credentials.Contains(CredentialMechanism.Certificate) ? CredentialMechanism.Certificate :
                credentials.Contains(CredentialMechanism.WorkloadFederation) ? CredentialMechanism.WorkloadFederation :
                credentials.Contains(CredentialMechanism.ManagedIdentity) ? CredentialMechanism.ManagedIdentity : CredentialMechanism.None;
            return new ProvisioningApplication(c.Id, c.Name, r.CreateRegistration, r.AccountAudience,
                platform, d.SignIn == SignInApproach.DeviceCode, d.ValidatesIncomingTokens, credential, credentials.Contains(CredentialMechanism.ManagedIdentity));
        }).ToImmutableArray();
        var permissions = plan.ApiValidation.SelectMany(api => api.Authorization
            .Where(a => a.Mode is PermissionMode.DelegatedScopes or PermissionMode.ApplicationPermissions)
            .Select(a => new ProvisioningPermission(a.DeveloperValueKey, api.ComponentId, a.Mode, StableId(api.ComponentId + ":" + a.DeveloperValueKey))))
            .DistinctBy(p => (p.ApiComponentId, p.Key, p.Mode)).ToImmutableArray();
        var access = plan.Relationships.Select(d => {
            var h = plan.Scenario.Relationships.Single(h => h.Id == d.RelationshipId);
            var r = plan.Scenario.Resources.Single(r => r.Id == h.TargetResourceId);
            return new ProvisioningAccess(h.Id, h.CallerComponentId, r.Id, r.ApiComponentId, r.Category.Value,
                d.Authorization.Mode, d.Authorization.DeveloperValueKey, h.TenantBoundary.Value, d.Credential);
        }).ToImmutableArray();
        var notes = new List<string> {
            "Fill settings.json before running anything: application names, tenant IDs, redirect URIs, permission values and descriptions are developer inputs, not selected examples.",
            "Register identities in each home tenant first, sharing state.json between stages; then configure permissions. Consent is a separate, opt-in tenant-wide operation.",
            "Exports manage fresh, uniquely named registrations. Reuse the same deployment key and state file on reruns; do not point these files at unrelated existing registrations.",
            "Application code, token validation, sign-in handling, host deployment and live authentication tests remain the implementation guide's responsibility."
        };
        if (apps.Any(a => a.UsesManagedIdentity)) notes.Add("Enable the managed identity on its Azure host first. Supply its existing service principal object ID; the export does not create a replacement app registration or configure the host.");
        if (apps.Any(a => a.Credential == CredentialMechanism.Certificate)) notes.Add("Supply a public certificate file for each certificate-authenticated app. Keep its private key in the application's secure runtime store; exports never generate or download private credentials.");
        if (apps.Any(a => a.Credential == CredentialMechanism.WorkloadFederation)) notes.Add("Workload federation needs provider-specific issuer, subject and audience configuration. Configure the federated identity credential manually before using that workload.");
        if (access.Any(a => a.Category == ResourceCategory.AzureResource)) notes.Add("Azure resource authorization is resource-specific. Configure the selected resource's Azure RBAC/data-plane access manually; these exports do not guess a role or assignment scope.");
        if (access.Any(a => a.Boundary == TenantBoundary.CrossTenant)) notes.Add("Cross-tenant stages require explicit home/resource tenant IDs and a shared state file. Custom API permission IDs are wired across stages; run consent in the resource tenant after registration. Verify multitenant compatibility and tenant policy. Managed identities cannot be onboarded as cross-tenant app registrations.");
        if (plan.Scenario.Tenants.Model.Value == WorkforceTenantModel.Multitenant) notes.Add("Multitenant customer onboarding/consent must be performed separately for each customer tenant; a home-tenant deployment is not customer onboarding.");
        if (plan.Scenario.Tenants.IncludesGuestUsers.Value) notes.Add("Guest invitations, tenant policies and user/group assignments remain manual and must be verified before testing.");
        return new(plan.Id, apps, permissions, access, notes.ToImmutableArray(), plan.Prerequisites);
    }
    private static Guid StableId(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes("entra-advisor:" + key)).AsSpan(0, 16));
}
