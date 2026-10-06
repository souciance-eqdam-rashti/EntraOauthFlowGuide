using System.Collections.Immutable;
using System.Text.Json;
using EntraAdvisor.Engine;
using EntraAdvisor.Engine.Contracts;
using EntraAdvisor.Guide.Contracts;

namespace EntraAdvisor.Guide;

/// <summary>Finite, reviewed template. Consumes decisions without selecting new flows.</summary>
public sealed class DelegatedChainGuideGenerator : IGuideGenerator
{
    public const string TemplateVersion = "1.0.0";
    public static bool CanGenerate(OAuthPlan p) =>
        p.Scenario.Components.Length == 3 && p.Scenario.Resources.Length == 2 && p.Relationships.Length == 2 &&
        p.Scenario.Tenants.Model.Value == WorkforceTenantModel.SingleTenant && p.Scenario.Tenants.IncludesGuestUsers.State == FactState.Known && !p.Scenario.Tenants.IncludesGuestUsers.Value &&
        p.Scenario.Components.Count(c => c.Stack.Value == ImplementationStack.BlazorServer) == 1 &&
        p.Scenario.Components.Count(c => c.Stack.Value == ImplementationStack.AspNetCoreApi) == 2 &&
        p.Scenario.Resources.All(r => r.Category.Value == ResourceCategory.CustomResource) &&
        p.Relationships.Count(h => h.Acquisition == TokenAcquisition.AuthorizationCode && h.Credential == CredentialMechanism.Certificate) == 1 &&
        p.Relationships.Count(h => h.Acquisition == TokenAcquisition.OnBehalfOf && h.Credential == CredentialMechanism.Certificate) == 1 &&
        p.ApiValidation.All(v => v.Authorization.All(a => a.AcceptedIdentity == ActingIdentity.DelegatedUser));

    public ImplementationGuide Generate(OAuthPlan validatedPlan, ImplementationFacts facts)
    {
        ArgumentNullException.ThrowIfNull(validatedPlan);
        var checkedPlan = new ArchitectureEvaluator().Evaluate(validatedPlan.Scenario).Plan;
        if (checkedPlan is null || JsonSerializer.Serialize(checkedPlan) != JsonSerializer.Serialize(validatedPlan)) throw new ArgumentException("Generate from the current engine's complete validated plan.", nameof(validatedPlan));
        if (!CanGenerate(validatedPlan)) throw new NotSupportedException("This template covers the single-tenant delegated certificate API chain.");
        if (!facts.Values.IsEmpty) throw new ArgumentException("This template exports explicit placeholders. Configure developer values in the generated projects.", nameof(facts));
        var first = validatedPlan.Relationships.Single(h => h.Acquisition == TokenAcquisition.AuthorizationCode);
        var second = validatedPlan.Relationships.Single(h => h.Acquisition == TokenAcquisition.OnBehalfOf);
        var web = validatedPlan.Scenario.Relationships.Single(h => h.Id == first.RelationshipId).CallerComponentId;
        var apiA = validatedPlan.Scenario.Relationships.Single(h => h.Id == second.RelationshipId).CallerComponentId;
        var apiB = validatedPlan.Scenario.Resources.Single(r => r.Id == validatedPlan.Scenario.Relationships.Single(h => h.Id == second.RelationshipId).TargetResourceId).ApiComponentId!;
        var sources = validatedPlan.Sources.AddRange(new[] {
            Source("Certificate configuration", "https://learn.microsoft.com/en-us/entra/msidweb/authentication/certificates"),
            Source("API scope enforcement", "https://learn.microsoft.com/en-us/entra/msidweb/authentication/authorization"),
            Source("API access-token version", "https://learn.microsoft.com/en-us/graph/api/resources/apiapplication?view=graph-rest-1.0"),
            Source("Blazor with Entra", "https://learn.microsoft.com/en-us/aspnet/core/blazor/security/blazor-web-app-with-entra?view=aspnetcore-10.0"),
            Source("OBO protocol", "https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-on-behalf-of-flow")
        }).DistinctBy(s => s.Url).ToImmutableArray();
        var steps = new List<GuideStep>();
        void Add(string id, GuideSection section, string title, string component, string action, string expected, params GuideContent[] content) => steps.Add(new() {
            Id=id, Section=section, Title=title, ComponentId=component, Purpose=action, Action=action, ExpectedResult=expected,
            DependsOnStepIds=steps.Count == 0 ? [] : [steps[^1].Id], RelatedRelationshipIds=[first.RelationshipId,second.RelationshipId], Content=content.Select(c => c is CodeContent code && code.Artifact.Id is "certs" or "run" ? new CodeContent(code.Artifact with { ExecutionLocation = code.Artifact.Id == "certs" ? "Local PowerShell terminal · account running the samples" : "Local terminal · common sample directory" }) : c).ToImmutableArray(), Sources=SourcesFor(section,sources)
        });
        GuideContent Text(string s) => new InstructionContent(s);
        GuideContent Portal(string action, params string[] path) => new PortalActionContent(path.ToImmutableArray(),action);
        GuideContent Value(string key,string label,string value,string guidance,bool referenceOnly=false) => new CopyableValueContent(new(key,label,value,GuideValueKind.DeveloperSupplied,guidance) { ReferenceOnly=referenceOnly,CanCopy=!referenceOnly });
        Add("prerequisites",GuideSection.Prerequisites,"Prepare your Entra tenant",web,"Verify access before creating three new registrations.","Tenant access and administrator support are available.",
            Text(TenantPreparation.Roles(validatedPlan)));
        foreach(var entry in new[] {(Id:apiB,Label:validatedPlan.Scenario.Components.Single(c=>c.Id==apiB).Name,Placeholder:"__API_B_CLIENT_ID__",Scope:"__API_B_SCOPE__"),(Id:apiA,Label:validatedPlan.Scenario.Components.Single(c=>c.Id==apiA).Name,Placeholder:"__API_A_CLIENT_ID__",Scope:"__API_A_SCOPE__")})
            Add("register-"+entry.Id,GuideSection.ResourceRegistration,"Register application for "+entry.Label,entry.Id,"Create a single-tenant API registration and define its delegated scope.","A new registration exposes one enabled delegated scope and requests v2 access tokens.",
                new ConfigurationRowsContent("Create the registration", ["Entra ID","App registrations","New registration"], [
                    new(entry.Id+".name","Name",entry.Label,GuideValueKind.Derived,"") { IsTechnical=false,CanCopy=false },
                    new(entry.Id+".accountTypes","Supported account types","Accounts in this organizational directory only",GuideValueKind.Derived,"") { IsTechnical=false,CanCopy=false }
                ], "Select Register. No redirect URI is needed."),
                Text("Use scopes: this API is called for a signed-in user. A scope describes an allowed operation; application permissions are not needed for this delegated chain."),
                Portal("Set Application ID URI to api://<api-client-id>; enable a scope such as Orders.Read with Admins only consent and its display name/description.","App registration","Expose an API"),
                Portal("Set api.requestedAccessTokenVersion to 2; preserve all other manifest fields.","App registration","Manifest"),
                new CodeContent(new("manifest-"+entry.Id,entry.Id,"json","Manifest fragment (merge only)","{\n  \"api\": {\n    \"requestedAccessTokenVersion\": 2\n  }\n}")));
        Add("register-web",GuideSection.ClientRegistration,"Register the server web app",web,"Create a confidential single-tenant Web registration.","Web redirect and logout callbacks match the local host.",
            Portal("New registration → Accounts in this organizational directory only. Authentication → Add a platform → Web → add redirect URIs https://localhost:7300/signin-oidc and https://localhost:7300/signout-callback-oidc. Set front-channel logout URL to https://localhost:7300/signout-oidc. Keep implicit grants and public client flows disabled.","Entra admin center","Identity","Applications","App registrations"));
        Add("certificates",GuideSection.ClientRegistration,"Create and upload two development certificates",web,"Use distinct certificates for Web and API A, both of which acquire tokens.","Each public certificate is uploaded to its owning registration; private keys remain in the Windows certificate store.",
            Text("Use CurrentUser/My under the account that runs the samples. These signing certificates are separate from the HTTPS development certificate."),
            Text("Run the following locally under the account that will run the sample. Replace __WEB_CERT_THUMBPRINT__ and __API_A_CERT_THUMBPRINT__ with the displayed values. Upload only the exported .cer files in each registration's Certificates & secrets → Certificates → Upload certificate. API B validates incoming tokens and needs no client credential."),
            new CodeContent(new("certs",web,"powershell","Create-DevelopmentCertificates.ps1", "foreach ($name in @('Web', 'ApiA')) {\n  $cert = New-SelfSignedCertificate -Subject \"CN=EntraAdvisor-$name\" -CertStoreLocation 'Cert:\\CurrentUser\\My' -KeySpec Signature -KeyExportPolicy NonExportable -NotAfter (Get-Date).AddMonths(3)\n  Export-Certificate -Cert $cert -FilePath \"$name.cer\" | Out-Null\n  Write-Output \"$name thumbprint: $($cert.Thumbprint)\"\n}")),
            new ExplanationContent("Production credentials and caches","These development certificates and in-memory token caches are for a local test only. Production requires a protected certificate source with rotation, process access to the private key, shared encrypted token caching and protected shared Data Protection keys for multiple server instances. Choose a supported certificate store or Key Vault integration before deployment. Do not substitute a browser secret."));
        Add("permissions",GuideSection.PermissionsAndConsent,"Grant the two delegated permissions",apiA,"Configure each caller against its immediate target.","Web has delegated API A scope; API A has delegated API B scope; administrator consent is recorded.",
            Portal("Web app: add the Backend API scope as a Delegated permission. Backend API: add the Downstream API scope as a Delegated permission. Have an administrator grant consent to both.","App registrations","API permissions"),
            Value(first.Authorization.DeveloperValueKey,"Web app → Backend API scope","api://__API_A_CLIENT_ID__/__API_A_SCOPE__","Request the backend’s scope, e.g. api://<backend-client-id>/Orders.Read."),
            Value(second.Authorization.DeveloperValueKey,"Backend API → Downstream API scope","api://__API_B_CLIENT_ID__/__API_B_SCOPE__","Request the downstream API’s own scope, e.g. api://<downstream-client-id>/Inventory.Read; acquire a separate token through OBO."),
            Text("Keep only permissions used by this chain; no Graph or Azure permissions are needed. Each API enforces its own scope (scp)."));
        var artifacts = DelegatedChainTemplates.Create(web,apiA,apiB);
        Add("projects",GuideSection.Dependencies,"Create the three projects",web,"Create empty directories Web, ApiA and ApiB and copy the project files.","Pinned Microsoft.Identity.Web 4.16.0 dependencies restore successfully.",artifacts.Where(a=>a.DestinationFile.EndsWith(".csproj",StringComparison.Ordinal)).Select(a=>(GuideContent)new CodeContent(a)).Prepend(Text("Install .NET SDK 10.0.400 or a compatible .NET 10 SDK. Trust the local HTTPS certificate with dotnet dev-certs https --trust. Use the supplied complete project files. Paths are relative to one common sample directory. Create every parent directory shown.")).ToArray());
        foreach(var component in new[]{web,apiA,apiB}) {
            var content=artifacts.Where(a=>a.ComponentId==component && !a.DestinationFile.EndsWith(".csproj",StringComparison.Ordinal)).Select(a=>(GuideContent)new CodeContent(a)).Prepend(Value(component+".clientId","Configure client and tenant IDs","__CLIENT_ID__","Open this app’s Entra registration → Overview. Set its Application (client) ID and Directory (tenant) ID in this app’s authentication configuration.", true)).Prepend(Text(component==web ? "Configure server sign-in and request the backend’s delegated scope; keep the certificate private key on the server." : "Validate signature, issuer, expiry and this API’s client ID as the v2 audience; enforce its scope in scp." + (component==apiA ? " Use OBO for the downstream API’s scope; do not forward the incoming token." : ""))).ToArray();
            Add("configure-"+component,GuideSection.AuthenticationAndAuthorization,"Configure "+validatedPlan.Scenario.Components.Single(c=>c.Id==component).Name,component,"Copy the complete files and replace every __PLACEHOLDER__ consistently.","The component builds with authentication, target-specific token acquisition and delegated authorization configured.",content);
        }
        Add("run",GuideSection.TestAndTroubleshoot,"Build and run the chain",web,"Build each project, then run it in a separate terminal.","The HTTPS hosts are reachable; signing in to Web and choosing Call API chain returns API B's protected response.",
            new CodeContent(new("run",web,"powershell","Terminal commands","dotnet build Web/Web.csproj\ndotnet build ApiA/ApiA.csproj\ndotnet build ApiB/ApiB.csproj\n# In three separate terminals:\ndotnet run --project ApiB --urls https://localhost:7302\ndotnet run --project ApiA --urls https://localhost:7301\ndotnet run --project Web --urls https://localhost:7300")),
            Text("Open https://localhost:7300. Sign in as a member test user and choose Call API chain. Web acquires an API A token; API A exchanges the incoming user context through OBO for an API B token. API B's accepted audience is its own client ID. Inspect status and trace IDs locally; do not print bearer tokens or paste them into third-party decoders."));
        Add("negative-tests",GuideSection.TestAndTroubleshoot,"Verify failures and record evidence",apiB,"Test authentication, authorization and consent separately.","Positive chain result and expected 401/403 outcomes are recorded without token disclosure.",
            Text("No token → GET https://localhost:7302/data must return 401. A genuine token issued for API A sent to API B must return 401 (wrong audience). A valid API B delegated token without the required scope must return 403. Use a controlled test client and server-side acquisition to obtain these tokens; never disable validation to manufacture a passing result. App-only tokens must not pass the delegated scope requirement."),
            Text("Revoke API A's API B consent in the test tenant, clear the local process token caches by restarting Web and API A, and retry. Expect an administrator-consent or claims challenge, not successful API B access. Denied consent is a prerequisite failure. Restore consent and repeat the positive test."),
            Text("401: check tenant, issuer, audience, token version and token expiry. 403: check scp and target scope, consent, user policy and conditional access. The sample propagates an OBO claims challenge to the Web authentication handler. If policy cannot be satisfied, record the blocker and contact the tenant administrator; do not retry in a loop or substitute application permissions."),
            new ExplanationContent("Verification limits","Compilation verifies API and syntax compatibility only. This template has not yet been run against the supplied test tenant. Record tenant verification evidence in milestone 5 before treating it as production validated."));
        return new() { PlanId=validatedPlan.Id, Versions=new(validatedPlan.Versions.Schema,validatedPlan.Versions.Rules,TemplateVersion), Architecture=validatedPlan, Steps=steps.ToImmutableArray(), Sources=sources, Assumptions=validatedPlan.Assumptions.Add("Windows local development; single workforce tenant, member users, fresh registrations, certificate credentials and explicit admin consent.") };
    }
    private static ImmutableArray<DocumentationSource> SourcesFor(GuideSection section, ImmutableArray<DocumentationSource> sources) => sources.Where(s => section switch {
        GuideSection.ResourceRegistration or GuideSection.Manifest => s.Title is "API access-token version" or "API scope enforcement",
        GuideSection.ClientRegistration => s.Title is "Certificate configuration" or "Blazor with Entra",
        GuideSection.Prerequisites or GuideSection.PermissionsAndConsent => s.Title == "Consent and policy",
        _ => s.Title is "API scope enforcement" or "OBO protocol" or "Blazor with Entra"
    }).ToImmutableArray();
    private static DocumentationSource Source(string title,string url) => new(title,new Uri(url),new DateOnly(2026,10,5));
}

