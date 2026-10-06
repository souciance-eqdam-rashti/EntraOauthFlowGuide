# Technical stack and verification sources

Reviewed: 2026-10-05, checkpoint 1.2. These sources establish the foundation and library families. OAuth rules and generated authentication snippets require their own later review dates and compilation/tenant evidence.

| Dependency | Selected version | Purpose / evidence |
| --- | --- | --- |
| .NET SDK | 10.0.400, latest patch roll-forward | Installed SDK; net10.0 targets; global.json pins baseline |
| ASP.NET Core | .NET 10 shared framework | Server-interactive Blazor host |
| Aspire AppHost SDK | 13.6.0 | Official templates; local web orchestration |
| Fluent UI Blazor | 5.0.0 | Component library; registered services/providers and interactive status button |
| Extensions resilience/service discovery | 10.10.0 | Official Aspire 13.6 ServiceDefaults template |
| OpenTelemetry | 1.17.0 | Official template tracing, metrics and conditional OTLP export |
| xUnit | 2.9.3 | Foundation contracts tests; runner 3.1.4 |
| Microsoft.NET.Test.Sdk | 17.14.1 | .NET 10 built-in xUnit template |
| coverlet.collector | 6.0.4 | Test coverage collector from built-in template |

Commit package lock files for resolved dependency versions. Use locked restore in repeatable validation. No database, containers, credential storage or advisor sign-in is required.

AppHost uses `AspireUseCliBundle=false`: this .NET-only solution obtains dashboard and orchestration binaries through the SDK's NuGet packages, so `dotnet run` does not require a separately installed Aspire CLI. Suppress the corresponding ASPIRE010 advisory explicitly; CLI-dependent features are outside this foundation. See the official SDK documentation below.

## Official sources

- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core): .NET 10 supported LTS baseline.
- [ASP.NET Core Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/?view=aspnetcore-10.0): .NET 10 hosting and render-mode documentation.
- [Aspire SDK](https://aspire.dev/get-started/aspire-sdk/): 13.6 SDK declaration, generated project references and optional CLI bundle.
- [Aspire SDK templates](https://aspire.dev/get-started/aspire-sdk-templates/): AppHost and ServiceDefaults scaffolding.
- [Aspire ServiceDefaults](https://aspire.dev/get-started/csharp-service-defaults/): health checks, telemetry and hosting responsibilities.
- [Fluent UI Blazor repository](https://github.com/microsoft/fluentui-blazor): v5 service registration, stylesheet, providers and interactivity.
- [Fluent UI package](https://www.nuget.org/packages/Microsoft.FluentUI.AspNetCore.Components/5.0.0): selected package metadata.
- [Microsoft.Identity.Web](https://learn.microsoft.com/en-us/entra/msidweb/overview): planned ASP.NET Core authentication guide family.
- [MSAL.NET acquisition](https://learn.microsoft.com/en-us/entra/msal/dotnet/acquiring-tokens/overview): planned desktop/CLI guide family.
- [MSAL browser](https://learn.microsoft.com/en-us/entra/msal/javascript/browser/about-msal-browser): planned browser guide family.

Authentication package versions will be selected, pinned and compilation-tested when their guide templates are implemented. These libraries are not required in the advisor host simply to describe target applications.

## Validation scope

Foundation checks cover dependency restore, solution compilation, hosted page/static assets and Aspire child-resource health. Full keyboard/screen-reader acceptance, interactive journey, OAuth rules, generated snippets and tenant verification remain later milestone work. Do not interpret a foundation build as authentication verification.

Observed local runtime limitation: Aspire 13.6 emitted one-minute idle Container/ContainerExec watcher timeouts. The .NET web resource remained healthy and served requests throughout observation. Loopback proxy exclusion did not eliminate the diagnostic; no containers are modeled. Retain this limitation in milestone reports and revisit before adding containers. No authentication or tenant behavior was tested during this check.

Milestone 3 authentication sample packages: Microsoft.Identity.Web and Microsoft.Identity.Web.UI 4.16.0, pinned and compiled against net10.0. These are generated sample dependencies, not authentication on the advisor itself. See [vertical slice](vertical-slice.md) for sources and smoke-test limits.

