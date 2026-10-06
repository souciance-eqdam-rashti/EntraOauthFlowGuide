using System.Collections.Immutable;
using EntraAdvisor.Engine;
using EntraAdvisor.Engine.Contracts;
using EntraAdvisor.Guide;
using EntraAdvisor.Guide.Contracts;
namespace EntraAdvisor.Tests;
public sealed class GuideGenerationTests
{
    private static OAuthPlan Chain() => new ArchitectureEvaluator().Evaluate(ScenarioExamples.Chain()).Plan!;
    [Fact] public void Guide_contains_ordered_fresh_registrations_and_exact_plan() {
        var p=Chain();var g=new DelegatedChainGuideGenerator().Generate(p,new([]));
        Assert.Same(p,g.Architecture); Assert.Equal(p.Id,g.PlanId);Assert.Equal("1.0.0",g.Versions.Templates);
        Assert.True(g.Steps.Single(s=>s.Id=="register-api-b").Section==GuideSection.ResourceRegistration);
        var seen=new HashSet<string>();foreach(var s in g.Steps) { Assert.All(s.DependsOnStepIds,id=>Assert.Contains(id,seen)); Assert.True(seen.Add(s.Id)); }
        var export=new MarkdownGuideExporter().ExportMarkdown(g);
        Assert.Contains("requestedAccessTokenVersion",export);Assert.Contains("__API_B_SCOPE__",export);Assert.Contains("RequiredScope",export);Assert.Contains("app.MapStaticAssets();",export);
        Assert.Contains("ReplyForbiddenWithWwwAuthenticateHeaderAsync",export); Assert.DoesNotContain("ClientSecret",export);
        Assert.All(g.Steps.SelectMany(s=>s.Content).OfType<CodeContent>(),c=>Assert.Contains(c.Artifact.Content,export));
    }
    [Fact] public void Forged_plan_is_rejected_even_when_id_matches() {
        var p=Chain();var forged=p with { Relationships=p.Relationships.SetItem(0,p.Relationships[0] with { Acquisition=TokenAcquisition.ClientCredentials }) };
        Assert.Throws<ArgumentException>(()=>new DelegatedChainGuideGenerator().Generate(forged,new([])));
    }
    [Fact] public void Other_identity_or_tenant_models_have_explicit_template_boundary() {
        var app=new ArchitectureEvaluator().Evaluate(ScenarioExamples.Chain(ActingIdentity.Application)).Plan!;
        Assert.False(DelegatedChainGuideGenerator.CanGenerate(app));
        Assert.Throws<NotSupportedException>(()=>new DelegatedChainGuideGenerator().Generate(app,new([])));
    }
    [Fact] public void Editing_a_hop_resets_dependent_implementation_progress() {
        var g=new DelegatedChainGuideGenerator().Generate(Chain(),new([]));
        var completion=g.Steps.Select(s=>new StepCompletion(s.Id,CompletionState.Complete)).ToImmutableArray();
        var changed=new AnswerChangeImpact([], [g.Architecture.Relationships[0].RelationshipId],[],"Changed") { ChangedFact=new("web","credential") };
        var result=GuideCompletionInvalidator.Apply(g,completion,changed,"web");
        Assert.All(result.Completion,c=>Assert.Equal(CompletionState.Pending,c.State));
    }
}

