using EntraAdvisor.Engine;
using EntraAdvisor.Engine.Contracts;
using EntraAdvisor.Guide;
using EntraAdvisor.Guide.Contracts;
namespace EntraAdvisor.Tests;
public sealed class GuideAccessCopyTests
{
    [Theory]
    [InlineData(true,false,"Use scopes:")]
    [InlineData(false,true,"Use app roles:")]
    [InlineData(true,true,"Use both:")]
    public void AccessStepExplainsOnlyTheIdentityModesActuallyAccepted(bool users,bool applications,string explanation)
    {
        var components=new List<ApplicationComponent>{ScenarioExamples.Component("api",ImplementationStack.AspNetCoreApi)};
        var hops=new List<AccessRelationship>();
        if(users) { components.Add(ScenarioExamples.Component("browser",ImplementationStack.JavaScriptTypeScript,true));hops.Add(ScenarioExamples.Hop("user-api","browser","orders")); }
        if(applications) { components.Add(ScenarioExamples.Component("worker",ImplementationStack.DotNetWorker));hops.Add(ScenarioExamples.Hop("app-api","worker","orders",ActingIdentity.Application)); }
        var scenario=ScenarioExamples.Scenario([..components],[ScenarioExamples.Resource("orders",ResourceCategory.CustomResource,"api")],[..hops]);
        var plan=new ArchitectureEvaluator().Evaluate(scenario).Plan!;
        var guide=new ArchitectureGuideGenerator().Generate(plan,new([]));
        var access=guide.Steps.Single(s=>s.Id=="expose-api");
        Assert.StartsWith(explanation,Assert.IsType<InstructionContent>(access.Content[0]).Text);
        Assert.Equal(users,access.Content.OfType<ConfigurationRowsContent>().Any(p=>p.Breadcrumbs.Contains("Expose an API")));
        Assert.Equal(applications,access.Content.OfType<PortalActionContent>().Any(p=>p.Breadcrumbs.Contains("App roles")));
        if(users) {
            var settings=Assert.Single(access.Content.OfType<ConfigurationRowsContent>());
            var scope=Assert.Single(settings.Values,v=>v.Label=="Scope name");
            Assert.Equal(GuideValueKind.Sample,scope.Kind);
            Assert.Contains("Example only",scope.Guidance);
            var exported=new MarkdownGuideExporter().ExportMarkdown(guide);
            Assert.Contains(scope.Value,exported);
            Assert.Contains(scope.Guidance,exported);
        }
        var registration=guide.Steps.Single(s=>s.Id=="register-api");
        var fields=registration.Content.OfType<ConfigurationRowsContent>().First();
        Assert.All(fields.Values,v=> { Assert.False(v.IsTechnical); Assert.False(v.CanCopy); });
        Assert.Equal("Select Register.",fields.Instruction);
        Assert.DoesNotContain(access.Content,c=>c is CopyableValueContent);
        Assert.DoesNotContain(access.Content.OfType<InstructionContent>(),c=>c.Text.Contains("Record the API"));
    }
}
