using EntraAdvisor.Guide.Contracts;
namespace EntraAdvisor.Guide;
public static class BlazorBrowserConfigurationContent {
 public static List<GuideContent> Create(string component,string name,string target) {
  GuideContent Action(string title,string text,string group,string system,string file) => new InstructionContent(text) { Title=title,GroupTitle=group,GroupSystem=system,GroupLocation=[name,file] };
  GuideContent Code(string id,string file,string text,string group,string system,string location,string language="csharp") => new CodeContent(new(id,component,language,file,text)) { GroupTitle=group,GroupSystem=system,GroupLocation=[name,location] };
  const string signIn="Configure sign-in";
  var call="Call "+target;
  const string authFiles="Program.cs + wwwroot/appsettings.json (Blazor WASM template)";
  const string apiFiles="Program.cs + Services/BackendApiClient.cs (sample file to create)";
  return [
   new CopyableValueContent(new(component+".clientId","Copy the application identifiers","__BROWSER_CLIENTID__",GuideValueKind.DeveloperSupplied,"Copy Application (client) ID and Directory (tenant) ID from this browser registration. Set them in the application in the next group; the browser client ID is different from the Backend API client ID.") { ReferenceOnly=true,CanCopy=false }) { GroupTitle="Collect application identifiers",GroupSystem="Entra",GroupLocation=["App registrations",name,"Overview"] },
   Action("Use the Blazor MSAL integration","For a new app, run `dotnet new blazorwasm -au SingleOrg -o BrowserApp --client-id __BROWSER_CLIENT_ID__ --tenant-id __TENANT_ID__`. For an existing app, use Microsoft.Authentication.WebAssembly.Msal and retain the template’s authentication routes, authorization components and AuthenticationService.js script. The samples below extend that authenticated template.",signIn,"Code",authFiles),
   Action("Set identifiers in the app","In wwwroot/appsettings.json, replace the placeholders with the identifiers collected from Entra. In Program.cs, retain AddMsalAuthentication. Use the template callback, for example https://localhost:<port>/authentication/login-callback, matching the SPA redirect URI registered in the earlier Entra setup step. Its scheme, host, port and path must match your app.",signIn,"Code",authFiles),
   Code("blazor-browser-auth","wwwroot/appsettings.json",Settings,signIn,"Code",authFiles,"json"),
   Code("blazor-browser-msal","Program.cs",Auth,signIn,"Code",authFiles),
   Action("Set the allowed API and delegated scope","Replace __BACKEND_API_BASE_URL__ with the target API base URL and __BACKEND_SCOPE__ with the actual scope granted for this connection. For example api://<backend-client-id>/Orders.Read is an example, not a selected permission. Install Microsoft.Extensions.Http matching your .NET version if AddHttpClient is unavailable.",call,"Api",apiFiles),
   Code("blazor-browser-client-registration","Program.cs",Registration,call,"Api",apiFiles),
   Code("blazor-browser-api","Services/BackendApiClient.cs",Client,call,"Api",apiFiles),
   Action("Call from an authenticated page","Inject BackendApiClient into an authorized Razor component and invoke GetAsync with your endpoint’s relative path. Catch AccessTokenNotAvailableException and call exception.Redirect() to request interaction. The handler acquires and caches an access token and attaches it only to the configured API URL; an ID token must not be sent to the API. Handle cancellation and HTTP failures in your UI.",call,"Api",apiFiles)
  ];
 }
 private const string Settings="""
{
  "AzureAd": {
    "Authority": "https://login.microsoftonline.com/__TENANT_ID__",
    "ClientId": "__BROWSER_CLIENT_ID__",
    "ValidateAuthority": true
  }
}
""";
 private const string Auth="""
// Program.cs: retain the template's other services and startup code.
builder.Services.AddMsalAuthentication(options =>
{
    builder.Configuration.Bind("AzureAd", options.ProviderOptions.Authentication);
});
""";
 private const string Registration="""
// Program.cs: add these services before builder.Build().RunAsync().
builder.Services.AddTransient<BackendApiAuthorizationHandler>();
builder.Services.AddHttpClient<BackendApiClient>(client =>
    client.BaseAddress = new Uri("__BACKEND_API_BASE_URL__"))
    .AddHttpMessageHandler<BackendApiAuthorizationHandler>();
""";
 private const string Client="""
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

// Adapt the namespace/imports to your app.
public sealed class BackendApiAuthorizationHandler : AuthorizationMessageHandler
{
    public BackendApiAuthorizationHandler(IAccessTokenProvider provider,
        NavigationManager navigation) : base(provider, navigation)
    {
        ConfigureHandler(
            authorizedUrls: ["__BACKEND_API_BASE_URL__"],
            scopes: ["__BACKEND_SCOPE__"]);
    }
}

public sealed class BackendApiClient(HttpClient http)
{
    public async Task<string> GetAsync(string relativePath)
    {
        using var response = await http.GetAsync(relativePath);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
""";
}
