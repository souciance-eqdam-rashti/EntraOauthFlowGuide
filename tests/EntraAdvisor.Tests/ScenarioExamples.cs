using System.Collections.Immutable;
using EntraAdvisor.Engine;
using EntraAdvisor.Engine.Contracts;
using EntraAdvisor.Engine.Rules;

namespace EntraAdvisor.Tests;

internal static class ScenarioExamples
{
    public static ApplicationComponent Component(string id, ImplementationStack stack, bool signIn = false) => new()
    {
        Id = id, Name = id, Kind = Fact<ComponentKind>.Supplied(ScenarioNormalizer.KindFor(stack)), Stack = Fact<ImplementationStack>.Supplied(stack),
        UserSignIn = Fact<bool>.Supplied(signIn), CanProtectCredentials = stack is ImplementationStack.BlazorWebAssembly or ImplementationStack.JavaScriptTypeScript or ImplementationStack.WindowsWpf || stack == ImplementationStack.DotNetConsole && signIn
            ? Fact<bool>.Supplied(false) : Fact<bool>.Supplied(true),
        Credential = stack is ImplementationStack.BlazorServer or ImplementationStack.AspNetCoreApi or ImplementationStack.DotNetWorker
            ? Fact<CredentialCapability>.Supplied(CredentialCapability.Certificate) : Fact<CredentialCapability>.NotApplicable(),
        Hosting = Fact<HostingEnvironment>.Supplied(HostingEnvironment.NonAzure),
        LocalBrowserAvailable = Fact<bool>.Supplied(true), IncomingIdentity = Fact<IncomingTokenIdentity>.Unknown()
    };

    public static TargetResource Resource(string id, ResourceCategory category, string? api = null) => new()
    { Id = id, Name = id, Category = Fact<ResourceCategory>.Supplied(category), ApiComponentId = api };

    public static AccessRelationship Hop(string id, string caller, string target, ActingIdentity identity = ActingIdentity.DelegatedUser, string? incoming = null) => new()
    {
        Id = id, CallerComponentId = caller, TargetResourceId = target, Identity = Fact<ActingIdentity>.Supplied(identity),
        TenantBoundary = Fact<TenantBoundary>.Supplied(TenantBoundary.SameTenant),
        IncomingRelationshipId = incoming is null ? Fact<string>.NotApplicable() : Fact<string>.Supplied(incoming)
    };

    public static ArchitectureScenario Scenario(ImmutableArray<ApplicationComponent> components, ImmutableArray<TargetResource> resources = default, ImmutableArray<AccessRelationship> hops = default) => new()
    {
        Id = "example", SchemaVersion = RuleCatalog.SchemaVersion, Components = components,
        Resources = resources.IsDefault ? [] : resources, Relationships = hops.IsDefault ? [] : hops,
        Tenants = new() { Domain = Fact<IdentityDomain>.Supplied(IdentityDomain.Workforce), Model = Fact<WorkforceTenantModel>.Supplied(WorkforceTenantModel.SingleTenant), IncludesGuestUsers = Fact<bool>.Supplied(false) }
    };

    public static ArchitectureScenario Chain(ActingIdentity downstream = ActingIdentity.DelegatedUser) => Scenario(
        [Component("web", ImplementationStack.BlazorServer, true), Component("api-a", ImplementationStack.AspNetCoreApi), Component("api-b", ImplementationStack.AspNetCoreApi)],
        [Resource("orders", ResourceCategory.CustomResource, "api-a"), Resource("inventory", ResourceCategory.CustomResource, "api-b")],
        [Hop("web-orders", "web", "orders"), Hop("orders-inventory", "api-a", "inventory", downstream, downstream == ActingIdentity.DelegatedUser ? "web-orders" : null)]);

    public static ArchitectureScenario Worker(ResourceCategory category = ResourceCategory.MicrosoftGraph, bool managed = false) => Scenario(
        [Component("worker", ImplementationStack.DotNetWorker) with
        {
            Credential = managed ? Fact<CredentialCapability>.Unknown() : Fact<CredentialCapability>.Supplied(CredentialCapability.Certificate),
            Hosting = Fact<HostingEnvironment>.Supplied(managed ? HostingEnvironment.Azure : HostingEnvironment.NonAzure),
            ManagedIdentityAvailable = Fact<bool>.Supplied(managed)
        }], [Resource("target", category)], [Hop("worker-target", "worker", "target", ActingIdentity.Application)]);

    public static ArchitectureScenario Device() => Scenario(
        [Component("cli", ImplementationStack.DotNetConsole, true) with
        { LocalBrowserAvailable = Fact<bool>.Supplied(false), AlternateBrowserAvailable = Fact<bool>.Supplied(true), DeviceCodePermitted = Fact<bool>.Supplied(true) }],
        [Resource("graph", ResourceCategory.MicrosoftGraph)], [Hop("cli-graph", "cli", "graph")]);
}
