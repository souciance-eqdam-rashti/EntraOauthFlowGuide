using System.Text.Json;
using System.Collections.Immutable;
using EntraAdvisor.Engine;
using EntraAdvisor.Engine.Contracts;
using EntraAdvisor.Engine.Rules;

namespace EntraAdvisor.Tests;

public sealed class ArchitectureEvaluatorTests
{
    private readonly ArchitectureEvaluator evaluator = new();

    public static TheoryData<string, ArchitectureScenario, TokenAcquisition?> Supported => new()
    {
        { "web sign-in only", ScenarioExamples.Scenario([ScenarioExamples.Component("web", ImplementationStack.BlazorServer, true)]), null },
        { "SPA custom API", ScenarioExamples.Scenario([ScenarioExamples.Component("spa", ImplementationStack.BlazorWebAssembly, true), ScenarioExamples.Component("api", ImplementationStack.AspNetCoreApi)],
            [ScenarioExamples.Resource("custom", ResourceCategory.CustomResource, "api")], [ScenarioExamples.Hop("spa-custom", "spa", "custom")]), TokenAcquisition.AuthorizationCode },
        { "delegated chain", ScenarioExamples.Chain(), TokenAcquisition.OnBehalfOf },
        { "application chain", ScenarioExamples.Chain(ActingIdentity.Application), TokenAcquisition.ClientCredentials },
        { "managed worker", ScenarioExamples.Worker(ResourceCategory.AzureResource, true), TokenAcquisition.ClientCredentials },
        { "certificate worker", ScenarioExamples.Worker(), TokenAcquisition.ClientCredentials },
        { "device code", ScenarioExamples.Device(), TokenAcquisition.DeviceCode },
        { "API validation only", ScenarioExamples.Scenario([ScenarioExamples.Component("api", ImplementationStack.AspNetCoreApi) with { IncomingIdentity = Fact<IncomingTokenIdentity>.Supplied(IncomingTokenIdentity.DelegatedUser) }]), null },
        { "Windows desktop", ScenarioExamples.Scenario([ScenarioExamples.Component("desktop", ImplementationStack.WindowsWpf, true)],
            [ScenarioExamples.Resource("graph", ResourceCategory.MicrosoftGraph)], [ScenarioExamples.Hop("desktop-graph", "desktop", "graph")]), TokenAcquisition.InteractivePublicClient },
        { "JavaScript", ScenarioExamples.Scenario([ScenarioExamples.Component("spa", ImplementationStack.JavaScriptTypeScript, true)],
            [ScenarioExamples.Resource("graph", ResourceCategory.MicrosoftGraph)], [ScenarioExamples.Hop("spa-graph", "spa", "graph")]), TokenAcquisition.AuthorizationCode }
    };

    [Theory]
    [MemberData(nameof(Supported))]
    public void SupportedFamiliesProducePerHopPlans(string name, ArchitectureScenario scenario, TokenAcquisition? expected)
    {
        var result = evaluator.Evaluate(scenario);
        Assert.True(result.Status == EvaluationStatus.Ready, $"{name}: {JsonSerializer.Serialize(result.Issues)}");
        if (expected is not null) Assert.Contains(result.Plan!.Relationships, decision => decision.Acquisition == expected);
        foreach (var hop in scenario.Relationships)
            Assert.Single(result.Plan!.Relationships, decision => decision.RelationshipId == hop.Id);
    }

    [Fact]
    public void WebSignInAloneDoesNotCreateApiPermissions()
    {
        var plan = evaluator.Evaluate(ScenarioExamples.Scenario([ScenarioExamples.Component("web", ImplementationStack.BlazorServer, true)])).Plan!;
        Assert.Empty(plan.Relationships);
        Assert.Empty(plan.ApiValidation);
        Assert.Single(plan.Registrations);
        Assert.Equal(SignInApproach.OpenIdConnectAuthorizationCode, Assert.Single(plan.Components).SignIn);
    }

    [Fact]
    public void ApplicationDownstreamIsNotOboEvenWhenAUserStartedTheRequest()
    {
        var plan = evaluator.Evaluate(ScenarioExamples.Chain(ActingIdentity.Application)).Plan!;
        var downstream = plan.Relationships.Single(h => h.RelationshipId == "orders-inventory");
        Assert.Equal(ActingIdentity.Application, downstream.Identity);
        Assert.Equal(TokenAcquisition.ClientCredentials, downstream.Acquisition);
        Assert.Equal(PermissionMode.ApplicationPermissions, downstream.Authorization.Mode);
        Assert.NotEqual(plan.ApiValidation.Single(api => api.ComponentId == "api-a").AudienceValueKey, downstream.AudienceValueKey);
    }

    [Fact]
    public void ApiValidationAloneNeverAcquiresDownstreamTokens()
    {
        var plan = evaluator.Evaluate(ScenarioExamples.Scenario([ScenarioExamples.Component("api", ImplementationStack.AspNetCoreApi) with { IncomingIdentity = Fact<IncomingTokenIdentity>.Supplied(IncomingTokenIdentity.DelegatedUser) }])).Plan!;
        Assert.Empty(plan.Relationships);
        Assert.Single(plan.ApiValidation);
        Assert.True(Assert.Single(plan.Components).ValidatesIncomingTokens);
        Assert.Equal(ClientClassification.NotApplicable, Assert.Single(plan.Components).Client);
    }

    [Fact]
    public void UnknownIdentityReturnsClarificationWithoutAPlan()
    {
        var scenario = ScenarioExamples.Chain();
        scenario = scenario with { Relationships = scenario.Relationships.SetItem(1, scenario.Relationships[1] with { Identity = Fact<ActingIdentity>.Unknown() }) };
        var result = evaluator.Evaluate(scenario);
        Assert.Equal(EvaluationStatus.NeedsClarification, result.Status);
        Assert.Null(result.Plan);
        Assert.Contains("orders-inventory.identity", result.NextQuestionIds);
    }

    [Theory]
    [InlineData(false, true, "boundary.device-browser")]
    [InlineData(true, false, "boundary.device-policy")]
    public void DeviceCodeRequiresBothABrowserAndPolicy(bool browser, bool permitted, string code)
    {
        var scenario = ScenarioExamples.Device();
        scenario = scenario with { Components = [scenario.Components[0] with { AlternateBrowserAvailable = Fact<bool>.Supplied(browser), DeviceCodePermitted = Fact<bool>.Supplied(permitted) }] };
        var result = evaluator.Evaluate(scenario);
        Assert.Equal(EvaluationStatus.Unsupported, result.Status);
        Assert.Contains(result.Issues, issue => issue.Code == code);
    }

    [Fact]
    public void ObORejectsAnIncomingApplicationToken()
    {
        var scenario = ScenarioExamples.Chain();
        scenario = scenario with { Relationships = scenario.Relationships.SetItem(0, scenario.Relationships[0] with { Identity = Fact<ActingIdentity>.Supplied(ActingIdentity.Application) }) };
        var result = evaluator.Evaluate(scenario);
        Assert.Equal(EvaluationStatus.Invalid, result.Status);
        Assert.Contains(result.Issues, issue => issue.Code == "input.obo-app-token");
    }

    [Theory]
    [InlineData(ImplementationStack.BlazorWebAssembly)]
    [InlineData(ImplementationStack.JavaScriptTypeScript)]
    [InlineData(ImplementationStack.WindowsWpf)]
    public void PublicClientsCannotUseAppOnlyCredentials(ImplementationStack stack)
    {
        var scenario = ScenarioExamples.Scenario([ScenarioExamples.Component("client", stack, true)],
            [ScenarioExamples.Resource("graph", ResourceCategory.MicrosoftGraph)], [ScenarioExamples.Hop("client-graph", "client", "graph", ActingIdentity.Application)]);
        Assert.Equal(EvaluationStatus.Invalid, evaluator.Evaluate(scenario).Status);
    }

    [Fact]
    public void BrowserNeverReceivesCredentialsAndHasPkce()
    {
        var scenario = Supported.Single(row => (string)row[0] == "JavaScript")[1] as ArchitectureScenario;
        var plan = evaluator.Evaluate(scenario!).Plan!;
        Assert.Equal(ClientClassification.Public, Assert.Single(plan.Components).Client);
        Assert.True(Assert.Single(plan.Components).UsesPkce);
        Assert.Equal(CredentialMechanism.None, Assert.Single(plan.Relationships).Credential);
    }

    [Fact]
    public void ContradictoryBlazorExecutionIsInvalid()
    {
        var component = ScenarioExamples.Component("blazor", ImplementationStack.BlazorServer, true) with { Execution = Fact<ExecutionLocation>.Supplied(ExecutionLocation.Browser) };
        var result = evaluator.Evaluate(ScenarioExamples.Scenario([component]));
        Assert.Equal(EvaluationStatus.Invalid, result.Status);
        Assert.Contains("blazor.execution", result.NextQuestionIds);
    }

    [Fact]
    public void InvalidReferencesAreRejectedBeforeUnsupportedBoundaries()
    {
        var scenario = ScenarioExamples.Worker() with { SpecializedRequirements = [SpecializedRequirement.ThirdPartyNonEntra] };
        scenario = scenario with { Relationships = [scenario.Relationships[0] with { TargetResourceId = "missing" }] };
        Assert.Equal(EvaluationStatus.Invalid, evaluator.Evaluate(scenario).Status);
    }

    [Theory]
    [InlineData(IdentityDomain.Consumer)]
    [InlineData(IdentityDomain.ExternalIdCustomers)]
    [InlineData(IdentityDomain.SovereignCloud)]
    [InlineData(IdentityDomain.SpecializedAgent)]
    public void UnsupportedIdentityDomainsNeverProduceAPlan(IdentityDomain domain)
    {
        var scenario = ScenarioExamples.Worker();
        scenario = scenario with { Tenants = scenario.Tenants with { Domain = Fact<IdentityDomain>.Supplied(domain) } };
        var result = evaluator.Evaluate(scenario);
        Assert.Equal(EvaluationStatus.Unsupported, result.Status);
        Assert.Null(result.Plan);
    }

    [Fact]
    public void EquivalentNormalizedInputsProduceByteIdenticalPlans()
    {
        var scenario = ScenarioExamples.Chain();
        var reordered = scenario with { Components = scenario.Components.Reverse().ToImmutableArray(), Resources = scenario.Resources.Reverse().ToImmutableArray(), Relationships = scenario.Relationships.Reverse().ToImmutableArray() };
        Assert.Equal(JsonSerializer.Serialize(evaluator.Evaluate(scenario).Plan), JsonSerializer.Serialize(evaluator.Evaluate(reordered).Plan));
    }

    [Fact]
    public void CyclesAndDuplicateNormalizedIdsAreInvalid()
    {
        var scenario = ScenarioExamples.Chain();
        var cycle = scenario with { Relationships = scenario.Relationships.Add(ScenarioExamples.Hop("reverse", "api-b", "orders", ActingIdentity.Application)) };
        Assert.Contains(evaluator.Evaluate(cycle).Issues, issue => issue.Code == "input.cycle");
        var duplicate = scenario with { Components = scenario.Components.Add(scenario.Components[0] with { Id = " WEB " }) };
        Assert.Contains(evaluator.Evaluate(duplicate).Issues, issue => issue.Code == "input.duplicate");
    }
}
