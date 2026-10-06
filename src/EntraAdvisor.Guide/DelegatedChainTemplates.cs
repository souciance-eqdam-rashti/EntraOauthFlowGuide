using System.Collections.Immutable;
using EntraAdvisor.Guide.Contracts;
namespace EntraAdvisor.Guide;
internal static class DelegatedChainTemplates
{
    public static ImmutableArray<CodeArtifact> Create(string web,string apiA,string apiB)
    {
        var files=new List<CodeArtifact>();
        void Add(string component,string path,string language,string text) => files.Add(new(path,component,language,path,text));
        foreach(var entry in new[]{(Id:web,Dir:"Web"),(Id:apiA,Dir:"ApiA"),(Id:apiB,Dir:"ApiB")}) {
            Add(entry.Id,$"{entry.Dir}/{entry.Dir}.csproj","xml", """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.Identity.Web" Version="4.16.0" />
                """+(entry.Id==web?"\n    <PackageReference Include=\"Microsoft.Identity.Web.UI\" Version=\"4.16.0\" />":"")+"\n  </ItemGroup>\n</Project>");
            var isWeb=entry.Id==web;var middle=entry.Id==apiA;
            var client=isWeb?"__WEB_CLIENT_ID__":middle?"__API_A_CLIENT_ID__":"__API_B_CLIENT_ID__";
            var thumb=isWeb?"__WEB_CERT_THUMBPRINT__":"__API_A_CERT_THUMBPRINT__";
            var config="{\n  \"AzureAd\": {\n    \"Instance\": \"https://login.microsoftonline.com/\",\n    \"TenantId\": \"__TENANT_ID__\",\n    \"ClientId\": \""+client+"\"";
            if(isWeb) config+=",\n    \"CallbackPath\": \"/signin-oidc\",\n    \"SignedOutCallbackPath\": \"/signout-callback-oidc\"";
            if(isWeb || middle) config+=",\n    \"ClientCertificates\": [{\n      \"SourceType\": \"StoreWithThumbprint\",\n      \"CertificateStorePath\": \"CurrentUser/My\",\n      \"CertificateThumbprint\": \""+thumb+"\"\n    }]";
            config+="\n  }";
            if(!isWeb) config+=",\n  \"RequiredScope\": \""+(middle?"__API_A_SCOPE__":"__API_B_SCOPE__")+"\"";
            if(isWeb || middle) config+=",\n  \"Downstream\": {\n    \"BaseUrl\": \"https://localhost:"+(isWeb?"7301":"7302")+"/\",\n    \"Scope\": \"api://"+(isWeb?"__API_A_CLIENT_ID__/__API_A_SCOPE__":"__API_B_CLIENT_ID__/__API_B_SCOPE__")+"\"\n  }";
            config+="\n}\n";
            Add(entry.Id,entry.Dir+"/appsettings.json","json",config);
        }
        Add(web,"Web/Program.cs","csharp","""
            using Microsoft.AspNetCore.Authentication.OpenIdConnect;
            using Microsoft.Identity.Web;
                using Microsoft.Identity.Web.Resource;
                using Microsoft.Identity.Client;
            using Microsoft.Identity.Web.UI;
            using Web.Components;
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
                .EnableTokenAcquisitionToCallDownstreamApi(new[] { builder.Configuration["Downstream:Scope"]! })
                .AddInMemoryTokenCaches(); // Local development only.
            builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options => options.UsePkce = true);
            builder.Services.AddAuthorization();
            builder.Services.AddControllersWithViews().AddMicrosoftIdentityUI();
            builder.Services.AddRazorComponents().AddInteractiveServerComponents();
            builder.Services.AddCascadingAuthenticationState();
            builder.Services.AddHttpClient("downstream", client => client.BaseAddress = new Uri(builder.Configuration["Downstream:BaseUrl"]!));
            var app = builder.Build();
            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseAntiforgery();
            app.MapControllers();
            app.MapStaticAssets();
            app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
            app.Run();
            """);
        Add(web,"Web/Components/App.razor","razor","""
            @using Microsoft.AspNetCore.Components.Web
            <!DOCTYPE html>
            <html lang="en"><head><meta charset="utf-8"/><meta name="viewport" content="width=device-width, initial-scale=1"/><base href="/"/><HeadOutlet/></head>
            <body><Routes/><script src="_framework/blazor.web.js"></script></body></html>
            """);
        Add(web,"Web/Components/Routes.razor","razor","""
            @using Microsoft.AspNetCore.Components.Routing
            <Router AppAssembly="typeof(Program).Assembly"><Found Context="routeData"><RouteView RouteData="routeData"/><FocusOnNavigate RouteData="routeData" Selector="h1"/></Found><NotFound><h1>Page not found</h1></NotFound></Router>
            """);
        Add(web,"Web/Components/Pages/Home.razor","razor","""
            @page "/"
            @using Microsoft.AspNetCore.Components
            @using Microsoft.AspNetCore.Components.Authorization
            @using static Microsoft.AspNetCore.Components.Web.RenderMode
            @rendermode InteractiveServer
            @inject NavigationManager Navigation
            <h1>Delegated API chain</h1>
            <AuthorizeView><Authorized><p>Signed in.</p><button @onclick="Call">Call API chain</button><a href="/MicrosoftIdentity/Account/SignOut">Sign out</a></Authorized><NotAuthorized><a href="/MicrosoftIdentity/Account/SignIn">Sign in</a></NotAuthorized></AuthorizeView>
            @code { private void Call() => Navigation.NavigateTo("/call-api", forceLoad: true); }
            """);
        Add(web,"Web/Controllers/CallController.cs","csharp","""
            using System.Net;
            using System.Net.Http.Headers;
            using Microsoft.AspNetCore.Authorization;
            using Microsoft.AspNetCore.Mvc;
            using Microsoft.Identity.Web;
                using Microsoft.Identity.Web.Resource;
                using Microsoft.Identity.Client;
            namespace Web.Controllers;
            [Authorize]
            public sealed class CallController(ITokenAcquisition tokens, IHttpClientFactory clients, IConfiguration config) : Controller
            {
                [HttpGet("/call-api")]
                [AuthorizeForScopes(ScopeKeySection = "Downstream:Scope")]
                public async Task<IActionResult> Call()
                {
                    var scopes = new[] { config["Downstream:Scope"]! };
                    var token = await tokens.GetAccessTokenForUserAsync(scopes, user: User);
                    using var request = new HttpRequestMessage(HttpMethod.Get, "data");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    using var response = await clients.CreateClient("downstream").SendAsync(request, HttpContext.RequestAborted);
                    if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                    {
                        var claims = WwwAuthenticateParameters.GetClaimChallengeFromResponseHeaders(response.Headers);
                        if (!string.IsNullOrEmpty(claims) && !Request.Query.ContainsKey("challengeResolved"))
                        {
                            await tokens.GetAccessTokenForUserAsync(scopes, user: User, tokenAcquisitionOptions: new TokenAcquisitionOptions { Claims = claims, ForceRefresh = true });
                            // Restart the browser request after satisfying the challenge; never loop automatically.
                            return Redirect("/call-api?challengeResolved=true");
                        }
                    }
                    if (!response.IsSuccessStatusCode) return StatusCode((int)response.StatusCode, "Downstream call failed. Check scope, consent and tenant policy; do not log bearer tokens.");
                    return Content(await response.Content.ReadAsStringAsync(HttpContext.RequestAborted), "application/json");
                }
            }
            """);
        foreach(var entry in new[]{(Id:apiA,Dir:"ApiA",Middle:true),(Id:apiB,Dir:"ApiB",Middle:false)}) {
            Add(entry.Id,entry.Dir+"/Program.cs","csharp","""
                using Microsoft.AspNetCore.Authentication.JwtBearer;
                using Microsoft.Identity.Web;
                using Microsoft.Identity.Web.Resource;
                using Microsoft.Identity.Client;
                var builder = WebApplication.CreateBuilder(args);
                var identity = builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
                """+(entry.Middle?"\nidentity.EnableTokenAcquisitionToCallDownstreamApi().AddInMemoryTokenCaches();\nbuilder.Services.AddHttpClient(\"downstream\", client => client.BaseAddress = new Uri(builder.Configuration[\"Downstream:BaseUrl\"]!));":"")+"\nbuilder.Services.AddAuthorization();\nbuilder.Services.AddControllers();\nvar app = builder.Build();\napp.UseHttpsRedirection();\napp.UseAuthentication();\napp.UseAuthorization();\napp.MapControllers();\napp.Run();");
            Add(entry.Id,entry.Dir+"/Controllers/DataController.cs","csharp",entry.Middle?"""
                using System.Net.Http.Headers;
                using Microsoft.AspNetCore.Authorization;
                using Microsoft.AspNetCore.Mvc;
                using Microsoft.Identity.Web;
                using Microsoft.Identity.Web.Resource;
                using Microsoft.Identity.Client;
                namespace ApiA.Controllers;
                [ApiController, Authorize, Route("data")]
                [RequiredScope(RequiredScopesConfigurationKey = "RequiredScope")]
                public sealed class DataController(ITokenAcquisition tokens, IHttpClientFactory clients, IConfiguration config) : ControllerBase
                {
                    [HttpGet]
                    public async Task<IActionResult> Get()
                    {
                        var scopes = new[] { config["Downstream:Scope"]! };
                        try
                        {
                            var token = await tokens.GetAccessTokenForUserAsync(scopes, user: User);
                            using var request = new HttpRequestMessage(HttpMethod.Get, "data");
                            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                            using var response = await clients.CreateClient("downstream").SendAsync(request, HttpContext.RequestAborted);
                            if (!response.IsSuccessStatusCode)
                            {
                                if (response.Headers.WwwAuthenticate.Count > 0) Response.Headers.WWWAuthenticate = string.Join(", ", response.Headers.WwwAuthenticate);
                                return StatusCode((int)response.StatusCode, "API B rejected the request.");
                            }
                            return Content(await response.Content.ReadAsStringAsync(HttpContext.RequestAborted), "application/json");
                        }
                        catch (MicrosoftIdentityWebChallengeUserException ex)
                        {
                            await tokens.ReplyForbiddenWithWwwAuthenticateHeaderAsync(scopes, ex.MsalUiRequiredException, Response);
                            return new EmptyResult();
                        }
                    }
                }
                """:"""
                using Microsoft.AspNetCore.Authorization;
                using Microsoft.AspNetCore.Mvc;
                using Microsoft.Identity.Web;
                using Microsoft.Identity.Web.Resource;
                using Microsoft.Identity.Client;
                namespace ApiB.Controllers;
                [ApiController, Authorize, Route("data")]
                [RequiredScope(RequiredScopesConfigurationKey = "RequiredScope")]
                public sealed class DataController : ControllerBase
                {
                    [HttpGet]
                    public IActionResult Get() => Ok(new { message = "API B authorized the delegated user." });
                }
                """);
        }
        return files.ToImmutableArray();
    }
}




