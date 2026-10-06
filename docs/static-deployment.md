# Static deployment

The advisor is a standalone .NET 10 Blazor WebAssembly app. MudBlazor, the decision engine, guide generation, session state and exports run in the browser. It has no backend, database, server circuit or SignalR dependency. Session progress is temporary and resets on reload. Export guides before leaving.

## Local development

Restore and build the solution, then run `./scripts/Start-Advisor.ps1`. The development file server serves the client at http://localhost:5077. Aspire remains optional local tooling and is not deployed.

## Publish

```powershell
./scripts/Publish-StaticSite.ps1
# GitHub repository Pages site:
./scripts/Publish-StaticSite.ps1 -BasePath /EntraOauthFlowGuide/
```

Upload only the contents of `artifacts/static-site/wwwroot`. This includes the runtime, application assemblies, CSS, JavaScript and routing files. No ASP.NET Core process is required. The publisher accepts an alternative `-OutputPath`.

The release build intentionally disables trimming: the existing scenario/plan contracts use reflection-based JSON serialization. This preserves guide integrity at the cost of a larger initial download. Enable trimming only after providing metadata for every serialized contract and verifying published browser execution.

## GitHub Pages

Use an Actions-based Pages deployment to upload the published `wwwroot` as a Pages artifact. Set BasePath to the repository URL directory, including both slashes; use `/` for a root custom domain or account site. The publisher creates `.nojekyll` so `_framework` and `_content` are served and `404.html` for direct client routes. Unknown direct routes are served with GitHub’s 404 status but the client router can display the app’s not-found view. No hosting workflow or public deployment has been enabled yet.

## Azure Static Web Apps

Deploy the same published `wwwroot`, normally with BasePath `/`. Use an already-built artifact (`skip_app_build: true` when using the Azure deployment action), rather than treating the original server project as a backend. The included `staticwebapp.config.json` rewrites client routes to index.html while excluding framework assets, and declares the WebAssembly MIME type. No API location or deployment secret is required by the application itself; the deployment service will need its normal publishing authorization.

## Verification

Verify the Release artifact on a static file server, not just the .NET development host. Check questionnaire decisions, guide generation, completion, the diagram dialog and downloaded exports. Check both root hosting and a repository subpath. No real Entra credentials or tokens are acquired by this advisor; exported validation scripts are separate instructions run by the user.

Sources: [standalone hosting](https://learn.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/webassembly/?view=aspnetcore-10.0), [Azure Static Web Apps](https://learn.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/webassembly/azure-static-web-apps?view=aspnetcore-10.0), [GitHub Pages](https://learn.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/webassembly/github-pages?view=aspnetcore-10.0).
