namespace EntraAdvisor.Engine.Contracts;

public enum FactState { Unknown, Known, NotApplicable }
public enum FactOrigin { Supplied, Derived }

/// <summary>An unanswered fact is distinct from a supplied false or inapplicable fact.</summary>
public sealed record Fact<T>
{
    public FactState State { get; }
    public FactOrigin Origin { get; }
    public T? Value { get; }
    public string? DerivedByRuleId { get; }

    private Fact(FactState state, FactOrigin origin, T? value, string? ruleId)
    {
        State = state;
        Origin = origin;
        Value = value;
        DerivedByRuleId = ruleId;
    }

    public static Fact<T> Unknown() => new(FactState.Unknown, FactOrigin.Supplied, default, null);
    public static Fact<T> NotApplicable() => new(FactState.NotApplicable, FactOrigin.Supplied, default, null);
    public static Fact<T> Supplied(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(FactState.Known, FactOrigin.Supplied, value, null);
    }

    public static Fact<T> Derived(T value, string ruleId)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        return new(FactState.Known, FactOrigin.Derived, value, ruleId);
    }

    public bool TryGetValue(out T? value)
    {
        value = Value;
        return State == FactState.Known;
    }
}

public enum ComponentKind { ServerWeb, BrowserSpa, Api, BackgroundService, WindowsDesktop, CliDevice }
public enum ExecutionLocation { Server, Browser, WindowsDesktop, Headless }
public enum ImplementationStack { BlazorServer, BlazorWebAssembly, AspNetCoreApi, JavaScriptTypeScript, WindowsWpf, DotNetWorker, DotNetConsole }
public enum HostingEnvironment { Azure, NonAzure, LocalDevelopment }
public enum ResourceCategory { MicrosoftGraph, AzureResource, CustomResource }
public enum ActingIdentity { DelegatedUser, Application }
public enum IncomingTokenIdentity { DelegatedUser, Application, Both }
public enum WorkforceTenantModel { SingleTenant, Multitenant }
public enum TenantBoundary { SameTenant, CrossTenant }
public enum CredentialCapability { Certificate, WorkloadFederation, ManagedIdentity }
public enum CompatibilityConfirmation { Confirmed, Incompatible, RequiresDeveloperVerification }
public enum IdentityDomain { Workforce, Consumer, ExternalIdCustomers, SovereignCloud, SpecializedAgent }
public enum SpecializedRequirement { ThirdPartyNonEntra, Implicit, ResourceOwnerPassword, IntegratedWindows }

public static class ArchitectureLimits
{
    public const int MaxApplicationComponents = 5;
    public const int MaxAccessRelationships = 8;
}
