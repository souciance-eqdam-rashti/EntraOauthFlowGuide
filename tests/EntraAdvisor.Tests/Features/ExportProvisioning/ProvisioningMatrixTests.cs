using System.Text.Json;
using EntraAdvisor.Engine.Questionnaire;
using EntraAdvisor.Guide.Provisioning;
using Xunit.Abstractions;

namespace EntraAdvisor.Tests;
public sealed class ProvisioningMatrixTests(ITestOutputHelper output)
{
 [Fact]
 public void AllConcreteReadyQuestionnairePathsProduceProvisioningBundles()
 {
  var plans=new HashSet<string>();
  foreach(var preset in TopologyPresets.All){
   var states=new HashSet<string>();var pending=new Stack<(QuestionnaireSession Session,int Depth)>();
   pending.Push((QuestionnaireSession.Create(TopologyPresets.Create(preset.Id)),0));
   var ready=0;
   while(pending.TryPop(out var item)){
    Assert.True(item.Depth<80);
    if(!states.Add(JsonSerializer.Serialize(item.Session.Scenario)))continue;
    if(item.Session.NextScreen is {} screen){
     var question=screen.Questions.Single();
     foreach(var option in question.Options.Where(o=>!o.RepresentsUnknown))pending.Push((item.Session.Apply(new(question.Id,[option.Id])).Session,item.Depth+1));
     continue;
    }
    if(item.Session.Evaluation.Plan is not {} architecture)continue;
    ready++;
    if(!plans.Add(architecture.Id))continue;
    var typed=new ProvisioningPlanBuilder().Build(architecture);
    foreach(var access in typed.Access.Where(a=>a.ApiComponentId is not null))
     Assert.Contains(typed.Permissions,p=>p.ApiComponentId==access.ApiComponentId && p.Key==access.PermissionKey && p.Mode==access.Mode);
    foreach(var access in typed.Access)Assert.Equal(architecture.Relationships.Single(r=>r.RelationshipId==access.RelationshipId).Credential,access.Credential);
    foreach(var app in typed.Applications)Assert.Equal(typed.Access.Any(a=>a.CallerComponentId==app.ComponentId && a.Credential==EntraAdvisor.Engine.Contracts.CredentialMechanism.ManagedIdentity),app.UsesManagedIdentity);
    var bicep=new ProvisioningExporter().Export(architecture,ProvisioningFormat.Bicep);
    var shell=new ProvisioningExporter().Export(architecture,ProvisioningFormat.PowerShell);
    Assert.Equal(bicep.Files["plan.json"],shell.Files["plan.json"]);
    Assert.Equal(bicep.Files["settings.json"],shell.Files["settings.json"]);
    using var settings=JsonDocument.Parse(shell.Files["settings.json"]);
    foreach(var app in typed.Applications)Assert.True(settings.RootElement.GetProperty("applications").TryGetProperty(app.ComponentId,out _));
   }
   Assert.True(ready>0,$"No ready paths for {preset.Id}");
  }
  output.WriteLine($"Provisioning exports covered {plans.Count} distinct ready scenarios across {TopologyPresets.All.Length} presets.");
 }
}
