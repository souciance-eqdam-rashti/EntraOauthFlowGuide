using EntraAdvisor.Guide.Contracts;
namespace EntraAdvisor.Guide;
public static class GenericBrowserConfigurationContent {
 public static List<GuideContent> Create(string component,string name,string target) => [
  new CopyableValueContent(new(component+".clientId","Collect identifiers","__BROWSER_CLIENTID__",GuideValueKind.DeveloperSupplied,"From Overview, note Application (client) ID and Directory (tenant) ID for this browser registration.") { ReferenceOnly=true,CanCopy=false }) { GroupTitle="Collect identifiers",GroupSystem="Entra",GroupLocation=["App registrations",name,"Overview"] },
  new CodeContent(new("browser-settings",component,"json","appsettings.json",Settings)) { GroupTitle="Configure sign-in and API access",GroupSystem="Code",GroupLocation=[name,"appsettings.json (generic configuration example)"] },
  new InstructionContent("Replace every placeholder with your Entra registration and API values. RedirectUri must match the registered SPA callback. Scope is the actual delegated permission granted earlier; no scope is selected by this example.") { Title="Set your configuration values",GroupTitle="Configure sign-in and API access",GroupSystem="Code",GroupLocation=[name,"appsettings.json (generic configuration example)"] },
  new InstructionContent("Map these settings to your authentication library. Sign in with authorization code + PKCE, request the configured scope, and acquire an access token using the signed-in session. Handle interaction-required responses and cancellation.") { Title="Use the settings for sign-in",GroupTitle="Call "+target,GroupSystem="Api",GroupLocation=[name,"Authentication and API client"] },
  new InstructionContent("Send the access token in `Authorization: Bearer <access-token>` to the configured API URL. Use the library’s token cache; do not send an ID token or store a client secret in the browser.") { Title="Attach the access token",GroupTitle="Call "+target,GroupSystem="Api",GroupLocation=[name,"Authentication and API client"] }
 ];
 private const string Settings="""
{
  "Authentication": {
    "ClientId": "<browser-app-client-id>",
    "Authority": "https://login.microsoftonline.com/<tenant-id>",
    "RedirectUri": "<registered-spa-redirect-uri>"
  },
  "BackendApi": {
    "BaseUrl": "<backend-api-base-url>",
    "Scope": "api://<backend-api-client-id>/<scope-name>"
  }
}
""";
}
