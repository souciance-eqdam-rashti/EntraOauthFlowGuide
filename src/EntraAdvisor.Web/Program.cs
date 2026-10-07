using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using EntraAdvisor.Web.Components;
using EntraAdvisor.Web.State;
using EntraAdvisor.Web.Features.DesignArchitecture;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddMudServices();
builder.Services.AddScoped<AdvisorWorkspace>();
builder.Services.AddScoped<DesignJourneyState>();
builder.Services.AddScoped<EntraAdvisor.Web.Infrastructure.Browser.BrowserDownload>();
await builder.Build().RunAsync();
