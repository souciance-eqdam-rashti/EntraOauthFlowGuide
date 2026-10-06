using System.Collections.Immutable;
using EntraAdvisor.Engine.Contracts;
using EntraAdvisor.Engine.Rules;

namespace EntraAdvisor.Engine.Questionnaire;

public sealed record TopologyPreset(string Id, string Title, string Description, string Example);

public static class TopologyPresets
{
    public static ImmutableArray<TopologyPreset> All { get; } =
    [
        new("signin", "User sign-in", "A server web application with no API calls.", "User → Web app."),
        new("client-api", "Application and API", "A browser application calling a custom API.", "Browser app → API."),
        new("api-chain", "Web app and API chain", "A server web app calls one API, which calls another.", "Web app → API 1 → API 2."),
        new("api-only", "API only", "An API validates tokens from callers outside this diagram.", "External caller → API."),
        new("worker", "Background service", "A workload calls a protected resource as the application.", "Background app → Resource."),
        new("device", "Command-line sign-in", "A user signs in to a command-line application.", "User → Command-line app → Resource."),
        new("desktop", "Windows desktop", "A desktop application calls a protected resource.", "User → Desktop app → Resource."),
        new("fan-out", "Multiple downstream APIs", "An API calls several resources with separate identity choices.", "API 1 → API 2 and another resource."),
        new("second-tier", "Two middle-tier APIs", "An additional API continues the delegated user context.", "Web app → API 1 → API 2 → Resource."),
        new("mixed-blazor", "Server and browser Blazor", "Separate server and browser components call a custom API.", "Browser and server → API.")
    ];

    public static ArchitectureScenario Create(string presetId)
    {
        ApplicationComponent Component(string id, string name, ImplementationStack stack, bool? user = null) => new()
        {
            Id = id, Name = name, Stack = Fact<ImplementationStack>.Supplied(stack), Kind = Fact<ComponentKind>.Supplied(ScenarioNormalizer.KindFor(stack)),
            UserSignIn = user.HasValue ? Fact<bool>.Supplied(user.Value) : Fact<bool>.Unknown()
        };
        TargetResource Resource(string id, string name, ResourceCategory category, string? api = null) => new()
        { Id = id, Name = name, Category = Fact<ResourceCategory>.Supplied(category), ApiComponentId = api };
        AccessRelationship Hop(string id, string caller, string target, ActingIdentity? identity = null, string? incoming = null) => new()
        {
            Id = id, CallerComponentId = caller, TargetResourceId = target,
            Identity = identity.HasValue ? Fact<ActingIdentity>.Supplied(identity.Value) : Fact<ActingIdentity>.Unknown(),
            IncomingRelationshipId = incoming is null ? Fact<string>.Unknown() : Fact<string>.Supplied(incoming)
        };
        var web = Component("web", "Web App", ImplementationStack.BlazorServer, true);
        var api = Component("api-a", "API 1", ImplementationStack.AspNetCoreApi);
        var apiB = Component("api-b", "API 2", ImplementationStack.AspNetCoreApi);
        var orders = Resource("orders", "API 1", ResourceCategory.CustomResource, api.Id);
        var inventory = Resource("inventory", "API 2", ResourceCategory.CustomResource, apiB.Id);
        var scenario = new ArchitectureScenario { Id = presetId, SchemaVersion = RuleCatalog.SchemaVersion };
        return presetId switch
        {
            "signin" => scenario with { Components = [web] },
            "client-api" => scenario with { Components = [Component("browser", "Browser App", ImplementationStack.BlazorWebAssembly, true), api], Resources = [orders], Relationships = [Hop("browser-orders", "browser", orders.Id, ActingIdentity.DelegatedUser)] },
            "api-chain" => scenario with { Components = [web, api, apiB], Resources = [orders, inventory], Relationships = [Hop("web-orders", web.Id, orders.Id, ActingIdentity.DelegatedUser), Hop("orders-inventory", api.Id, inventory.Id, ActingIdentity.DelegatedUser, "web-orders")] },
            "api-only" => scenario with { Components = [api] },
            "worker" => scenario with { Components = [Component("worker", "Background Service", ImplementationStack.DotNetWorker, false)], Resources = [Resource("target", "Target Resource", ResourceCategory.AzureResource)], Relationships = [Hop("worker-target", "worker", "target", ActingIdentity.Application)] },
            "device" => scenario with { Components = [Component("cli", "Command-line App", ImplementationStack.DotNetConsole, true)], Resources = [Resource("target", "Target Resource", ResourceCategory.MicrosoftGraph)], Relationships = [Hop("cli-target", "cli", "target", ActingIdentity.DelegatedUser)] },
            "desktop" => scenario with { Components = [Component("desktop", "Windows App", ImplementationStack.WindowsWpf, true)], Resources = [Resource("target", "Target Resource", ResourceCategory.MicrosoftGraph)], Relationships = [Hop("desktop-target", "desktop", "target", ActingIdentity.DelegatedUser)] },
            "fan-out" => scenario with { Components = [web, api, apiB], Resources = [orders, inventory, Resource("graph", "Microsoft Graph", ResourceCategory.MicrosoftGraph)], Relationships = [Hop("web-orders", web.Id, orders.Id, ActingIdentity.DelegatedUser), Hop("orders-inventory", api.Id, inventory.Id), Hop("orders-graph", api.Id, "graph")] },
            "second-tier" => scenario with { Components = [web, api, apiB], Resources = [orders, inventory, Resource("graph", "Microsoft Graph", ResourceCategory.MicrosoftGraph)], Relationships = [Hop("web-orders", web.Id, orders.Id, ActingIdentity.DelegatedUser), Hop("orders-inventory", api.Id, inventory.Id, ActingIdentity.DelegatedUser, "web-orders"), Hop("inventory-graph", apiB.Id, "graph", ActingIdentity.DelegatedUser, "orders-inventory")] },
            "mixed-blazor" => scenario with { Components = [web, Component("browser", "Browser Component", ImplementationStack.BlazorWebAssembly, true), api], Resources = [orders], Relationships = [Hop("web-orders", web.Id, orders.Id, ActingIdentity.DelegatedUser), Hop("browser-orders", "browser", orders.Id, ActingIdentity.DelegatedUser)] },
            _ => throw new ArgumentException("Unknown topology preset.", nameof(presetId))
        };
    }

    internal static ArchitectureScenario SetResourceCategory(ArchitectureScenario scenario, string id, Fact<ResourceCategory> category)
    {
        var resource = scenario.Resources.Single(r => r.Id == id);
        var components = scenario.Components;
        var hops = scenario.Relationships;
        var resources = scenario.Resources;
        string? apiId = resource.ApiComponentId;
        if (category.State == FactState.Known && category.Value != ResourceCategory.CustomResource && apiId is not null)
        {
            var removed = new HashSet<string> { apiId };
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var downstream in resources.Where(r => r.ApiComponentId is not null && !removed.Contains(r.ApiComponentId)))
                {
                    var incoming = hops.Where(h => h.TargetResourceId == downstream.Id).ToArray();
                    if (incoming.Length > 0 && incoming.All(h => removed.Contains(h.CallerComponentId)))
                        changed |= removed.Add(downstream.ApiComponentId!);
                }
            }
            components = components.Where(c => !removed.Contains(c.Id)).ToImmutableArray();
            hops = hops.Where(h => !removed.Contains(h.CallerComponentId)).ToImmutableArray();
            resources = resources.Where(r => r.Id == resource.Id || r.ApiComponentId is null || !removed.Contains(r.ApiComponentId)).ToImmutableArray();
            resources = resources.Where(r => r.Id == resource.Id || hops.Any(h => h.TargetResourceId == r.Id)).ToImmutableArray();
            apiId = null;
        }
        if (category.State == FactState.Known && category.Value == ResourceCategory.CustomResource && apiId is null)
        {
            apiId = resource.Id + "-api";
            if (components.Any(c => c.Id == apiId) || resources.Any(r => r.Id == apiId) || hops.Any(h => h.Id == apiId))
                throw new ArgumentException("The generated custom API identifier conflicts with an existing entity.");
            components = components.Add(new() { Id = apiId, Name = resource.Name + " API", Kind = Fact<ComponentKind>.Supplied(ComponentKind.Api), Stack = Fact<ImplementationStack>.Supplied(ImplementationStack.AspNetCoreApi) });
        }
        return scenario with { Components = components, Relationships = hops, Resources = resources.Replace(resource, resource with { Category = category, ApiComponentId = apiId }) };
    }
}

