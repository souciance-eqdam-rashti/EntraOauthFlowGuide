using System.Reflection;
using EntraAdvisor.Engine.Contracts;

namespace EntraAdvisor.Engine.Questionnaire;

public sealed record FactView(FactState State, string? Value, FactOrigin Origin);

public static class ScenarioFacts
{
    public static FactView Read(ArchitectureScenario scenario, FactReference reference)
    {
        var entity = Entity(scenario, reference.EntityId);
        var property = entity.GetType().GetProperty(reference.FactName);
        if (property is null || !property.PropertyType.IsGenericType || property.PropertyType.GetGenericTypeDefinition() != typeof(Fact<>))
            throw new ArgumentException("The question does not reference a typed architecture fact.", nameof(reference));
        var fact = property.GetValue(entity)!;
        var type = fact.GetType();
        var state = (FactState)type.GetProperty(nameof(Fact<bool>.State))!.GetValue(fact)!;
        return new(state, state == FactState.Known ? type.GetProperty(nameof(Fact<bool>.Value))!.GetValue(fact)?.ToString() : null,
            (FactOrigin)type.GetProperty(nameof(Fact<bool>.Origin))!.GetValue(fact)!);
    }

    internal static object Entity(ArchitectureScenario scenario, string id)
    {
        if (id == "tenants") return scenario.Tenants;
        return (object?)scenario.Components.FirstOrDefault(c => c.Id == id) ?? (object?)scenario.Resources.FirstOrDefault(r => r.Id == id) ??
            scenario.Relationships.FirstOrDefault(h => h.Id == id) ?? throw new ArgumentException($"Unknown architecture entity '{id}'.");
    }

    internal static ArchitectureScenario Write(ArchitectureScenario scenario, FactReference reference, string? value)
    {
        if (reference.FactName == nameof(TargetResource.Category))
            return TopologyPresets.SetResourceCategory(scenario, reference.EntityId, Parse<ResourceCategory>(value));
        if (reference.EntityId == "tenants")
            return scenario with { Tenants = reference.FactName switch
            {
                nameof(WorkforceTenantContext.Domain) => scenario.Tenants with { Domain = Parse<IdentityDomain>(value) },
                nameof(WorkforceTenantContext.Model) => scenario.Tenants with { Model = Parse<WorkforceTenantModel>(value) },
                nameof(WorkforceTenantContext.IncludesGuestUsers) => scenario.Tenants with { IncludesGuestUsers = Parse<bool>(value) },
                _ => throw new ArgumentException("Unknown tenant fact.")
            } };
        var component = scenario.Components.FirstOrDefault(c => c.Id == reference.EntityId);
        if (component is not null)
        {
            var updated = reference.FactName switch
            {
                nameof(ApplicationComponent.Kind) => component with { Kind = Parse<ComponentKind>(value) },
                nameof(ApplicationComponent.Stack) => component with { Stack = Parse<ImplementationStack>(value) },
                nameof(ApplicationComponent.Execution) => component with { Execution = Parse<ExecutionLocation>(value) },
                nameof(ApplicationComponent.UserSignIn) => component with { UserSignIn = Parse<bool>(value) },
                nameof(ApplicationComponent.CanProtectCredentials) => component with { CanProtectCredentials = Parse<bool>(value) },
                nameof(ApplicationComponent.LocalBrowserAvailable) => component with { LocalBrowserAvailable = Parse<bool>(value) },
                nameof(ApplicationComponent.AlternateBrowserAvailable) => component with { AlternateBrowserAvailable = Parse<bool>(value) },
                nameof(ApplicationComponent.DeviceCodePermitted) => component with { DeviceCodePermitted = Parse<bool>(value) },
                nameof(ApplicationComponent.Hosting) => component with { Hosting = Parse<HostingEnvironment>(value) },
                nameof(ApplicationComponent.ManagedIdentityAvailable) => component with { ManagedIdentityAvailable = Parse<bool>(value) },
                nameof(ApplicationComponent.IncomingIdentity) => component with { IncomingIdentity = Parse<IncomingTokenIdentity>(value) },
                nameof(ApplicationComponent.Credential) => component with { Credential = Parse<CredentialCapability>(value) },
                _ => throw new ArgumentException("Unknown component fact.")
            };
            return scenario with { Components = scenario.Components.Replace(component, updated) };
        }
        var hop = scenario.Relationships.Single(h => h.Id == reference.EntityId);
        return scenario with { Relationships = scenario.Relationships.Replace(hop, reference.FactName switch
        {
            nameof(AccessRelationship.Identity) => hop with { Identity = Parse<ActingIdentity>(value) },
            nameof(AccessRelationship.TenantBoundary) => hop with { TenantBoundary = Parse<TenantBoundary>(value) },
            nameof(AccessRelationship.IncomingRelationshipId) => hop with { IncomingRelationshipId = value is null ? Fact<string>.Unknown() : Fact<string>.Supplied(value) },
            _ => throw new ArgumentException("Unknown connection fact.")
        }) };
    }

    private static Fact<T> Parse<T>(string? value)
    {
        if (value is null) return Fact<T>.Unknown();
        object parsed;
        if (typeof(T) == typeof(bool)) parsed = bool.Parse(value);
        else if (typeof(T).IsEnum)
        {
            parsed = Enum.Parse(typeof(T), value);
            if (!Enum.IsDefined(typeof(T), parsed)) throw new ArgumentException("Unknown choice.");
        }
        else throw new ArgumentException("Unsupported fact value type.");
        return Fact<T>.Supplied((T)parsed);
    }
}
