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
            var groups=InstructionGroups.Create(step,plan);
            Assert.Equal(step.Content,groups.SelectMany(g=>g.Actions));
            Assert.All(groups,g=> { Assert.NotEmpty(g.Location); Assert.NotEmpty(g.Title); });
            if(step.Content.OfType<CodeContent>().Any(c=>c.Artifact.Id is "browser-auth" or "blazor-browser-auth" or "browser-settings")) {
                Assert.Equal(3,groups.Length);
                Assert.Equal(new[] { "Entra","Code","Api" },groups.Select(g=>g.System));
                Assert.DoesNotContain(groups[0].Actions,c=>c is CodeContent);
                Assert.Contains(groups[1].Actions,c=>c is CodeContent code && (code.Artifact.DestinationFile=="redirect.html" || code.Artifact.DestinationFile=="wwwroot/appsettings.json" || code.Artifact.DestinationFile=="appsettings.json"));
                Assert.Contains(groups[2].Actions,c=>c is InstructionContent instruction && instruction.Text.Contains("Authorization: Bearer"));
                Assert.Contains("using an access token issued for that API",step.ExpectedResult);
            }
        }
        var markdown=new MarkdownGuideExporter().ExportMarkdown(guide);
        Assert.All(guide.Steps,step=>Assert.Contains(step.Title,markdown));
        Assert.All(plan.Relationships,hop=>Assert.Contains(guide.Steps,s=>s.RelatedRelationshipIds.Contains(hop.RelationshipId)));
        Assert.All(plan.Registrations,registration=>Assert.Contains(guide.Steps,s=>s.Id=="register-"+registration.ComponentId || s.Id=="register-web"));
        Assert.All(guide.Steps.Where(s=>s.Id.StartsWith("register-")),step=>Assert.DoesNotContain(step.Content.OfType<CopyableValueContent>(),v=>v.Value.Label.Contains("client ID")));
        Assert.All(guide.Steps.SelectMany(s=>s.Content).OfType<CopyableValueContent>().Where(v=>v.Value.Key.EndsWith(".clientId")), value => {
            Assert.True(value.Value.ReferenceOnly);
            Assert.False(value.Value.CanCopy);
            Assert.DoesNotContain(value.Value.Value,markdown);
        });
        Assert.All(plan.Registrations.Where(r=>r.CreateRegistration),registration=>Assert.Contains(guide.Steps.Single(s=>s.Id=="configure-"+registration.ComponentId).Content.OfType<CopyableValueContent>(),v=>v.Value.Guidance.Contains("Directory (tenant) ID")));
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
        Assert.Contains("Enable the selected managed identity",text);
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
