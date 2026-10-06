using System.Collections.Immutable;
using EntraAdvisor.Engine;
using EntraAdvisor.Engine.Contracts;
using EntraAdvisor.Engine.Questionnaire;
using EntraAdvisor.Guide;
using EntraAdvisor.Guide.Contracts;
if(args.Contains("--mock-resource")) {
 using var listener=new System.Net.HttpListener();listener.Prefixes.Add("http://127.0.0.1:58473/");listener.Start();
 Console.WriteLine("Local synthetic resource ready.");
 for(var i=0;i<2;i++) {
  var context=await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(45));
  context.Response.StatusCode=context.Request.Headers["Authorization"]=="Bearer synthetic-probe-token" ? 200 : 401;
  context.Response.Close();
 }
 return;
}
var scenario = TopologyPresets.Create("api-chain");
scenario = scenario with {
 Tenants = new() { Domain=Fact<IdentityDomain>.Supplied(IdentityDomain.Workforce), Model=Fact<WorkforceTenantModel>.Supplied(WorkforceTenantModel.SingleTenant), IncludesGuestUsers=Fact<bool>.Supplied(false) },
 Components = scenario.Components.Select(c=> c with { CanProtectCredentials=Fact<bool>.Supplied(true), Credential=Fact<CredentialCapability>.Supplied(CredentialCapability.Certificate), Hosting=Fact<HostingEnvironment>.Supplied(HostingEnvironment.NonAzure) }).ToImmutableArray(),
 Relationships = scenario.Relationships.Select(h=>h with { TenantBoundary=Fact<TenantBoundary>.Supplied(TenantBoundary.SameTenant) }).ToImmutableArray()
};
var plan = new ArchitectureEvaluator().Evaluate(scenario).Plan ?? throw new InvalidOperationException("Fixture must evaluate Ready.");
var guide = new DelegatedChainGuideGenerator().Generate(plan,new([]));
var root=Path.GetFullPath(Path.Combine(Environment.CurrentDirectory,".artifacts","generated-samples"));
Directory.CreateDirectory(root);
foreach(var artifact in guide.Steps.SelectMany(s=>s.Content).OfType<CodeContent>().Select(c=>c.Artifact).Where(a=>a.DestinationFile.StartsWith("Web/",StringComparison.Ordinal)||a.DestinationFile.StartsWith("ApiA/",StringComparison.Ordinal)||a.DestinationFile.StartsWith("ApiB/",StringComparison.Ordinal))) {
 var path=Path.GetFullPath(Path.Combine(root,artifact.DestinationFile));
 if(!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Invalid artifact path.");
 Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,artifact.Content);
}
File.WriteAllText(Path.Combine(root,"guide.md"),new MarkdownGuideExporter().ExportMarkdown(guide));
Console.WriteLine($"Generated {guide.Steps.Length} guide steps into {root}");
var validator=new ValidationScriptExporter();
foreach(var preset in TopologyPresets.All) {
 var session=QuestionnaireSession.Create(TopologyPresets.Create(preset.Id));
 for(var i=0;i<70 && session.NextScreen is {} screen;i++) {
  var q=screen.Questions.Single();
  var value=q.SuppliesFact.FactName switch {
   "Model"=>"SingleTenant", "IncludesGuestUsers" or "ManagedIdentityAvailable"=>"False",
   "CanProtectCredentials" or "LocalBrowserAvailable" or "AlternateBrowserAvailable" or "DeviceCodePermitted"=>"True",
   "Hosting"=>"NonAzure", "Credential"=>"Certificate", "IncomingIdentity" or "Identity"=>"DelegatedUser", "TenantBoundary"=>"SameTenant", _=>q.Options[0].Id
  };
  session=session.Apply(new(q.Id,[value])).Session;
 }
 var ready=session.Evaluation.Plan ?? throw new InvalidOperationException("Validation fixture must be ready.");
 var scenarioFolder=Path.Combine(root,"validation",preset.Id,"all-connections");
 Directory.CreateDirectory(scenarioFolder);
 foreach(var format in Enum.GetValues<ValidationScriptFormat>()) {
  var output=validator.ExportAll(ready,format);
  File.WriteAllText(Path.Combine(scenarioFolder,output.FileName),output.Content);
 }
 foreach(var probe in validator.Probes(ready)) {
  var folder=Path.Combine(root,"validation",preset.Id,probe.Id);
  Directory.CreateDirectory(folder);
  foreach(var powershell in new[]{false,true}) {
   var output=validator.Export(ready,probe.Id,powershell);
   File.WriteAllText(Path.Combine(folder,output.FileName),output.Content);
  }
 }
}
Console.WriteLine("Generated validation scripts for every preset into the ignored sample directory.");
 

