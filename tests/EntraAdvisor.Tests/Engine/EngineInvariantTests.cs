using EntraAdvisor.Engine;
using EntraAdvisor.Engine.Contracts;
using EntraAdvisor.Engine.Questionnaire;
using EntraAdvisor.Engine.Rules;

namespace EntraAdvisor.Tests;

public sealed class EngineInvariantTests
{
    private readonly ArchitectureEvaluator evaluator = new();

    [Fact]
    public void RemovingAnyRequiredArchitectureFactNeverLeavesAReadyPlan()
    {
        var scenario = ScenarioExamples.Chain();
        FactReference[] required =
        [new("web", "Kind"), new("web", "Stack"), new("web", "UserSignIn"), new("web", "CanProtectCredentials"), new("web", "Credential"),
            new("web-orders", "Identity"), new("web-orders", "TenantBoundary"), new("tenants", "Domain"), new("tenants", "Model"), new("tenants", "IncludesGuestUsers")];
        foreach (var reference in required)
        {
            var web = scenario.Components.Single(c => c.Id == "web");
            var hop = scenario.Relationships.Single(h => h.Id == "web-orders");
            var changed = reference.EntityId switch
            {
                "web" => scenario with { Components = scenario.Components.Replace(web, reference.FactName switch
                {
                    "Kind" => web with { Kind = Fact<ComponentKind>.Unknown(), Stack = Fact<ImplementationStack>.Unknown() },
                    "Stack" => web with { Stack = Fact<ImplementationStack>.Unknown() },
                    "UserSignIn" => web with { UserSignIn = Fact<bool>.Unknown() },
                    "CanProtectCredentials" => web with { CanProtectCredentials = Fact<bool>.Unknown() },
                    _ => web with { Credential = Fact<CredentialCapability>.Unknown() }
                }) },
                "web-orders" => scenario with { Relationships = scenario.Relationships.Replace(hop, reference.FactName == "Identity"
                    ? hop with { Identity = Fact<ActingIdentity>.Unknown() } : hop with { TenantBoundary = Fact<TenantBoundary>.Unknown() }) },
                _ => scenario with { Tenants = reference.FactName switch
                {
                    "Domain" => scenario.Tenants with { Domain = Fact<IdentityDomain>.Unknown() },
                    "Model" => scenario.Tenants with { Model = Fact<WorkforceTenantModel>.Unknown() },
                    _ => scenario.Tenants with { IncludesGuestUsers = Fact<bool>.Unknown() }
                } }
            };
            var result = evaluator.Evaluate(changed);
            Assert.NotEqual(EvaluationStatus.Ready, result.Status);
            Assert.Null(result.Plan);
        }
    }

    [Fact]
    public void StaleDerivedSecurityFactsAreNeverTrusted()
    {
        var scenario = ScenarioExamples.Worker();
        scenario = scenario with { Relationships = [scenario.Relationships[0] with { Identity = Fact<ActingIdentity>.Derived(ActingIdentity.Application, "hop.application") }] };
        Assert.Equal(EvaluationStatus.NeedsClarification, evaluator.Evaluate(scenario).Status);
    }

    [Fact]
    public void ApiWithAnExternalUserCallerCanUseOboButNotApplicationContext()
    {
        var api = ScenarioExamples.Component("api", ImplementationStack.AspNetCoreApi) with { IncomingIdentity = Fact<IncomingTokenIdentity>.Supplied(IncomingTokenIdentity.DelegatedUser) };
        var scenario = ScenarioExamples.Scenario([api], [ScenarioExamples.Resource("graph", ResourceCategory.MicrosoftGraph)],
            [ScenarioExamples.Hop("api-graph", "api", "graph", incoming: AccessRelationship.ExternalUserToken)]);
        var plan = evaluator.Evaluate(scenario).Plan!;
        Assert.Equal(TokenAcquisition.OnBehalfOf, Assert.Single(plan.Relationships).Acquisition);
        Assert.Contains(plan.Prerequisites, prerequisite => prerequisite.Id == "api-graph.external-user");
        var appOnly = scenario with { Components = [api with { IncomingIdentity = Fact<IncomingTokenIdentity>.Supplied(IncomingTokenIdentity.Application) }] };
        Assert.Equal(EvaluationStatus.Invalid, evaluator.Evaluate(appOnly).Status);
    }

    [Fact]
    public void ExplicitBothIncomingModesRequireBothAuthorizationChecks()
    {
        var scenario = ScenarioExamples.Chain();
        scenario = scenario with { Components = scenario.Components.SetItem(1, scenario.Components[1] with { IncomingIdentity = Fact<IncomingTokenIdentity>.Supplied(IncomingTokenIdentity.Both) }) };
        var plan = evaluator.Evaluate(scenario).Plan!;
        var requirements = plan.ApiValidation.Single(api => api.ComponentId == "api-a").Authorization;
        Assert.Contains(requirements, requirement => requirement.Mode == PermissionMode.DelegatedScopes);
        Assert.Contains(requirements, requirement => requirement.Mode == PermissionMode.ApplicationPermissions);
    }

    [Fact]
    public void AGraphDownstreamHasNoInventedResourceRegistration()
    {
        var scenario = ScenarioExamples.Chain();
        scenario = scenario with
        {
            Components = scenario.Components.RemoveAt(2),
            Resources = scenario.Resources.SetItem(1, ScenarioExamples.Resource("inventory", ResourceCategory.MicrosoftGraph))
        };
        var plan = evaluator.Evaluate(scenario).Plan!;
        Assert.Equal(2, plan.Registrations.Length);
        Assert.Equal(TokenAcquisition.OnBehalfOf, plan.Relationships.Single(hop => hop.RelationshipId == "orders-inventory").Acquisition);
    }

    [Fact]
    public void FanOutDecisionsKeepApplicationAndUserPermissionsSeparate()
    {
        var scenario = ScenarioExamples.Chain();
        scenario = scenario with
        {
            Resources = scenario.Resources.Add(ScenarioExamples.Resource("graph", ResourceCategory.MicrosoftGraph)),
            Relationships = scenario.Relationships.Add(ScenarioExamples.Hop("orders-graph", "api-a", "graph", ActingIdentity.Application))
        };
        var plan = evaluator.Evaluate(scenario).Plan!;
        Assert.Equal(TokenAcquisition.OnBehalfOf, plan.Relationships.Single(hop => hop.RelationshipId == "orders-inventory").Acquisition);
        Assert.Equal(TokenAcquisition.ClientCredentials, plan.Relationships.Single(hop => hop.RelationshipId == "orders-graph").Acquisition);
        Assert.Equal(PermissionMode.ApplicationPermissions, plan.Relationships.Single(hop => hop.RelationshipId == "orders-graph").Authorization.Mode);
    }

    [Fact]
    public void CrossTenantAudienceCannotRemainSingleTenant()
    {
        var scenario = ScenarioExamples.Worker();
        scenario = scenario with { Relationships = [scenario.Relationships[0] with { TenantBoundary = Fact<TenantBoundary>.Supplied(TenantBoundary.CrossTenant) }] };
        var result = evaluator.Evaluate(scenario);
        Assert.Equal(EvaluationStatus.Invalid, result.Status);
        Assert.Contains("tenants.model", result.NextQuestionIds);
    }

    [Fact]
    public void LargerArchitecturesReturnABoundaryWithoutTruncating()
    {
        var scenario = ScenarioExamples.Worker();
        scenario = scenario with { Components = scenario.Components.AddRange(Enumerable.Range(1, 5).Select(index => ScenarioExamples.Component("worker-" + index, ImplementationStack.DotNetWorker))) };
        Assert.Equal(EvaluationStatus.Unsupported, evaluator.Evaluate(scenario).Status);
    }

    [Fact]
    public void MalformedFactsAndReferencesReturnInvalidInsteadOfThrowing()
    {
        var scenario = ScenarioExamples.Worker();
        Assert.Equal(EvaluationStatus.Invalid, evaluator.Evaluate(scenario with { Components = default }).Status);
        var invalid = scenario with { Components = [scenario.Components[0] with { Kind = Fact<ComponentKind>.Supplied((ComponentKind)999) }] };
        Assert.Equal(EvaluationStatus.Invalid, evaluator.Evaluate(invalid).Status);
        invalid = scenario with { Components = [scenario.Components[0] with { Credential = null! }] };
        Assert.Equal(EvaluationStatus.Invalid, evaluator.Evaluate(invalid).Status);
    }

    [Fact]
    public void RuleMetadataHasUniqueIdsAndExplicitPrecedence()
    {
        Assert.Equal(RuleCatalog.All.Length, RuleCatalog.All.Select(rule => rule.Id).Distinct().Count());
        Assert.All(RuleCatalog.All, rule =>
        {
            Assert.NotEmpty(rule.Rationale);
            Assert.NotEmpty(rule.Sources);
            Assert.True(Enum.IsDefined(rule.Phase));
        });
    }
}

