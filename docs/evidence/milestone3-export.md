# Entra delegated API chain implementation

Plan: plan-606dbdcd517d6cd225d58eb35df75a2b0e55bd128c5dbb6bdbc01a07d28f7e16

Versions: schema 1.1.0, rules 1.0.0, templates 1.0.0

## Architecture

- Orders API: AspNetCoreApi
- Inventory API: AspNetCoreApi
- Web App: BlazorServer
- orders-inventory: DelegatedUser, OnBehalfOf, audience `inventory.audience`, authorization `inventory.scope`
- web-orders: DelegatedUser, AuthorizationCode, audience `orders.audience`, authorization `orders.scope`

## Assumptions

- Workforce identities in the public cloud; no consumer or External ID customer configuration.
- Exact Graph permissions, Azure authorization details and custom scopes/app roles are developer-supplied values.
- Ready describes architectural completeness, not verified permissions, tenant state or resource compatibility.
- Windows local development; single workforce tenant, member users, fresh registrations, certificate credentials and explicit admin consent.

## Prepare the tenant and value sheet

Step ID: prerequisites · component: web

Verify access before creating three new registrations.

Action: Verify access before creating three new registrations.

Use a workforce test tenant. Record its Directory (tenant) ID as __TENANT_ID__. Never put tokens or private keys into this advisor. App-registration creation depends on tenant settings; Application Developer can create owned registrations when ordinary user creation is disabled. Editing an owned app is different from granting tenant-wide consent. Ask an authorized tenant administrator to grant the custom delegated consent below under your tenant policy; do not assume registration ownership grants consent authority.

Install .NET SDK 10.0.400 or a compatible .NET 10 SDK. These Windows local-development samples use CurrentUser/My certificates and localhost HTTPS ports 7300, 7301, 7302. Trust the development HTTPS certificate with dotnet dev-certs https --trust. The two authentication certificates are separate from the HTTPS certificate.

**Architecture prerequisites**

A token must be acquired for this resource's audience. It cannot be reused for a different downstream resource. Supply inventory.audience and the corresponding target scope/resource endpoint.
Choose only the target delegated permissions or custom scopes required; confirm applicable user/admin consent policy. Supply inventory.scope; configure and test the target's least-privilege authorization.
Downstream consent and Conditional Access may require interaction through the initiating client. Implement supported claims-challenge/consent handling and never forward the incoming API token downstream.
Confirm Inventory supports DelegatedUser access with Certificate credentials. Check the target's official authentication documentation before implementing this conditional recommendation.
Delegated consent availability depends on selected permission definitions and user/admin consent policy. Identify the consent actor and verify the permission definition and tenant policy; do not assume an app-registration owner can grant consent.
A token must be acquired for this resource's audience. It cannot be reused for a different downstream resource. Supply orders.audience and the corresponding target scope/resource endpoint.
Choose only the target delegated permissions or custom scopes required; confirm applicable user/admin consent policy. Supply orders.scope; configure and test the target's least-privilege authorization.
Confirm Orders supports DelegatedUser access with Certificate credentials. Check the target's official authentication documentation before implementing this conditional recommendation.
Delegated consent availability depends on selected permission definitions and user/admin consent policy. Identify the consent actor and verify the permission definition and tenant policy; do not assume an app-registration owner can grant consent.
Sign-in and consent must be permitted by the workforce tenant's policies. Verify tenant policy, redirect handling and applicable consent before testing sign-in.

Expected result: Tenant access, administrator support and local .NET 10 tooling are available.

- [Consent and policy](https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/user-admin-consent-overview) (reviewed 2026-10-05)

## Register API B and expose a scope

Step ID: register-api-b · component: api-b

Create a single-tenant API registration and define its delegated scope.

Action: Create a single-tenant API registration and define its delegated scope.

Entra admin center → Identity → Applications → App registrations

New registration → name your API → Accounts in this organizational directory only → Register. No redirect URI is needed for either API.

**API B client ID**: `__API_B_CLIENT_ID__`

Record Application (client) ID from Overview; this is also the v2 token audience.

App registration → Expose an API

Set Application ID URI to api://__API_B_CLIENT_ID__. Add a scope: choose a meaningful least-privilege value and replace __API_B_SCOPE__ everywhere. For this test choose Admins only, fill consent display names/descriptions, enable the scope.

App registration → Manifest

In the Microsoft Graph-format manifest, change only api.requestedAccessTokenVersion to 2. Preserve the existing api scope definitions and all other fields; do not replace the entire manifest.

### Manifest fragment (merge only)

```json
{
  "api": {
    "requestedAccessTokenVersion": 2
  }
}
```

Expected result: A new registration exposes one enabled delegated scope and requests v2 access tokens.

- [API scope enforcement](https://learn.microsoft.com/en-us/entra/msidweb/authentication/authorization) (reviewed 2026-10-05)
- [API access-token version](https://learn.microsoft.com/en-us/graph/api/resources/apiapplication?view=graph-rest-1.0) (reviewed 2026-10-05)

## Register API A and expose a scope

Step ID: register-api-a · component: api-a

Create a single-tenant API registration and define its delegated scope.

Action: Create a single-tenant API registration and define its delegated scope.

Entra admin center → Identity → Applications → App registrations

New registration → name your API → Accounts in this organizational directory only → Register. No redirect URI is needed for either API.

**API A client ID**: `__API_A_CLIENT_ID__`

Record Application (client) ID from Overview; this is also the v2 token audience.

App registration → Expose an API

Set Application ID URI to api://__API_A_CLIENT_ID__. Add a scope: choose a meaningful least-privilege value and replace __API_A_SCOPE__ everywhere. For this test choose Admins only, fill consent display names/descriptions, enable the scope.

App registration → Manifest

In the Microsoft Graph-format manifest, change only api.requestedAccessTokenVersion to 2. Preserve the existing api scope definitions and all other fields; do not replace the entire manifest.

### Manifest fragment (merge only)

```json
{
  "api": {
    "requestedAccessTokenVersion": 2
  }
}
```

Expected result: A new registration exposes one enabled delegated scope and requests v2 access tokens.

- [API scope enforcement](https://learn.microsoft.com/en-us/entra/msidweb/authentication/authorization) (reviewed 2026-10-05)
- [API access-token version](https://learn.microsoft.com/en-us/graph/api/resources/apiapplication?view=graph-rest-1.0) (reviewed 2026-10-05)

## Register the server web app

Step ID: register-web · component: web

Create a confidential single-tenant Web registration.

Action: Create a confidential single-tenant Web registration.

Entra admin center → Identity → Applications → App registrations

New registration → Accounts in this organizational directory only. Authentication → Add a platform → Web → add redirect URIs https://localhost:7300/signin-oidc and https://localhost:7300/signout-callback-oidc. Set front-channel logout URL to https://localhost:7300/signout-oidc. Keep implicit grants and public client flows disabled.

**Web client ID**: `__WEB_CLIENT_ID__`

Record its Application (client) ID. The browser uses the server session cookie; token acquisition happens on the server. No manual manifest edit is required for this Web registration.

Expected result: Web redirect and logout callbacks match the local host.

- [Certificate configuration](https://learn.microsoft.com/en-us/entra/msidweb/authentication/certificates) (reviewed 2026-10-05)
- [Blazor with Entra](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/blazor-web-app-with-entra?view=aspnetcore-10.0) (reviewed 2026-10-05)

## Create and upload two development certificates

Step ID: certificates · component: web

Use distinct certificates for Web and API A, both of which acquire tokens.

Action: Use distinct certificates for Web and API A, both of which acquire tokens.

Run the following locally under the account that will run the sample. Replace __WEB_CERT_THUMBPRINT__ and __API_A_CERT_THUMBPRINT__ with the displayed values. Upload only the exported .cer files in each registration's Certificates & secrets → Certificates → Upload certificate. API B validates incoming tokens and needs no client credential.

### Create-DevelopmentCertificates.ps1

```powershell
foreach ($name in @('Web', 'ApiA')) {
  $cert = New-SelfSignedCertificate -Subject "CN=EntraAdvisor-$name" -CertStoreLocation 'Cert:\CurrentUser\My' -KeySpec Signature -KeyExportPolicy NonExportable -NotAfter (Get-Date).AddMonths(3)
  Export-Certificate -Cert $cert -FilePath "$name.cer" | Out-Null
  Write-Output "$name thumbprint: $($cert.Thumbprint)"
}
```

**Production credentials and caches**

These development certificates and in-memory token caches are for a local test only. Production requires a protected certificate source with rotation, process access to the private key, shared encrypted token caching and protected shared Data Protection keys for multiple server instances. Choose a supported certificate store or Key Vault integration before deployment. Do not substitute a browser secret.

Expected result: Each public certificate is uploaded to its owning registration; private keys remain in the Windows certificate store.

- [Certificate configuration](https://learn.microsoft.com/en-us/entra/msidweb/authentication/certificates) (reviewed 2026-10-05)
- [Blazor with Entra](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/blazor-web-app-with-entra?view=aspnetcore-10.0) (reviewed 2026-10-05)

## Grant the two delegated permissions

Step ID: permissions · component: api-a

Configure each caller against its immediate target.

Action: Configure each caller against its immediate target.

App registrations → API permissions

Web registration → API permissions → Add a permission → My APIs → API A → Delegated permissions → your API A scope. API A registration → API permissions → Add a permission → My APIs → API B → Delegated permissions → your API B scope. Have an authorized administrator grant admin consent for each registration in this tenant.

**Web → API A scope**: `api://__API_A_CLIENT_ID__/__API_A_SCOPE__`

Use the scope value defined on API A.

**API A → API B scope**: `api://__API_B_CLIENT_ID__/__API_B_SCOPE__`

Use the scope value defined on API B. The first token cannot be forwarded to API B.

Remove template permissions you do not use, such as User.Read, after checking dependencies. No Graph or Azure permissions are required for this custom API chain. Each API must still enforce its own delegated scope; consent alone does not authorize a route.

Expected result: Web has delegated API A scope; API A has delegated API B scope; administrator consent is recorded.

- [Consent and policy](https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/user-admin-consent-overview) (reviewed 2026-10-05)

## Create the three projects

Step ID: projects · component: web

Create empty directories Web, ApiA and ApiB and copy the project files.

Action: Create empty directories Web, ApiA and ApiB and copy the project files.

Use the supplied complete project files. Paths are relative to one common sample directory. Create every parent directory shown. No existing registration or project is assumed.

### Web/Web.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Identity.Web" Version="4.16.0" />
    <PackageReference Include="Microsoft.Identity.Web.UI" Version="4.16.0" />
  </ItemGroup>
</Project>
```

### ApiA/ApiA.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Identity.Web" Version="4.16.0" />
  </ItemGroup>
</Project>
```

### ApiB/ApiB.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Identity.Web" Version="4.16.0" />
  </ItemGroup>
</Project>
```

Expected result: Pinned Microsoft.Identity.Web 4.16.0 dependencies restore successfully.

- [API scope enforcement](https://learn.microsoft.com/en-us/entra/msidweb/authentication/authorization) (reviewed 2026-10-05)
- [Blazor with Entra](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/blazor-web-app-with-entra?view=aspnetcore-10.0) (reviewed 2026-10-05)

## Configure Web App

Step ID: configure-web · component: web

Copy the complete files and replace every __PLACEHOLDER__ consistently.

Action: Copy the complete files and replace every __PLACEHOLDER__ consistently.

### Web/appsettings.json

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "__TENANT_ID__",
    "ClientId": "__WEB_CLIENT_ID__",
    "CallbackPath": "/signin-oidc",
    "SignedOutCallbackPath": "/signout-callback-oidc",
    "ClientCertificates": [{
      "SourceType": "StoreWithThumbprint",
      "CertificateStorePath": "CurrentUser/My",
      "CertificateThumbprint": "__WEB_CERT_THUMBPRINT__"
    }]
  },
  "Downstream": {
    "BaseUrl": "https://localhost:7301/",
    "Scope": "api://__API_A_CLIENT_ID__/__API_A_SCOPE__"
  }
}

```

### Web/Program.cs

```csharp
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
```

### Web/Components/App.razor

```razor
@using Microsoft.AspNetCore.Components.Web
<!DOCTYPE html>
<html lang="en"><head><meta charset="utf-8"/><meta name="viewport" content="width=device-width, initial-scale=1"/><base href="/"/><HeadOutlet/></head>
<body><Routes/><script src="_framework/blazor.web.js"></script></body></html>
```

### Web/Components/Routes.razor

```razor
@using Microsoft.AspNetCore.Components.Routing
<Router AppAssembly="typeof(Program).Assembly"><Found Context="routeData"><RouteView RouteData="routeData"/><FocusOnNavigate RouteData="routeData" Selector="h1"/></Found><NotFound><h1>Page not found</h1></NotFound></Router>
```

### Web/Components/Pages/Home.razor

```razor
@page "/"
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Authorization
@using static Microsoft.AspNetCore.Components.Web.RenderMode
@rendermode InteractiveServer
@inject NavigationManager Navigation
<h1>Delegated API chain</h1>
<AuthorizeView><Authorized><p>Signed in.</p><button @onclick="Call">Call API chain</button><a href="/MicrosoftIdentity/Account/SignOut">Sign out</a></Authorized><NotAuthorized><a href="/MicrosoftIdentity/Account/SignIn">Sign in</a></NotAuthorized></AuthorizeView>
@code { private void Call() => Navigation.NavigateTo("/call-api", forceLoad: true); }
```

### Web/Controllers/CallController.cs

```csharp
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
```

Expected result: The component builds with authentication, target-specific token acquisition and delegated authorization configured.

- [API scope enforcement](https://learn.microsoft.com/en-us/entra/msidweb/authentication/authorization) (reviewed 2026-10-05)
- [Blazor with Entra](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/blazor-web-app-with-entra?view=aspnetcore-10.0) (reviewed 2026-10-05)

## Configure Orders API

Step ID: configure-api-a · component: api-a

Copy the complete files and replace every __PLACEHOLDER__ consistently.

Action: Copy the complete files and replace every __PLACEHOLDER__ consistently.

### ApiA/appsettings.json

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "__TENANT_ID__",
    "ClientId": "__API_A_CLIENT_ID__",
    "ClientCertificates": [{
      "SourceType": "StoreWithThumbprint",
      "CertificateStorePath": "CurrentUser/My",
      "CertificateThumbprint": "__API_A_CERT_THUMBPRINT__"
    }]
  },
  "RequiredScope": "__API_A_SCOPE__",
  "Downstream": {
    "BaseUrl": "https://localhost:7302/",
    "Scope": "api://__API_B_CLIENT_ID__/__API_B_SCOPE__"
  }
}

```

### ApiA/Program.cs

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.Resource;
using Microsoft.Identity.Client;
var builder = WebApplication.CreateBuilder(args);
var identity = builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
identity.EnableTokenAcquisitionToCallDownstreamApi().AddInMemoryTokenCaches();
builder.Services.AddHttpClient("downstream", client => client.BaseAddress = new Uri(builder.Configuration["Downstream:BaseUrl"]!));
builder.Services.AddAuthorization();
builder.Services.AddControllers();
var app = builder.Build();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

### ApiA/Controllers/DataController.cs

```csharp
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
```

Expected result: The component builds with authentication, target-specific token acquisition and delegated authorization configured.

- [API scope enforcement](https://learn.microsoft.com/en-us/entra/msidweb/authentication/authorization) (reviewed 2026-10-05)
- [Blazor with Entra](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/blazor-web-app-with-entra?view=aspnetcore-10.0) (reviewed 2026-10-05)

## Configure Inventory API

Step ID: configure-api-b · component: api-b

Copy the complete files and replace every __PLACEHOLDER__ consistently.

Action: Copy the complete files and replace every __PLACEHOLDER__ consistently.

### ApiB/appsettings.json

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "__TENANT_ID__",
    "ClientId": "__API_B_CLIENT_ID__"
  },
  "RequiredScope": "__API_B_SCOPE__"
}

```

### ApiB/Program.cs

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.Resource;
using Microsoft.Identity.Client;
var builder = WebApplication.CreateBuilder(args);
var identity = builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
builder.Services.AddAuthorization();
builder.Services.AddControllers();
var app = builder.Build();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

### ApiB/Controllers/DataController.cs

```csharp
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
```

Expected result: The component builds with authentication, target-specific token acquisition and delegated authorization configured.

- [API scope enforcement](https://learn.microsoft.com/en-us/entra/msidweb/authentication/authorization) (reviewed 2026-10-05)
- [Blazor with Entra](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/blazor-web-app-with-entra?view=aspnetcore-10.0) (reviewed 2026-10-05)

## Build and run the chain

Step ID: run · component: web

Build each project, then run it in a separate terminal.

Action: Build each project, then run it in a separate terminal.

### Terminal commands

```powershell
dotnet build Web/Web.csproj
dotnet build ApiA/ApiA.csproj
dotnet build ApiB/ApiB.csproj
# In three separate terminals:
dotnet run --project ApiB --urls https://localhost:7302
dotnet run --project ApiA --urls https://localhost:7301
dotnet run --project Web --urls https://localhost:7300
```

Open https://localhost:7300. Sign in as a member test user and choose Call API chain. Web acquires an API A token; API A exchanges the incoming user context through OBO for an API B token. API B's accepted audience is its own client ID. Inspect status and trace IDs locally; do not print bearer tokens or paste them into third-party decoders.

Expected result: The HTTPS hosts are reachable; signing in to Web and choosing Call API chain returns API B's protected response.

- [API scope enforcement](https://learn.microsoft.com/en-us/entra/msidweb/authentication/authorization) (reviewed 2026-10-05)
- [Blazor with Entra](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/blazor-web-app-with-entra?view=aspnetcore-10.0) (reviewed 2026-10-05)

## Verify failures and record evidence

Step ID: negative-tests · component: api-b

Test authentication, authorization and consent separately.

Action: Test authentication, authorization and consent separately.

No token → GET https://localhost:7302/data must return 401. A genuine token issued for API A sent to API B must return 401 (wrong audience). A valid API B delegated token without the required scope must return 403. Use a controlled test client and server-side acquisition to obtain these tokens; never disable validation to manufacture a passing result. App-only tokens must not pass the delegated scope requirement.

Revoke API A's API B consent in the test tenant, clear the local process token caches by restarting Web and API A, and retry. Expect an administrator-consent or claims challenge, not successful API B access. Denied consent is a prerequisite failure. Restore consent and repeat the positive test.

401: check tenant, issuer, audience, token version and token expiry. 403: check scp and target scope, consent, user policy and conditional access. The sample propagates an OBO claims challenge to the Web authentication handler. If policy cannot be satisfied, record the blocker and contact the tenant administrator; do not retry in a loop or substitute application permissions.

**Verification limits**

Compilation verifies API and syntax compatibility only. This template has not yet been run against the supplied test tenant. Record tenant verification evidence in milestone 5 before treating it as production validated.

Expected result: Positive chain result and expected 401/403 outcomes are recorded without token disclosure.

- [API scope enforcement](https://learn.microsoft.com/en-us/entra/msidweb/authentication/authorization) (reviewed 2026-10-05)
- [Blazor with Entra](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/blazor-web-app-with-entra?view=aspnetcore-10.0) (reviewed 2026-10-05)
