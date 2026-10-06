using System.Collections.Immutable;
using EntraAdvisor.Guide.Contracts;
using EntraAdvisor.Engine.Contracts;
namespace EntraAdvisor.Guide;
public sealed record InstructionGroup(string Title,string System,ImmutableArray<string> Location,ImmutableArray<GuideContent> Actions);
public static class InstructionGroups {
 public static ImmutableArray<InstructionGroup> Create(GuideStep step,OAuthPlan plan) {
  var component=plan.Scenario.Components.First(c=>c.Id==step.ComponentId).Name;
  var result=new List<InstructionGroup>();
  foreach(var content in step.Content) {
   if(content is ExplanationContent && result.Count>0) { result[^1]=result[^1] with { Actions=result[^1].Actions.Add(content) };continue; }
   var context=content switch {
    PortalActionContent p => (p.Title,"Entra",p.Breadcrumbs),
    ConfigurationRowsContent v => (v.Title,"Entra",v.Breadcrumbs),
    CopyableValueContent v when v.Value.ReferenceOnly => ("Collect application identifiers","Entra",ImmutableArray.Create("App registrations",component,"Overview")),
    CopyableValueContent => ("Configure application settings","Code",ImmutableArray.Create(component,"Authentication configuration")),
    CodeContent c => (c.Artifact.ExecutionLocation is null ? "Apply the code" : "Run the commands",c.Artifact.ExecutionLocation is null ? "Code" : "Terminal",ImmutableArray.Create(component,c.Artifact.ExecutionLocation ?? c.Artifact.DestinationFile)),
    InstructionContent i => (string.IsNullOrEmpty(i.Title) ? step.Title : i.Title,step.Section==GuideSection.Prerequisites ? "Entra" : step.Section==GuideSection.TestAndTroubleshoot ? "Test" : "Code",i.Location.IsEmpty ? ImmutableArray.Create(component,step.Section==GuideSection.Prerequisites ? "Tenant access requirements" : "Implementation settings") : i.Location),
    _ => (step.Title,"Code",ImmutableArray.Create(component,"Implementation settings"))
   };
   var title=string.IsNullOrEmpty(content.GroupTitle) ? context.Item1 : content.GroupTitle;
   var system=string.IsNullOrEmpty(content.GroupSystem) ? context.Item2 : content.GroupSystem;
   var location=content.GroupLocation.IsEmpty ? context.Item3 : content.GroupLocation;
   if(result.Count>0 && result[^1].System==system && result[^1].Location.SequenceEqual(location) && (string.IsNullOrEmpty(content.GroupTitle) || result[^1].Title==title)) result[^1]=result[^1] with { Actions=result[^1].Actions.Add(content) };
   else result.Add(new(title,system,location,[content]));
  }
  return result.ToImmutableArray();
 }
}
