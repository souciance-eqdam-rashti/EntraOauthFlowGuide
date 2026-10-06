using EntraAdvisor.Engine;
using EntraAdvisor.Engine.Contracts;
using EntraAdvisor.Guide;

namespace EntraAdvisor.Tests;
public sealed class ValidationExportTests
{
    private readonly ValidationScriptExporter exporter=new();
    [Theory]
    [InlineData(ValidationScriptFormat.CurlBash)]
    [InlineData(ValidationScriptFormat.CurlWindows)]
    [InlineData(ValidationScriptFormat.PowerShell)]
    public void ScenarioExportIncludesEveryConnectionWithIndependentSettings(ValidationScriptFormat format)
    {
        var plan=new ArchitectureEvaluator().Evaluate(ScenarioExamples.Chain()).Plan!;
        var script=exporter.ExportAll(plan,format);
        foreach(var probe in exporter.Probes(plan)) Assert.Contains(System.Text.Json.JsonSerializer.Serialize(probe.Label),script.Content);
        Assert.Contains("Scenario connection 1:",script.Content);
        Assert.Contains("Scenario connection 2:",script.Content);
        if(format==ValidationScriptFormat.CurlBash) {
            Assert.Equal(2,script.Content.Split("CLIENT_ID='<caller-client-guid>'").Length-1);
            Assert.Contains("MODE='code'",script.Content);Assert.Contains("MODE='obo'",script.Content);
        } else {
            Assert.Contains("function Test-Connection1",script.Content);Assert.Contains("function Test-Connection2",script.Content);
            Assert.Contains("$mode = 'code'",script.Content);Assert.Contains("$mode = 'obo'",script.Content);
        }
        if(format==ValidationScriptFormat.CurlWindows) {
            Assert.Contains("curl.exe",script.Content);
            Assert.DoesNotContain("Invoke-RestMethod",script.Content);Assert.DoesNotContain("Invoke-WebRequest",script.Content);
            Assert.EndsWith("-curl-windows.ps1",script.FileName);
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CertificateApplicationProbeUsesClientCredentialsAndDoesNotPrintToken(bool powershell)
    {
        var plan=new ArchitectureEvaluator().Evaluate(ScenarioExamples.Worker()).Plan!;
        var output=exporter.Export(plan,exporter.Probes(plan)[0].Id,powershell);
        Assert.Contains("client_credentials",output.Content);
        Assert.Contains("PS256",output.Content);Assert.Contains("x5t#S256",output.Content);
        Assert.Contains("without token",output.Content);
        Assert.DoesNotContain("client_secret",output.Content);
        Assert.DoesNotContain("grant_type=password",output.Content);
        Assert.DoesNotContain("Write-Host $accessToken",output.Content);
        Assert.Contains(plan.Id,output.Content);
    }
    [Fact]
    public void ChainExportsSeparateCodeAndOboProbes()
    {
        var plan=new ArchitectureEvaluator().Evaluate(ScenarioExamples.Chain()).Plan!;
        var probes=exporter.Probes(plan);Assert.Equal(2,probes.Length);
        var code=exporter.Export(plan,probes.Single(p=>p.Flow==TokenAcquisition.AuthorizationCode).Id,true).Content;
        Assert.Contains("$mode = 'code'",code);Assert.Contains("code_verifier",code);Assert.Contains("state/code is invalid",code);
        var obo=exporter.Export(plan,probes.Single(p=>p.Flow==TokenAcquisition.OnBehalfOf).Id,true).Content;
        Assert.Contains("$mode = 'obo'",obo);Assert.Contains("IncomingTokenFile",obo);Assert.Contains("on_behalf_of",obo);
    }
    [Fact]
    public void DeviceCodeIsNotReplacedWithAnotherGrant()
    {
        var plan=new ArchitectureEvaluator().Evaluate(ScenarioExamples.Device()).Plan!;
        var probe=exporter.Probes(plan).Single();
        Assert.Equal(TokenAcquisition.DeviceCode,probe.Flow);
        Assert.Contains("$mode = 'device'",exporter.Export(plan,probe.Id,true).Content);
        Assert.Contains("authorization_pending",exporter.Export(plan,probe.Id,false).Content);
    }
    [Fact]
    public void ManagedIdentityProbeUsesHostTokenAndExplainsAcquisitionBoundary()
    {
        var plan=new ArchitectureEvaluator().Evaluate(ScenarioExamples.Worker(managed:true)).Plan!;
        var probe=exporter.Probes(plan).Single();
        var script=exporter.Export(plan,probe.Id,true).Content;
        Assert.Contains("$mode = 'token'",script);Assert.Contains("AccessTokenFile",script);
        Assert.Contains("does not verify the host's token acquisition",script);
    }
    [Fact]
    public void ForgedPlanCannotExportScripts()
    {
        var plan=new ArchitectureEvaluator().Evaluate(ScenarioExamples.Worker()).Plan!;
        Assert.Throws<ArgumentException>(()=>exporter.Probes(plan with {Registrations=[]}));
    }
}
