using System.Text.Json;
using EntraAdvisor.Engine;
using EntraAdvisor.Engine.Contracts;
using EntraAdvisor.Engine.Rules;

namespace EntraAdvisor.Tests;

public sealed class PlanPrerequisiteTests
{
    private readonly ArchitectureEvaluator evaluator = new();

    [Theory]
    [InlineData(ResourceCategory.MicrosoftGraph, PermissionMode.ApplicationPermissions)]
    [InlineData(ResourceCategory.AzureResource, PermissionMode.AzureResourceAuthorization)]
    public void CategoriesUseDeveloperAuthorizationValuesRatherThanACatalog(ResourceCategory category, PermissionMode expected)
    {
        var plan = evaluator.Evaluate(ScenarioExamples.Worker(category)).Plan!;
        var hop = Assert.Single(plan.Relationships);
        Assert.Equal(expected, hop.Authorization.Mode);
        Assert.Contains(plan.Prerequisites, prerequisite => prerequisite.Kind == PrerequisiteKind.ResourceCompatibility);
        Assert.Contains(plan.Prerequisites, prerequisite => prerequisite.Kind == PrerequisiteKind.Assignment);
        Assert.DoesNotContain("User.Read", JsonSerializer.Serialize(plan));
        Assert.DoesNotContain("Contributor", JsonSerializer.Serialize(plan));
    }

    [Fact]
    public void KnownIncompatibleResourceCannotBeReady()
    {
        var scenario = ScenarioExamples.Worker();
        scenario = scenario with { Relationships = [scenario.Relationships[0] with { ResourceCompatibility = Fact<CompatibilityConfirmation>.Supplied(CompatibilityConfirmation.Incompatible) }] };
        var result = evaluator.Evaluate(scenario);
        Assert.Equal(EvaluationStatus.Unsupported, result.Status);
        Assert.Null(result.Plan);
    }

    [Fact]
    public void ManagedIdentityStillRequiresTargetAssignmentAndNoCustomClientRegistration()
    {
        var plan = evaluator.Evaluate(ScenarioExamples.Worker(ResourceCategory.AzureResource, true)).Plan!;
        Assert.Equal(CredentialMechanism.ManagedIdentity, Assert.Single(plan.Relationships).Credential);
        Assert.False(Assert.Single(plan.Registrations).CreateRegistration);
        Assert.Contains(plan.Prerequisites, prerequisite => prerequisite.Id == "worker-target.managed");
        Assert.Contains(plan.Prerequisites, prerequisite => prerequisite.Id == "worker-target.compatibility");
    }

    [Fact]
    public void CrossTenantCertificateAccessAndGuestsCarryExplicitPrerequisites()
    {
        var scenario = ScenarioExamples.Worker();
        scenario = scenario with
        {
            Tenants = scenario.Tenants with { Model = Fact<WorkforceTenantModel>.Supplied(WorkforceTenantModel.Multitenant), IncludesGuestUsers = Fact<bool>.Supplied(true) },
            Relationships = [scenario.Relationships[0] with { TenantBoundary = Fact<TenantBoundary>.Supplied(TenantBoundary.CrossTenant) }]
        };
        var plan = evaluator.Evaluate(scenario).Plan!;
        Assert.Contains(plan.Prerequisites, p => p.Id == "worker-target.cross-tenant");
        Assert.Contains(plan.Prerequisites, p => p.Id == "tenants.multitenant");
        Assert.Contains(plan.Prerequisites, p => p.Id == "tenants.guests");
        Assert.Equal("AzureADMultipleOrgs", Assert.Single(plan.Registrations).AccountAudience);
    }

    [Fact]
    public void ManagedIdentityAcrossTenantsIsAnExplicitMvpBoundary()
    {
        var scenario = ScenarioExamples.Worker(ResourceCategory.AzureResource, true);
        scenario = scenario with
        {
            Relationships = [scenario.Relationships[0] with { TenantBoundary = Fact<TenantBoundary>.Supplied(TenantBoundary.CrossTenant) }],
            Tenants = scenario.Tenants with { Model = Fact<WorkforceTenantModel>.Supplied(WorkforceTenantModel.Multitenant) }
        };
        var result = evaluator.Evaluate(scenario);
        Assert.Equal(EvaluationStatus.Unsupported, result.Status);
        Assert.Contains(result.Issues, issue => issue.Code == "boundary.managed-cross-tenant");
    }

    [Fact]
    public void FederationIsSupportedForAppOnlyButNotAutomaticallyForObo()
    {
        var workload = ScenarioExamples.Worker();
        workload = workload with { Components = [workload.Components[0] with { Credential = Fact<CredentialCapability>.Supplied(CredentialCapability.WorkloadFederation) }] };
        var plan = evaluator.Evaluate(workload).Plan!;
        Assert.Equal(CredentialMechanism.WorkloadFederation, Assert.Single(plan.Relationships).Credential);
        Assert.Contains(plan.Prerequisites, prerequisite => prerequisite.Id == "worker-target.federation");

        var chain = ScenarioExamples.Chain();
        chain = chain with { Components = chain.Components.SetItem(1, chain.Components[1] with { Credential = Fact<CredentialCapability>.Supplied(CredentialCapability.WorkloadFederation) }) };
        Assert.Contains(evaluator.Evaluate(chain).Issues, issue => issue.Code == "boundary.obo-credential");
    }

    [Fact]
    public void EveryDecisionAndTraceHasVersionedReviewedSources()
    {
        var plan = evaluator.Evaluate(ScenarioExamples.Chain()).Plan!;
        Assert.Equal(RuleCatalog.RuleVersion, plan.Versions.Rules);
        Assert.All(plan.Trace, trace => Assert.NotEmpty(RuleCatalog.Get(trace.RuleId).Sources));
        Assert.All(plan.Sources, source =>
        {
            Assert.Equal("learn.microsoft.com", source.Url.Host);
            Assert.Equal(new DateOnly(2026, 10, 5), source.ReviewedOn);
        });
        Assert.Contains(plan.Prerequisites, prerequisite => prerequisite.Id == "orders-inventory.challenge");
        Assert.Contains(plan.Prerequisites, prerequisite => prerequisite.Kind == PrerequisiteKind.Consent);
        Assert.DoesNotContain(plan.Registrations, registration => registration.ComponentId == "graph");
    }
}
