using System.Collections.Immutable;

namespace EntraAdvisor.Engine.Contracts;

public sealed record ApplicationComponent
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public Fact<ComponentKind> Kind { get; init; } = Fact<ComponentKind>.Unknown();
    public Fact<ExecutionLocation> Execution { get; init; } = Fact<ExecutionLocation>.Unknown();
    public Fact<ImplementationStack> Stack { get; init; } = Fact<ImplementationStack>.Unknown();
    public Fact<bool> UserSignIn { get; init; } = Fact<bool>.Unknown();
    public Fact<bool> CanProtectCredentials { get; init; } = Fact<bool>.Unknown();
    public Fact<bool> LocalBrowserAvailable { get; init; } = Fact<bool>.Unknown();
    public Fact<bool> AlternateBrowserAvailable { get; init; } = Fact<bool>.Unknown();
    public Fact<bool> DeviceCodePermitted { get; init; } = Fact<bool>.Unknown();
    public Fact<HostingEnvironment> Hosting { get; init; } = Fact<HostingEnvironment>.Unknown();
    public Fact<bool> ManagedIdentityAvailable { get; init; } = Fact<bool>.Unknown();
    public Fact<IncomingTokenIdentity> IncomingIdentity { get; init; } = Fact<IncomingTokenIdentity>.Unknown();
    public Fact<CredentialCapability> Credential { get; init; } = Fact<CredentialCapability>.Unknown();
    public Fact<string> HomeTenantReference { get; init; } = Fact<string>.Unknown();
}

/// <summary>Provider targets need no custom registration; custom API targets reference a component.</summary>
public sealed record TargetResource
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public Fact<ResourceCategory> Category { get; init; } = Fact<ResourceCategory>.Unknown();
    public string? ApiComponentId { get; init; }
    public Fact<string> ResourceTenantReference { get; init; } = Fact<string>.Unknown();
}

public sealed record AccessRelationship
{
    public const string ExternalUserToken = "$external-user";
    public required string Id { get; init; }
    public required string CallerComponentId { get; init; }
    public required string TargetResourceId { get; init; }
    public Fact<ActingIdentity> Identity { get; init; } = Fact<ActingIdentity>.Unknown();
    // Selects the incoming user-token hop for an OBO decision, without storing any token.
    public Fact<string> IncomingRelationshipId { get; init; } = Fact<string>.NotApplicable();
    public Fact<TenantBoundary> TenantBoundary { get; init; } = Fact<TenantBoundary>.Unknown();
    public Fact<CompatibilityConfirmation> ResourceCompatibility { get; init; } = Fact<CompatibilityConfirmation>.Unknown();
}

public sealed record WorkforceTenantContext
{
    public Fact<IdentityDomain> Domain { get; init; } = Fact<IdentityDomain>.Unknown();
    public Fact<WorkforceTenantModel> Model { get; init; } = Fact<WorkforceTenantModel>.Unknown();
    public Fact<bool> IncludesGuestUsers { get; init; } = Fact<bool>.Unknown();
}

public sealed record ArchitectureScenario
{
    public required string Id { get; init; }
    public required string SchemaVersion { get; init; }
    public ImmutableArray<ApplicationComponent> Components { get; init; } = [];
    public ImmutableArray<TargetResource> Resources { get; init; } = [];
    public ImmutableArray<AccessRelationship> Relationships { get; init; } = [];
    public WorkforceTenantContext Tenants { get; init; } = new();
    public ImmutableArray<SpecializedRequirement> SpecializedRequirements { get; init; } = [];
}
