using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using EntraAdvisor.Engine.Contracts;

namespace EntraAdvisor.Guide.Provisioning;

public enum ProvisioningFormat { Bicep, PowerShell }
public sealed record ProvisioningBundle(string FileName, IReadOnlyDictionary<string, string> Files)
{
    public byte[] ToZip()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
            foreach (var file in Files) {
                var entry = archive.CreateEntry(file.Key);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(file.Value);
            }
        return stream.ToArray();
    }
}

public sealed class ProvisioningExporter
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() } };
    public ProvisioningBundle Export(OAuthPlan architecture, ProvisioningFormat format)
    {
        var plan = new ProvisioningPlanBuilder().Build(architecture);
        var settings = new {
            deploymentKey = "<stable-unique-key-for-this-deployment>",
            applications = plan.Applications.ToDictionary(a => a.ComponentId, a => new {
                displayName = a.CreateRegistration ? "<name-of-your-" + a.ComponentId + ">" : a.Name, tenantId = "<home-tenant-guid>",
                redirectUris = a.Platform == RegistrationPlatform.None || a.DeviceCode ? Array.Empty<string>() : new[] { "<exact-registered-callback-uri>" },
                certificatePath = a.Credential == CredentialMechanism.Certificate ? "<path-to-public-certificate.cer>" : "",
                managedIdentityPrincipalId = a.UsesManagedIdentity ? "<existing-managed-identity-object-guid>" : ""
            }),
            permissions = plan.Permissions.ToDictionary(p => p.Key, p => new {
                value = "<actual-scope-or-role-name>", displayName = "<permission-display-name>", description = "<permission-description>",
                consentType = p.Mode == PermissionMode.DelegatedScopes ? "<User-or-Admin>" : ""
            }),
            resources = plan.Access.DistinctBy(a => a.ResourceId).ToDictionary(a => a.ResourceId, a => new {
                tenantId = "<resource-tenant-guid>", appId = a.ApiComponentId is not null || a.Category == ResourceCategory.AzureResource ? "" : a.Category == ResourceCategory.MicrosoftGraph ? "00000003-0000-0000-c000-000000000000" : "<resource-application-client-guid>"
            }),
            access = plan.Access.ToDictionary(a => a.RelationshipId, a => new {
                permissionValues = a.ApiComponentId is not null || a.Category == ResourceCategory.AzureResource ? Array.Empty<string>() : new[] { "<actual-permission-name>" }
            })
        };
        var files = new Dictionary<string, string> {
            ["plan.json"] = JsonSerializer.Serialize(plan, Json), ["settings.json"] = JsonSerializer.Serialize(settings, Json),
            ["Provisioning.Common.ps1"] = Resource("Provisioning.Common.ps1"),
            ["Grant-Consent.ps1"] = Resource("Grant-Consent.ps1"),
            ["README.md"] = Readme(plan, format)
        };
        if (format == ProvisioningFormat.PowerShell) files["Setup-Entra.ps1"] = Resource("Setup-Entra.ps1");
        else {
            files["Deploy-Bicep.ps1"] = Resource("Deploy-Bicep.ps1");
            files["create.bicep"] = Bicep(false); files["configure.bicep"] = Bicep(true);
            files["bicepconfig.json"] = """
                { "extensions": { "microsoftGraphV1": "br:mcr.microsoft.com/bicep/extensions/microsoftgraph/v1.0:1.0.0" } }
                """;
        }
        return new(format == ProvisioningFormat.Bicep ? "entra-setup-bicep.zip" : "entra-setup-powershell.zip", files);
    }
    private static string Resource(string name)
    {
        using var stream = typeof(ProvisioningExporter).Assembly.GetManifestResourceStream("EntraAdvisor.Guide.Provisioning." + name)
            ?? throw new InvalidOperationException("Missing provisioning script: " + name);
        using var reader = new StreamReader(stream); return reader.ReadToEnd();
    }
    private static string Readme(ProvisioningPlan plan, ProvisioningFormat format) => """
        # Entra setup export

        Extract this ZIP into a new directory. Edit settings.json, preserving plan.json.
        Examples are deliberately not selected for you. Remaining angle-bracket placeholders cause preflight failure before changes.
        This bundle contains no credentials. Never place private keys, secrets or access tokens in settings/state files.

        ## Prerequisites
        PowerShell 7 and Microsoft.Graph.Authentication (Install-Module Microsoft.Graph.Authentication -Scope CurrentUser).
        Setup uses Microsoft Graph v1.0 via Invoke-MgGraphRequest. The signed-in user needs Application.ReadWrite.All
        and an Entra role/tenant policy permitting the applicable app registration operations.
        Registration rights do not imply rights to grant admin consent. Grant-Consent.ps1 separately requests
        DelegatedPermissionGrant.ReadWrite.All and AppRoleAssignment.ReadWrite.All and requires an appropriately
        authorized administrator (some permissions require Privileged Role Administrator).

        ## Stages
        Run Register in every application's home tenant first, using the same state.json. Then run Configure in
        each caller/API home tenant. For different tenants, reconnect explicitly on each invocation.
        The scripts check the supplied tenant and connection. state.json contains only application/object IDs.
        Reruns locate registrations by stable uniqueName, not display name. Keep deploymentKey unchanged.
        These are fresh-start templates: the export owns its registration's scope/role/redirect/certificate collections.
        Reconcile changes manually if you have subsequently added other settings. Never use a new key for a rerun.
        Configure requires every participating custom API's registration to have completed Register first.
        Directory propagation can delay permission lookups: scripts retry bounded reads; rerun Configure if needed.

        """ + (format == ProvisioningFormat.PowerShell ? """
        ```powershell
        ./Setup-Entra.ps1 -TenantId '<home-tenant-guid>' -Phase Register
        ./Setup-Entra.ps1 -TenantId '<home-tenant-guid>' -Phase Configure
        ```
        """ : """
        Bicep additionally needs Az.Accounts, Az.Resources, Bicep CLI >= 0.36.1 and an existing Azure resource group
        in a subscription associated with the selected tenant. Microsoft Graph Bicep v1.0 extension is pinned in
        bicepconfig.json. The wrapper resolves permission IDs using Graph, then supplies typed template inputs.
        create.bicep and configure.bicep can also be deployed directly using the generated parameters JSON.
        No Azure hosts, role assignments or resource groups are created. Graph resources don't support what-if.

        ```powershell
        Connect-AzAccount -Tenant '<home-tenant-guid>' -Subscription '<subscription-guid>'
        ./Deploy-Bicep.ps1 -TenantId '<home-tenant-guid>' -ResourceGroupName '<existing-rg>' -Phase Register
        ./Deploy-Bicep.ps1 -TenantId '<home-tenant-guid>' -ResourceGroupName '<existing-rg>' -Phase Configure
        ```
        """) + """

        ## Optional administrator consent (separate from setup)
        Review the actual permissions and tenant-wide impact before running this command. Without this explicit
        switch, Grant-Consent.ps1 refuses to proceed. Delegated user consent may be sufficient depending on policy;
        this helper grants administrator consent for all users, not individual user consent.

        ```powershell
        ./Grant-Consent.ps1 -TenantId '<resource-tenant-guid>' -GrantTenantWideConsent
        ```

        ## Coverage and remaining work
        """ + "\n" + string.Join("\n", plan.ManualActions.Select(n => "- " + n)) + "\n\n" +
        "## References\n- https://learn.microsoft.com/en-us/graph/templates/bicep/quickstart-create-bicep-interactive-mode\n" +
        "- https://learn.microsoft.com/en-us/graph/tutorial-applications-basics\n" +
        "- https://learn.microsoft.com/en-us/graph/templates/bicep/limitations\n";

    private static string Bicep(bool configure) => """
        // Generated from typed provisioning data; no settings are inferred from guide prose.
        extension microsoftGraphV1
        param applications array

        resource registrations 'Microsoft.Graph/applications@v1.0' = [for app in applications: {
          uniqueName: app.uniqueName
          displayName: app.displayName
          signInAudience: app.signInAudience
          isFallbackPublicClient: app.deviceCode
          spa: { redirectUris: app.platform == 'Spa' ? app.redirectUris : [] }
          web: {
            redirectUris: app.platform == 'Web' ? app.redirectUris : []
            implicitGrantSettings: { enableAccessTokenIssuance: false, enableIdTokenIssuance: false }
          }
          publicClient: { redirectUris: app.platform == 'PublicClient' ? app.redirectUris : [] }
          keyCredentials: app.keyCredentials
          api: { requestedAccessTokenVersion: 2, oauth2PermissionScopes: app.scopes }
          appRoles: app.roles
        """ + (configure ? "\n  identifierUris: app.isApi ? ['api://${app.appId}'] : []\n  requiredResourceAccess: app.requiredResourceAccess\n" : "\n") + """
        }]
        resource principals 'Microsoft.Graph/servicePrincipals@v1.0' = [for (app, i) in applications: {
          appId: registrations[i].appId
        }]
        output identities array = [for (app, i) in applications: {
          componentId: app.componentId
          appId: registrations[i].appId
          objectId: registrations[i].id
          principalId: principals[i].id
        }]
        """;
}
