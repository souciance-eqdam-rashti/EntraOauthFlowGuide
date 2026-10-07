using EntraAdvisor.Engine.Contracts;

namespace EntraAdvisor.Tests;

public sealed class ContractSafetyTests
{
    [Fact]
    public void UnansweredBooleanIsNotASuppliedFalse()
    {
        var unanswered = Fact<bool>.Unknown();
        var answered = Fact<bool>.Supplied(false);

        Assert.False(unanswered.TryGetValue(out _));
        Assert.True(answered.TryGetValue(out var value));
        Assert.False(value);
        Assert.NotEqual(unanswered, answered);
        Assert.Equal(FactState.Unknown, new ApplicationComponent { Id = "web", Name = "Web App" }.UserSignIn.State);
    }

    [Fact]
    public void DerivedFactsRequireTraceableRuleProvenance()
    {
        Assert.Throws<ArgumentException>(() => Fact<bool>.Derived(true, " "));
        var derived = Fact<bool>.Derived(true, "classification.browser-public");
        Assert.Equal(FactOrigin.Derived, derived.Origin);
        Assert.Equal("classification.browser-public", derived.DerivedByRuleId);
    }

    [Fact]
    public void UnknownAndNotApplicableAreDistinct()
    {
        var unknown = Fact<CredentialCapability>.Unknown();
        var inapplicable = Fact<CredentialCapability>.NotApplicable();
        Assert.NotEqual(unknown, inapplicable);
        Assert.False(inapplicable.TryGetValue(out _));
    }

    [Fact]
    public void BlockedResultsCannotCarryPlansAndMustExplainTheNextAction()
    {
        DecisionIssue issue = new("resource.unsupported", "Identity mode is incompatible.", "Choose a supported identity mode.");
        var result = EvaluationResult.Unsupported([issue]);

        Assert.Null(result.Plan);
        Assert.Equal(EvaluationStatus.Unsupported, result.Status);
        Assert.Throws<ArgumentException>(() => EvaluationResult.Unsupported([]));
        Assert.Throws<ArgumentException>(() => EvaluationResult.NeedsClarification([], [issue]));
        Assert.Throws<ArgumentException>(() => EvaluationResult.Invalid([issue], []));
    }

    [Fact]
    public void ClarificationAndInvalidResultsRetainApplicableQuestions()
    {
        DecisionIssue issue = new("identity.required", "Acting identity is unknown.", "Choose who the API acts as.");
        var clarification = EvaluationResult.NeedsClarification(["hop.identity"], [issue]);
        var invalid = EvaluationResult.Invalid([issue], ["hop.identity"]);

        Assert.Null(clarification.Plan);
        Assert.Null(invalid.Plan);
        Assert.Equal("hop.identity", Assert.Single(clarification.NextQuestionIds));
        Assert.Equal("hop.identity", Assert.Single(invalid.NextQuestionIds));
    }

    [Fact]
    public void DomainLibrariesDoNotDependOnUiOrOrchestration()
    {
        var assemblies = new[] { typeof(ArchitectureScenario).Assembly, typeof(Guide.Contracts.ImplementationGuide).Assembly };
        foreach (var assembly in assemblies)
        {
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference =>
                reference.Name!.StartsWith("Aspire", StringComparison.Ordinal) ||
                reference.Name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) ||
                reference.Name.StartsWith("Microsoft.FluentUI", StringComparison.Ordinal) ||
                reference.Name == "EntraAdvisor.Web");
        }
    }
}
