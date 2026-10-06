using System.Collections.Immutable;
using EntraAdvisor.Engine.Contracts;
using EntraAdvisor.Engine.Rules;

namespace EntraAdvisor.Engine;

public static class ScenarioNormalizer
{
    public static string Identifier(string value) => value.Trim().ToLowerInvariant();
    private static Fact<T> ClearDerived<T>(Fact<T> fact) => fact.Origin == FactOrigin.Derived ? Fact<T>.Unknown() : fact;
    private static Fact<string> Reference(Fact<string> fact) => fact.State == FactState.Known
        ? Fact<string>.Supplied(Identifier(fact.Value!)) : fact;

    // Called after shape validation. Supplied facts are authoritative; stale derived values are recomputed.
    public static ArchitectureScenario WithoutDerivedFacts(ArchitectureScenario scenario)
    {
        static T Reset<T>(T clone) where T : class
        {
            foreach (var property in typeof(T).GetProperties())
            {
                if (!property.PropertyType.IsGenericType || property.PropertyType.GetGenericTypeDefinition() != typeof(Fact<>)) continue;
                var fact = property.GetValue(clone)!;
                if ((FactOrigin)fact.GetType().GetProperty(nameof(Fact<bool>.Origin))!.GetValue(fact)! == FactOrigin.Derived)
                    property.SetValue(clone, fact.GetType().GetMethod(nameof(Fact<bool>.Unknown))!.Invoke(null, null));
            }
            return clone;
        }
        return scenario with
        {
            Components = scenario.Components.Select(c => Reset(c with { })).ToImmutableArray(),
            Resources = scenario.Resources.Select(r => Reset(r with { })).ToImmutableArray(),
            Relationships = scenario.Relationships.Select(h => Reset(h with { })).ToImmutableArray(),
            Tenants = Reset(scenario.Tenants with { })
        };
    }

    public static ArchitectureScenario Normalize(ArchitectureScenario suppliedScenario)
    {
        var scenario = WithoutDerivedFacts(suppliedScenario);
        return scenario with
    {
        Id = Identifier(scenario.Id),
        Components = scenario.Components.Select(component =>
        {
            var normalized = component with
            {
                Id = Identifier(component.Id), Name = component.Name.Trim(),
                Execution = ClearDerived(component.Execution), CanProtectCredentials = ClearDerived(component.CanProtectCredentials),
                UserSignIn = ClearDerived(component.UserSignIn), HomeTenantReference = Reference(component.HomeTenantReference)
            };
            if (normalized.Stack.State == FactState.Known && normalized.Execution.State == FactState.Unknown)
                normalized = normalized with { Execution = Fact<ExecutionLocation>.Derived(ExecutionFor(normalized.Stack.Value), "classification.execution") };
            if (normalized.Execution.State == FactState.Known && normalized.Execution.Value is ExecutionLocation.Browser or ExecutionLocation.WindowsDesktop && normalized.CanProtectCredentials.State == FactState.Unknown)
                normalized = normalized with { CanProtectCredentials = Fact<bool>.Derived(false, "classification.public") };
            if (normalized.Kind.State == FactState.Known && normalized.Kind.Value is ComponentKind.Api or ComponentKind.BackgroundService && normalized.UserSignIn.State == FactState.Unknown)
                normalized = normalized with { UserSignIn = Fact<bool>.Derived(false, normalized.Kind.Value == ComponentKind.Api ? "api.validation" : "classification.execution") };
            if (normalized.Kind.State == FactState.Known && normalized.Kind.Value == ComponentKind.CliDevice && normalized.CanProtectCredentials.State == FactState.Unknown && normalized.UserSignIn.State == FactState.Known && normalized.UserSignIn.Value)
                normalized = normalized with { CanProtectCredentials = Fact<bool>.Derived(false, "classification.public") };
            return normalized;
        }).OrderBy(component => component.Id, StringComparer.Ordinal).ToImmutableArray(),
        Resources = scenario.Resources.Select(resource => resource with
        {
            Id = Identifier(resource.Id), Name = resource.Name.Trim(),
            ApiComponentId = resource.ApiComponentId is null ? null : Identifier(resource.ApiComponentId),
            ResourceTenantReference = Reference(resource.ResourceTenantReference)
        }).OrderBy(resource => resource.Id, StringComparer.Ordinal).ToImmutableArray(),
        Relationships = scenario.Relationships.Select(hop => hop with
        {
            Id = Identifier(hop.Id), CallerComponentId = Identifier(hop.CallerComponentId), TargetResourceId = Identifier(hop.TargetResourceId),
            IncomingRelationshipId = Reference(hop.IncomingRelationshipId)
        }).OrderBy(hop => hop.Id, StringComparer.Ordinal).ToImmutableArray(),
        SpecializedRequirements = scenario.SpecializedRequirements.Order().ToImmutableArray()
        };
    }

    public static ExecutionLocation ExecutionFor(ImplementationStack stack) => stack switch
    {
        ImplementationStack.BlazorWebAssembly or ImplementationStack.JavaScriptTypeScript => ExecutionLocation.Browser,
        ImplementationStack.WindowsWpf => ExecutionLocation.WindowsDesktop,
        ImplementationStack.DotNetWorker or ImplementationStack.DotNetConsole => ExecutionLocation.Headless,
        _ => ExecutionLocation.Server
    };

    public static ComponentKind KindFor(ImplementationStack stack) => stack switch
    {
        ImplementationStack.BlazorServer => ComponentKind.ServerWeb,
        ImplementationStack.BlazorWebAssembly or ImplementationStack.JavaScriptTypeScript => ComponentKind.BrowserSpa,
        ImplementationStack.AspNetCoreApi => ComponentKind.Api,
        ImplementationStack.WindowsWpf => ComponentKind.WindowsDesktop,
        ImplementationStack.DotNetWorker => ComponentKind.BackgroundService,
        _ => ComponentKind.CliDevice
    };
}
