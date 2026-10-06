using System.Collections.Immutable;
using EntraAdvisor.Engine;
using EntraAdvisor.Engine.Contracts;
using EntraAdvisor.Engine.Questionnaire;
using EntraAdvisor.Guide;
using EntraAdvisor.Guide.Contracts;

namespace EntraAdvisor.Tests;

public sealed class QuestionnaireTests
{
    public static TheoryData<string> Presets => new(TopologyPresets.All.Select(preset => preset.Id));

    private static string DefaultAnswer(QuestionDefinition question) => question.SuppliesFact.FactName switch
    {
        "Domain" => nameof(IdentityDomain.Workforce), "Model" => nameof(WorkforceTenantModel.SingleTenant), "IncludesGuestUsers" => "False",
        "CanProtectCredentials" or "LocalBrowserAvailable" or "AlternateBrowserAvailable" or "DeviceCodePermitted" => "True",
        "Hosting" => nameof(HostingEnvironment.NonAzure), "ManagedIdentityAvailable" => "False", "Credential" => nameof(CredentialCapability.Certificate),
        "IncomingIdentity" => nameof(IncomingTokenIdentity.DelegatedUser), "TenantBoundary" => nameof(TenantBoundary.SameTenant),
        "Identity" => nameof(ActingIdentity.DelegatedUser), "IncomingRelationshipId" => question.Options.First(option => !option.RepresentsUnknown).Id,
        _ => question.Options.First(option => !option.RepresentsUnknown).Id
    };

    [Theory]
    [MemberData(nameof(Presets))]
    public void EveryEditablePresetCanReachReadyWithoutOversizedScreens(string preset)
    {
        var session = QuestionnaireSession.Create(TopologyPresets.Create(preset));
        Assert.Equal(IdentityDomain.Workforce, session.Scenario.Tenants.Domain.Value);
        Assert.DoesNotContain(QuestionCatalog.ForScenario(session.Scenario), q => q.Id == "tenants.domain");
        for (var count = 0; count < 60 && session.Evaluation.Status == EvaluationStatus.NeedsClarification; count++)
        {
            var screen = session.NextScreen!;
            Assert.Single(screen.Questions);
            var question = screen.Questions[0];
            session = session.Apply(new(question.Id, [DefaultAnswer(question)])).Session;
        }
        Assert.True(session.Evaluation.Status == EvaluationStatus.Ready, $"{preset}: {string.Join(',', session.Evaluation.NextQuestionIds)}");
    }

    [Fact]
    public void CredentialChoicesExcludeUnsupportedManagedIdentityAndOboFederation()
    {
        var chain = ScenarioExamples.Chain();
        var questions = QuestionCatalog.ForScenario(chain);
        Assert.DoesNotContain(questions.Single(q => q.Id == "web.credential").Options, o => o.Id == "ManagedIdentity");
        Assert.Single(questions.Single(q => q.Id == "api-a.credential").Options);
        var worker = ScenarioExamples.Worker(managed: true);
        Assert.Contains(QuestionCatalog.ForScenario(worker).Single(q => q.Id == "worker.credential").Options, o => o.Id == "ManagedIdentity");
        Assert.Equal(EvaluationStatus.Ready, QuestionnaireSession.Create(worker).Apply(new("worker.credential", ["ManagedIdentity"])).Session.Evaluation.Status);
        var crossTenant = worker with { Relationships = [worker.Relationships[0] with { TenantBoundary = Fact<TenantBoundary>.Supplied(TenantBoundary.CrossTenant) }] };
        Assert.DoesNotContain(QuestionCatalog.ForScenario(crossTenant).Single(q => q.Id == "worker.credential").Options, o => o.Id == "ManagedIdentity");
        Assert.DoesNotContain(QuestionCatalog.ForScenario(ScenarioExamples.Worker()).Single(q => q.Id == "worker.credential").Options, o => o.Id == "ManagedIdentity");
    }

    [Fact]
    public void UnansweredFactsRemainUnknownAndNotSureIsRejected()
    {
        var scenario = ScenarioExamples.Worker();
        scenario = scenario with { Relationships = [scenario.Relationships[0] with { Identity = Fact<ActingIdentity>.Unknown() }] };
        var session = QuestionnaireSession.Create(scenario);
        Assert.Throws<ArgumentException>(() => session.Apply(new("worker-target.identity", ["not-sure"])));
        Assert.DoesNotContain(QuestionCatalog.ForScenario(session.Scenario).SelectMany(q => q.Options), o => o.RepresentsUnknown);
        Assert.Equal(EvaluationStatus.NeedsClarification, session.Evaluation.Status);
        Assert.Equal(FactState.Unknown, session.Scenario.Relationships[0].Identity.State);
        Assert.Null(session.Evaluation.Plan);
    }

    [Fact]
    public void BrowserCapabilityChangeInvalidatesTheDeviceBranch()
    {
        var session = QuestionnaireSession.Create(ScenarioExamples.Device());
        Assert.Equal(EvaluationStatus.Ready, session.Evaluation.Status);
        var change = session.Apply(new("cli.localbrowseravailable", ["True"]));
        Assert.Contains("cli.alternatebrowseravailable", change.Impact.InvalidatedQuestionIds);
        Assert.Contains("cli.devicecodepermitted", change.Impact.InvalidatedQuestionIds);
        Assert.False(change.Session.Answers.ContainsKey("cli.devicecodepermitted"));
        Assert.Equal(FactState.Unknown, change.Session.Scenario.Components[0].DeviceCodePermitted.State);
        Assert.Equal(SignInApproach.InteractivePublicClient, change.Session.Evaluation.Plan!.Components[0].SignIn);
    }

    [Fact]
    public void EditingAnAnswerRemovesTheStalePlanAndPreservesUnrelatedFacts()
    {
        var session = QuestionnaireSession.Create(ScenarioExamples.Chain());
        var change = session.Apply(new("web.usersignin", ["False"]));
        Assert.Null(change.Session.Evaluation.Plan);
        Assert.False(change.Session.Answers.ContainsKey("web-orders.identity"));
        Assert.Equal(FactState.Known, change.Session.Scenario.Components.Single(c => c.Id == "api-b").Stack.State);
        Assert.Equal(FactState.Known, change.Session.Scenario.Tenants.Domain.State);
        Assert.Contains("orders-inventory", change.Impact.InvalidatedRelationshipIds);
    }

    [Fact]
    public void ANoOpAnswerPreservesTheSessionAndCompletion()
    {
        var session = QuestionnaireSession.Create(ScenarioExamples.Chain());
        var change = session.Apply(new("web.usersignin", ["True"]));
        Assert.Same(session, change.Session);
        var guide = Guide(session.Evaluation.Plan!);
        var completion = guide.Steps.Select(step => new StepCompletion(step.Id, CompletionState.Complete)).ToImmutableArray();
        Assert.Equal(completion, GuideCompletionInvalidator.Apply(guide, completion, change.Impact, "web").Completion);
    }

    [Fact]
    public void CompletionInvalidationIncludesDependentStepsButPreservesOtherBranches()
    {
        var session = QuestionnaireSession.Create(ScenarioExamples.Chain());
        var guide = Guide(session.Evaluation.Plan!);
        var completion = guide.Steps.Select(step => new StepCompletion(step.Id, CompletionState.Complete)).ToImmutableArray();
        var change = session.Apply(new("web.usersignin", ["False"]));
        var result = GuideCompletionInvalidator.Apply(guide, completion, change.Impact, "web");
        Assert.Equal(CompletionState.Pending, result.Completion.Single(item => item.StepId == "web-config").State);
        Assert.Equal(CompletionState.Pending, result.Completion.Single(item => item.StepId == "downstream-test").State);
        Assert.Equal(CompletionState.Complete, result.Completion.Single(item => item.StepId == "unrelated").State);
    }

    private static ImplementationGuide Guide(OAuthPlan plan) => new()
    {
        PlanId = plan.Id, Architecture = plan, Versions = new(plan.Versions.Schema, plan.Versions.Rules, "test"),
        Steps =
        [
            new() { Id = "web-config", ComponentId = "web", Section = GuideSection.Configuration, Title = "Configure web", Purpose = "test", Action = "test", ExpectedResult = "test", RelatedRelationshipIds = ["web-orders"] },
            new() { Id = "downstream-test", ComponentId = "api-b", Section = GuideSection.TestAndTroubleshoot, Title = "Test downstream", Purpose = "test", Action = "test", ExpectedResult = "test", DependsOnStepIds = ["web-config"] },
            new() { Id = "unrelated", ComponentId = "independent", Section = GuideSection.Configuration, Title = "Unrelated", Purpose = "test", Action = "test", ExpectedResult = "test" }
        ]
    };

    [Fact]
    public void ResourceCategoryEditsCreateOrRemoveCustomRegistrationResponsibilities()
    {
        var session = QuestionnaireSession.Create(ScenarioExamples.Worker());
        var change = session.Apply(new("target.category", [nameof(ResourceCategory.CustomResource)]));
        Assert.Contains(change.Session.Scenario.Components, component => component.Id == "target-api");
        Assert.Equal("target-api", change.Session.Scenario.Resources[0].ApiComponentId);
        change = change.Session.Apply(new("target.category", [nameof(ResourceCategory.MicrosoftGraph)]));
        Assert.DoesNotContain(change.Session.Scenario.Components, component => component.Id == "target-api");
        Assert.Null(change.Session.Scenario.Resources[0].ApiComponentId);
    }

    [Fact]
    public void ResourceSelectionHasExactlyThreeCategoriesAndNoOperationQuestions()
    {
        var questions = QuestionCatalog.ForScenario(ScenarioExamples.Worker());
        var resourceQuestion = questions.Single(q => q.Id == "target.category");
        Assert.Equal(3, resourceQuestion.Options.Count(option => !option.RepresentsUnknown));
        Assert.DoesNotContain(questions, q => q.SuppliesFact.FactName.Contains("Operation", StringComparison.Ordinal));
    }

    [Fact]
    public void RemovedDeviceQuestionsCannotBeAnsweredAndBadOptionsAreRejected()
    {
        var session = QuestionnaireSession.Create(ScenarioExamples.Device());
        session = session.Apply(new("cli.localbrowseravailable", ["True"])).Session;
        Assert.Throws<ArgumentException>(() => session.Apply(new("cli.devicecodepermitted", ["True"])));
        Assert.Throws<ArgumentException>(() => session.Apply(new("cli.localbrowseravailable", ["bad-option"])));
        Assert.Throws<ArgumentException>(() => session.Apply(new("cli.localbrowseravailable", ["True", "False"])));
    }
}


