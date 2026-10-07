using EntraAdvisor.Web.Features.ImplementationJourney;
using EntraAdvisor.Web.State;

namespace EntraAdvisor.Tests;

public sealed class GenerateGuideTests
{
    [Fact]
    public void Incomplete_architecture_cannot_create_a_guide()
    {
        var workspace = new AdvisorWorkspace();
        Assert.Throws<InvalidOperationException>(() => new GenerateGuide(workspace).Execute());
        Assert.Null(workspace.Guide);
        Assert.Equal(JourneyStage.Design, workspace.Stage);
    }

    [Fact]
    public void Regeneration_preserves_completion_for_the_same_validated_plan()
    {
        var workspace = new AdvisorWorkspace();
        workspace.ChoosePreset("client-api");
        workspace.Apply(new("tenants.model", ["SingleTenant"]));
        workspace.Apply(new("tenants.includesguestusers", ["False"]));
        workspace.Apply(new("browser-orders.tenantboundary", ["SameTenant"]));
        var workflow = new GenerateGuide(workspace);
        workflow.Execute();
        Assert.True(workspace.GuideIsCurrent);
        Assert.Equal(JourneyStage.Implement, workspace.Stage);
        var planId = workspace.Guide!.PlanId;
        workspace.SetCompletion(EntraAdvisor.Guide.Contracts.CompletionState.Complete);
        workflow.Execute();
        Assert.Equal(planId, workspace.Guide!.PlanId);
        Assert.Equal(EntraAdvisor.Guide.Contracts.CompletionState.Complete, workspace.Completion[0].State);
    }
}
