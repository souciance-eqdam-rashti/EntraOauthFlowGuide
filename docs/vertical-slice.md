# Milestone 3 vertical slice

The default journey supports Design → Recommendation → Implement for a single workforce tenant, member users, Blazor server → API A → API B, delegated access and certificate credentials. The engine still evaluates its wider milestone 2 coverage. All ready architectures now enter Implementation with ordered configuration checklists; the original delegated chain retains its complete runnable samples. Additional runnable templates remain milestone 4 work.

`AdvisorWorkspace` is scoped to the running standalone browser app. It stores architecture answers and checklist state, not access tokens or credentials. Refreshing or closing the browser app loses session state; Markdown includes a completion record for reference. Reopening a completed prerequisite clears dependent confirmations. Changing an architectural answer removes stale recommendations and resets affected guide steps; regenerating never revives invalidated completion.

`DelegatedChainGuideGenerator` re-evaluates the scenario and compares the entire plan before generation. It emits typed steps and complete project artifacts from one source template. The UI and `MarkdownGuideExporter` consume that same content. Deployment values remain labeled `__PLACEHOLDERS__`; the advisor does not create registrations, generate private keys or grant consent.

The sample is a .NET 10 Blazor Web App with server interactivity. Its button performs a full navigation to a protected MVC endpoint for token acquisition and downstream calls. This deliberately uses request-scoped authentication and the library's MVC consent/claims handling; it avoids acquiring tokens from an expired circuit HttpContext. API A uses OBO with a separate certificate; API B validates its own audience and delegated scope. Downstream claims challenges propagate to Web, with one bounded reauthentication attempt. Certificates use Windows CurrentUser/My for local development. Production caching, certificate protection and rotation require separate deployment choices.

## Reproduce sample compilation

From the repository root:

```powershell
dotnet restore tools/SampleWriter/SampleWriter.csproj --locked-mode
dotnet build tools/SampleWriter/SampleWriter.csproj --no-restore -m:1 -p:UseSharedCompilation=false
dotnet tools/SampleWriter/bin/Debug/net10.0/SampleWriter.dll
dotnet restore .artifacts/generated-samples/Web/Web.csproj
dotnet restore .artifacts/generated-samples/ApiA/ApiA.csproj
dotnet restore .artifacts/generated-samples/ApiB/ApiB.csproj
dotnet build .artifacts/generated-samples/Web/Web.csproj --no-restore -m:1 -p:UseSharedCompilation=false
dotnet build .artifacts/generated-samples/ApiA/ApiA.csproj --no-restore -m:1 -p:UseSharedCompilation=false
dotnet build .artifacts/generated-samples/ApiB/ApiB.csproj --no-restore -m:1 -p:UseSharedCompilation=false
```

This generates the exact guide artifacts and Markdown into ignored `.artifacts/generated-samples`. Compilation validates syntax/library compatibility, not tenant behavior. Tenant-backed positive, wrong-audience, missing-scope and denied-consent verification remains milestone 5.

## Template sources

Reviewed 2026-10-05: [certificate configuration](https://learn.microsoft.com/en-us/entra/msidweb/authentication/certificates), [API scope enforcement](https://learn.microsoft.com/en-us/entra/msidweb/authentication/authorization), [Graph-format API manifest property](https://learn.microsoft.com/en-us/graph/api/resources/apiapplication?view=graph-rest-1.0), [Blazor with Entra](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/blazor-web-app-with-entra?view=aspnetcore-10.0), [OBO](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-on-behalf-of-flow). Package compile target: Microsoft.Identity.Web and Microsoft.Identity.Web.UI 4.16.0.

## Picker refinements after user review

The initial topology picker uses a light theme, concise real-world examples and immediate navigation into relevant Design questions. Required facts must still be answered before Recommendation is available. The architecture sidebar is hidden until implementation; later it tracks resource calls and highlights the component being configured. It remains collapsible on narrow screens. These are milestone 3 refinements, not approval to begin milestone 4.


## Current source locations

The runnable chain generator and templates are in src/EntraAdvisor.Guide/Features/GenerateDelegatedChain. This document describes a complete product capability; the advisor now organizes UI/workflows by feature as described in [hybrid architecture](hybrid-architecture.md).
