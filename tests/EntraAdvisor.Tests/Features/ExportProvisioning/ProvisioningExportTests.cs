using System.Collections.Immutable;
using System.IO.Compression;
using System.Text.Json;
using EntraAdvisor.Engine;
using EntraAdvisor.Engine.Contracts;
using EntraAdvisor.Guide.Provisioning;

namespace EntraAdvisor.Tests;
public sealed class ProvisioningExportTests
{
 [Theory]
 [MemberData(nameof(ArchitectureEvaluatorTests.Supported), MemberType=typeof(ArchitectureEvaluatorTests))]
 public void SupportedFamiliesHaveMatchingTypedExports(string name, ArchitectureScenario scenario, TokenAcquisition? acquisition)
 {
  Assert.NotEmpty(name);
  var architecture=new ArchitectureEvaluator().Evaluate(scenario).Plan!;
  if(acquisition is not null)Assert.Contains(architecture.Relationships,r=>r.Acquisition==acquisition);
  var typed=new ProvisioningPlanBuilder().Build(architecture);
  Assert.Equal(architecture.Registrations.Select(r=>r.ComponentId),typed.Applications.Select(a=>a.ComponentId));
  Assert.Equal(architecture.Relationships.Length,typed.Access.Length);
  Assert.Equal(architecture.Prerequisites.ToArray(),typed.Prerequisites.ToArray());
  foreach(var app in typed.Applications)Assert.Equal(architecture.Registrations.Single(r=>r.ComponentId==app.ComponentId).CreateRegistration,app.CreateRegistration);
  foreach(var access in typed.Access)Assert.Equal(architecture.Relationships.Single(r=>r.RelationshipId==access.RelationshipId).Authorization.Mode,access.Mode);
  foreach(var format in Enum.GetValues<ProvisioningFormat>()){
   var bundle=new ProvisioningExporter().Export(architecture,format);
   using var archive=new ZipArchive(new MemoryStream(bundle.ToZip()));
   Assert.Equal(bundle.Files.Count,archive.Entries.Count);
   using var settings=JsonDocument.Parse(bundle.Files["settings.json"]);
   Assert.DoesNotContain("Orders.Read",bundle.Files["settings.json"]);
   Assert.DoesNotContain("User.Read",bundle.Files["settings.json"]);
   Assert.Contains("GrantTenantWideConsent",bundle.Files["Grant-Consent.ps1"]);
   Assert.DoesNotContain("Grant-Consent.ps1",bundle.Files[format==ProvisioningFormat.Bicep?"Deploy-Bicep.ps1":"Setup-Entra.ps1"]);
   Assert.Contains("Microsoft.Graph.Authentication",bundle.Files["Provisioning.Common.ps1"]);
   if(format==ProvisioningFormat.Bicep)Assert.Contains("requestedAccessTokenVersion: 2",bundle.Files["create.bicep"]);
  }
 }
 [Fact]
 public void PermissionsAreStableAndCoverBothIncomingIdentities()
 {
  var scenario=ScenarioExamples.Scenario([ScenarioExamples.Component("api",ImplementationStack.AspNetCoreApi) with {IncomingIdentity=Fact<IncomingTokenIdentity>.Supplied(IncomingTokenIdentity.Both)}]);
  var architecture=new ArchitectureEvaluator().Evaluate(scenario).Plan!;
  var builder=new ProvisioningPlanBuilder();var plan=builder.Build(architecture);
  Assert.Equal(2,plan.Permissions.Length);
  Assert.Contains(plan.Permissions,p=>p.Mode==PermissionMode.DelegatedScopes);
  Assert.Contains(plan.Permissions,p=>p.Mode==PermissionMode.ApplicationPermissions);
  Assert.Equal(plan.Permissions.ToArray(),builder.Build(architecture).Permissions.ToArray());
 }
 [Fact]
 public void ManagedIdentityAndAzureAuthorizationAreExplicitRemainingWork()
 {
  var architecture=new ArchitectureEvaluator().Evaluate(ScenarioExamples.Worker(ResourceCategory.AzureResource,true)).Plan!;
  var plan=new ProvisioningPlanBuilder().Build(architecture);
  Assert.False(Assert.Single(plan.Applications).CreateRegistration);
  Assert.Contains(plan.ManualActions,n=>n.Contains("existing service principal"));
  Assert.Contains(plan.ManualActions,n=>n.Contains("Azure RBAC"));
 }
 [Fact]
 public void ScenarioTextStaysInJsonRatherThanExecutableCode()
 {
  var architecture=new ArchitectureEvaluator().Evaluate(ScenarioExamples.Chain()).Plan!;
  var name="' ; Remove-Item / ; ${malicious}";
  architecture=architecture with { Scenario=architecture.Scenario with {Components=architecture.Scenario.Components.Select(c=>c with{Name=name}).ToImmutableArray()} };
  var bundle=new ProvisioningExporter().Export(architecture,ProvisioningFormat.PowerShell);
  Assert.Contains("Remove-Item",bundle.Files["plan.json"]);
  Assert.DoesNotContain("Remove-Item",bundle.Files["Setup-Entra.ps1"]);
 }
}
