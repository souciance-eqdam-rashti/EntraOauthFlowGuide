using System.Collections.Immutable;
using EntraAdvisor.Engine.Contracts;

namespace EntraAdvisor.Engine.Questionnaire;

public static class QuestionCatalog
{
    private static string Id(string entity, string fact) => ArchitectureEvaluator.QuestionId(new(entity, fact));

    private static QuestionDefinition Choice(string entity, string name, string section, string prompt, IEnumerable<QuestionOption> options,
        IEnumerable<string>? dependencies = null, IEnumerable<QuestionCondition>? relevance = null) => new()
    {
        Id = Id(entity, name), SuppliesFact = new(entity, name), Section = section, Prompt = prompt,
        Options = options.ToImmutableArray(), DependsOnQuestionIds = dependencies?.Distinct().ToImmutableArray() ?? [], Relevance = relevance?.ToImmutableArray() ?? [],
        HelpText = "Answer from your application's architecture. Exact permissions and resource roles are supplied during implementation."
    };

    private static QuestionOption Option<T>(T value, string title, string description) where T : notnull => new(value.ToString()!, title, description, "choice");
    private static IEnumerable<QuestionOption> BooleanOptions(string yes, string no) =>
        [new("True", "Yes", yes, "check"), new("False", "No", no, "minus")];

    public static ImmutableArray<QuestionDefinition> ForScenario(ArchitectureScenario scenario)
    {
        var questions = new List<QuestionDefinition>
        {
            new() { Id = "architecture.structure", SuppliesFact = new("architecture", "Structure"), Section = "Design", Prompt = "Choose an architecture to start from.",
                Options = TopologyPresets.All.Select(p => new QuestionOption(p.Id, p.Title, p.Description, "architecture")).ToImmutableArray() }
        };
        foreach (var component in scenario.Components)
        {
            var id = component.Id;
            var name = component.Name;
            questions.Add(Choice(id, "Kind", "Application", $"What are you building for {name}?",
            [Option(ComponentKind.ServerWeb, "Server web app", "The application executes on a server."), Option(ComponentKind.BrowserSpa, "Browser app", "The application executes in the user's browser."),
                Option(ComponentKind.Api, "API", "Callers send access tokens to this API."), Option(ComponentKind.BackgroundService, "Background service", "Work runs without interactive user sign-in."),
                Option(ComponentKind.WindowsDesktop, "Windows desktop", "An installed Windows application."), Option(ComponentKind.CliDevice, "Command-line or device", "A console or input-constrained application.")], ["architecture.structure"]));
            var stacks = Enum.GetValues<ImplementationStack>().Where(stack => component.Kind.State != FactState.Known || ScenarioNormalizer.KindFor(stack) == component.Kind.Value);
            questions.Add(Choice(id, "Stack", "Application", $"Which stack runs {name}?", stacks.Select(stack => Option(stack, stack switch
            {
                ImplementationStack.BlazorServer => "Blazor server", ImplementationStack.BlazorWebAssembly => "Blazor WebAssembly", ImplementationStack.AspNetCoreApi => "ASP.NET Core API",
                ImplementationStack.JavaScriptTypeScript => "JavaScript / TypeScript", ImplementationStack.WindowsWpf => "Windows desktop (.NET)", ImplementationStack.DotNetWorker => ".NET worker", _ => ".NET console"
            }, "The execution boundary determines how authentication is configured.")), [Id(id, "Kind")]));
            questions.Add(Choice(id, "Execution", "Application", $"Where does {name} execute?", Enum.GetValues<ExecutionLocation>().Select(value => Option(value, value.ToString(), "Use separate components for browser and server execution.")), [Id(id, "Stack")]));
            questions.Add(Choice(id, "UserSignIn", "User", $"Will a user sign in to {name}?", BooleanOptions("The application has a signed-in user.", "This component does not perform interactive sign-in."), [Id(id, "Kind"), Id(id, "Stack")]));
            questions.Add(Choice(id, "CanProtectCredentials", "Hosting", $"Can {name} keep a server credential protected?", BooleanOptions("A protected server environment controls the credential.", "Users can inspect or extract the client credential."), [Id(id, "Kind"), Id(id, "Stack"), Id(id, "Execution")]));
            var userRelevant = new[] { new QuestionCondition(new(id, "UserSignIn"), ConditionOperator.Equals, "True") };
            questions.Add(Choice(id, "LocalBrowserAvailable", "User", $"Can {name} open a browser for sign-in?", BooleanOptions("The user can sign in using a browser here.", "The application cannot open a local browser."), [Id(id, "Kind"), Id(id, "Stack"), Id(id, "UserSignIn")], userRelevant));
            questions.Add(Choice(id, "AlternateBrowserAvailable", "User", "Can the user sign in using another browser or device?", BooleanOptions("The user has another browser available.", "There is no browser where the user can complete sign-in."), [Id(id, "LocalBrowserAvailable")],
                [.. userRelevant, new(new(id, "LocalBrowserAvailable"), ConditionOperator.Equals, "False")]));
            questions.Add(Choice(id, "DeviceCodePermitted", "User", "Does your tenant permit sign-in using a code on another device?", BooleanOptions("Tenant policy permits this sign-in method.", "Tenant policy prohibits this sign-in method."), [Id(id, "AlternateBrowserAvailable")],
                [.. userRelevant, new(new(id, "LocalBrowserAvailable"), ConditionOperator.Equals, "False"), new(new(id, "AlternateBrowserAvailable"), ConditionOperator.Equals, "True")]));
            questions.Add(Choice(id, "Hosting", "Hosting", $"Where will {name} run?", [Option(HostingEnvironment.Azure, "Azure", "Run on an Azure host."), Option(HostingEnvironment.NonAzure, "Outside Azure", "Run on your own or another provider's host."), Option(HostingEnvironment.LocalDevelopment, "Local development", "Choose a separate production identity strategy later.")], [Id(id, "Kind")]));
            questions.Add(Choice(id, "ManagedIdentityAvailable", "Hosting", "Can this Azure host use a managed identity?", BooleanOptions("The host exposes a managed identity to this application.", "This host does not expose a managed identity."), [Id(id, "Hosting")], [new(new(id, "Hosting"), ConditionOperator.Equals, nameof(HostingEnvironment.Azure))]));
            var incoming = scenario.Relationships.Where(h => scenario.Resources.Any(r => r.Id == h.TargetResourceId && r.ApiComponentId == id)).Select(h => Id(h.Id, "Identity"));
            questions.Add(Choice(id, "IncomingIdentity", "Connections", $"What identity reaches {name} from its callers?",
                [Option(IncomingTokenIdentity.DelegatedUser, "Signed-in user", "Incoming tokens represent a user."), Option(IncomingTokenIdentity.Application, "Application", "Incoming tokens represent an application."), Option(IncomingTokenIdentity.Both, "Both", "Authorize user and application calls separately.")], [Id(id, "Kind"), .. incoming]));
            var outbound = scenario.Relationships.Where(h => h.CallerComponentId == id).Select(h => Id(h.Id, "Identity"));
            questions.Add(Choice(id, "Credential", "Hosting", $"How should {name} authenticate its server identity?",
                [Option(CredentialCapability.Certificate, "Certificate", "Keep the private key protected; upload only the public certificate."), Option(CredentialCapability.WorkloadFederation, "Federated workload identity", "Use a supported external identity trust without a client secret."), Option(CredentialCapability.ManagedIdentity, "Managed identity", "Use a supported Azure host identity for app-only access.")],
                [Id(id, "Kind"), Id(id, "Stack"), Id(id, "UserSignIn"), Id(id, "CanProtectCredentials"), Id(id, "Hosting"), Id(id, "ManagedIdentityAvailable"), .. outbound]));
        }
        foreach (var resource in scenario.Resources)
            questions.Add(Choice(resource.Id, "Category", "Connections", $"What kind of resource is {resource.Name}?",
                [Option(ResourceCategory.MicrosoftGraph, "Microsoft Graph", "Access Microsoft services through Graph."), Option(ResourceCategory.AzureResource, "Azure resources", "Access an Entra-compatible Azure resource."), Option(ResourceCategory.CustomResource, "Custom resources", "Call your own Entra-protected API.")], ["architecture.structure"]));
        questions.Add(Choice("tenants", "Model", "Tenants", "Who can sign in?", [Option(WorkforceTenantModel.SingleTenant, "Single Entra tenant", "Restrict the registration to one organization."), Option(WorkforceTenantModel.Multitenant, "Multi-tenant", "Onboard other organizations with explicit issuer and consent handling.")], ["architecture.structure"]));
        questions.Add(Choice("tenants", "IncludesGuestUsers", "Tenants", "Will guest users sign in through an Entra tenant?", BooleanOptions("Guests use the applicable resource-tenant context.", "Guest-user access is not required."), ["tenants.model"]));
        foreach (var hop in scenario.Relationships)
        {
            var caller = scenario.Components.FirstOrDefault(c => c.Id == hop.CallerComponentId);
            var target = scenario.Resources.FirstOrDefault(r => r.Id == hop.TargetResourceId);
            questions.Add(Choice(hop.Id, "Identity", "Connections", $"Who should {caller?.Name ?? hop.CallerComponentId} act as when calling {target?.Name ?? hop.TargetResourceId}?",
                [Option(ActingIdentity.DelegatedUser, "Signed-in user", "Use the user's delegated access."), Option(ActingIdentity.Application, "The application", "Use application access independently of the initiating user.")], [Id(hop.CallerComponentId, "Kind"), Id(hop.CallerComponentId, "UserSignIn"), Id(hop.TargetResourceId, "Category")]));
            questions.Add(Choice(hop.Id, "TenantBoundary", "Tenants", "Are this caller and resource in the same tenant?",
                [Option(TenantBoundary.SameTenant, "Same tenant", "The caller and resource share an Entra tenant."), Option(TenantBoundary.CrossTenant, "Different tenants", "Resource-tenant compatibility and authorization need verification.")], ["tenants.model", Id(hop.TargetResourceId, "Category")]));
            var incoming = scenario.Relationships.Where(h => scenario.Resources.Any(r => r.Id == h.TargetResourceId && r.ApiComponentId == hop.CallerComponentId));
            questions.Add(Choice(hop.Id, "IncomingRelationshipId", "Connections", "Which incoming user call provides the user identity for this downstream call?",
                incoming.Select(h => new QuestionOption(h.Id, h.Id, "Use the user access token issued for this middle-tier API.", "connection"))
                    .Append(new(AccessRelationship.ExternalUserToken, "User call from outside this diagram", "Validate a user access token issued for this API before accessing downstream resources.", "connection")),
                [Id(hop.Id, "Identity"), Id(hop.CallerComponentId, "IncomingIdentity"), .. incoming.Select(h => Id(h.Id, "Identity"))],
                [new(new(hop.Id, "Identity"), ConditionOperator.Equals, nameof(ActingIdentity.DelegatedUser)), new(new(hop.CallerComponentId, "Kind"), ConditionOperator.Equals, nameof(ComponentKind.Api))]));
        }
        return questions.ToImmutableArray();
    }

    public static bool IsRelevant(QuestionDefinition question, ArchitectureScenario scenario) => question.Relevance.All(condition =>
    {
        var fact = ScenarioFacts.Read(scenario, condition.Fact);
        return condition.Operator switch
        {
            ConditionOperator.IsKnown => fact.State == FactState.Known,
            ConditionOperator.IsUnknown => fact.State == FactState.Unknown,
            _ => fact.State == FactState.Known && fact.Value == condition.ExpectedValue
        };
    });
}



