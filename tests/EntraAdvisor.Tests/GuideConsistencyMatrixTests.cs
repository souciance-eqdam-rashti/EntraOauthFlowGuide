using EntraAdvisor.Engine.Contracts;
using EntraAdvisor.Engine.Questionnaire;
using EntraAdvisor.Guide;
using EntraAdvisor.Guide.Contracts;
using Xunit.Abstractions;
using System.Text.Json;
namespace EntraAdvisor.Tests;
public sealed class GuideConsistencyMatrixTests(ITestOutputHelper output)
{
 [Fact]
 public void ReadyScenarioMatrixUsesConsistentInstructionsAndExports()
 {
  var ready=0;var checkedSteps=0;var visited=new HashSet<string>();
  foreach(var preset in TopologyPresets.All) {
   var presetReady=0;
   var states=new HashSet<string>();
   var pending=new Stack<(QuestionnaireSession Session,int Depth)>();
   pending.Push((QuestionnaireSession.Create(TopologyPresets.Create(preset.Id)),0));
   while(pending.TryPop(out var item)) {
    var session=item.Session;
    if(!states.Add(JsonSerializer.Serialize(session.Scenario))) continue;
    Assert.True(item.Depth<80,"Questionnaire traversal must terminate.");
    if(session.NextScreen is {} screen) {
     var question=screen.Questions.Single();
     foreach(var option in question.Options.Where(o=>!o.RepresentsUnknown)) {
      var next=session.Apply(new(question.Id,[option.Id])).Session;
      pending.Push((next,item.Depth+1));
     }
     continue;
    }
    if(session.Evaluation.Plan is not {} plan) continue;
    presetReady++;
    if(!visited.Add(plan.Id))continue;
    ready++;
    var guide=new ArchitectureGuideGenerator().Generate(plan,new([]));
    var markdown=new MarkdownGuideExporter().ExportMarkdown(guide);
    Assert.Same(plan,guide.Architecture);
    foreach(var step in guide.Steps) {
     checkedSteps++;
     var groups=InstructionGroups.Create(step,plan);
     Assert.Equal(step.Content,groups.SelectMany(g=>g.Actions));
     Assert.All(groups,g=> { Assert.NotEmpty(g.Title);Assert.NotEmpty(g.Location); });
     Assert.Contains(step.Title,markdown);
     Assert.DoesNotContain("For v1",markdown);
     Assert.DoesNotContain("Done with this group",markdown);
     foreach(var value in step.Content.OfType<ConfigurationRowsContent>().SelectMany(c=>c.Values).Concat(step.Content.OfType<CopyableValueContent>().Select(c=>c.Value))) {
      Assert.False(value.CopyInForm);Assert.False(value.CanCopy);
      if(value.Label=="Name") { Assert.Equal(GuideValueKind.DeveloperSupplied,value.Kind);Assert.StartsWith("<name of your ",value.Value); }
     }
     var browser=plan.Scenario.Components.Single(c=>c.Id==step.ComponentId);
     if(step.Id.StartsWith("configure-") && browser.Stack.Value is ImplementationStack.BlazorWebAssembly or ImplementationStack.JavaScriptTypeScript) {
      Assert.Contains(step.Content,c=>c is CodeContent code && code.Artifact.DestinationFile=="appsettings.json");
      Assert.DoesNotContain(step.Content.OfType<CodeContent>(),c=>c.Artifact.Content.Contains("@azure/msal-browser") || c.Artifact.Content.Contains("AddMsalAuthentication"));
     }
    }
   }
   Assert.True(presetReady>0,$"No ready paths covered for {preset.Id}");
  }
  output.WriteLine($"Covered {ready} distinct ready scenarios and {checkedSteps} implementation steps across {TopologyPresets.All.Length} presets (all terminating, concrete questionnaire answer paths).");
 }
}
