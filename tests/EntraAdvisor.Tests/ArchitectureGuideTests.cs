using EntraAdvisor.Engine;
using EntraAdvisor.Engine.Contracts;
using EntraAdvisor.Engine.Questionnaire;
using EntraAdvisor.Guide;
using EntraAdvisor.Guide.Contracts;

namespace EntraAdvisor.Tests;

public sealed class ArchitectureGuideTests
{
    public static TheoryData<string> Presets => new(TopologyPresets.All.Select(p=>p.Id));
    [Theory]
    [MemberData(nameof(Presets))]
    public void EveryReadyPresetHasOrderedExportableImplementationSteps(string preset)
    {
        var session=QuestionnaireSession.Create(TopologyPresets.Create(preset));
        for(var i=0;i<70 && session.NextScreen is {} screen;i++) {
            var q=screen.Questions.Single();
            var value=q.SuppliesFact.FactName switch {
                "Model"=>"SingleTenant", "IncludesGuestUsers" or "ManagedIdentityAvailable"=>"False",
                "CanProtectCredentials" or "LocalBrowserAvailable" or "AlternateBrowserAvailable" or "DeviceCodePermitted"=>"True",
                "Hosting"=>"NonAzure", "Credential"=>"Certificate", "IncomingIdentity" or "Identity"=>"DelegatedUser",
                "TenantBoundary"=>"SameTenant", _=>q.Options[0].Id
            };
            session=session.Apply(new(q.Id,[value])).Session;
        }
        var plan=Assert.IsType<OAuthPlan>(session.Evaluation.Plan);
        var guide=new ArchitectureGuideGenerator().Generate(plan,new([]));
        Assert.Equal(plan.Id,guide.PlanId);
        Assert.Same(plan,guide.Architecture);
        Assert.NotEmpty(guide.Steps);
        var seen=new HashSet<string>();
        foreach(var step in guide.Steps) {
            Assert.All(step.DependsOnStepIds,id=>Assert.Contains(id,seen));
            Assert.True(seen.Add(step.Id));
            Assert.NotEmpty(step.Content);Assert.NotEmpty(step.Sources);
        }
        var markdown=new MarkdownGuideExporter().ExportMarkdown(guide);
        Assert.All(guide.Steps,step=>Assert.Contains(step.Title,markdown));
        Assert.All(plan.Relationships,hop=>Assert.Contains(guide.Steps,s=>s.RelatedRelationshipIds.Contains(hop.RelationshipId)));
        Assert.All(plan.Registrations,registration=>Assert.Contains(guide.Steps,s=>s.Id=="register-"+registration.ComponentId || s.Id=="register-web"));
    }
    [Fact]
    public void AppOnlyWorkerUsesApplicationAccessWithoutUserSignIn()
    {
        var plan=new ArchitectureEvaluator().Evaluate(ScenarioExamples.Worker()).Plan!;
        var guide=new ArchitectureGuideGenerator().Generate(plan,new([]));
        var text=new MarkdownGuideExporter().ExportMarkdown(guide);
        Assert.Contains("Acquire a token as the application",text);
        Assert.Contains("/.default",text);
        Assert.DoesNotContain("Configure the sign-in method",text);
        Assert.Contains(guide.Sources,s=>s.Url==ArchitectureGuideGenerator.FlowSource(TokenAcquisition.ClientCredentials).Url);
    }
    [Fact]
    public void ManagedIdentityGuideDoesNotCreateCallerRegistration()
    {
        var plan=new ArchitectureEvaluator().Evaluate(ScenarioExamples.Worker(managed:true)).Plan!;
        var guide=new ArchitectureGuideGenerator().Generate(plan,new([]));
        var text=new MarkdownGuideExporter().ExportMarkdown(guide);
        Assert.Contains("Use the host-provided managed identity",text);
        Assert.DoesNotContain("Create a new registration named",text);
        Assert.Contains("Managed identity has no caller app registration",text);
    }
    [Fact]
    public void MultitenantGuestGuideUsesOrganizationalRegistrationAndIssuerChecks()
    {
        var scenario=ScenarioExamples.Chain();
        scenario=scenario with { Tenants=scenario.Tenants with { Model=Fact<WorkforceTenantModel>.Supplied(WorkforceTenantModel.Multitenant), IncludesGuestUsers=Fact<bool>.Supplied(true) } };
        var plan=new ArchitectureEvaluator().Evaluate(scenario).Plan!;
        var text=new MarkdownGuideExporter().ExportMarkdown(new ArchitectureGuideGenerator().Generate(plan,new([])));
        Assert.Contains("Accounts in any organizational directory",text);
        Assert.Contains("Do not disable issuer validation",text);
        Assert.Contains("sign the guest in using the applicable resource tenant",text);
    }
    [Fact]
    public void ForgedPlanCannotGenerateSetupInstructions()
    {
        var plan=new ArchitectureEvaluator().Evaluate(ScenarioExamples.Worker()).Plan!;
        Assert.Throws<ArgumentException>(()=>new ArchitectureGuideGenerator().Generate(plan with { Registrations=[] },new([])));
    }
}
