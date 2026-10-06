using System.Collections.Immutable;

namespace EntraAdvisor.Engine.Contracts;

public enum QuestionSelection { SingleChoice, MultipleChoice, Text }
public enum ConditionOperator { Equals, IsUnknown, IsKnown }

public sealed record FactReference(string EntityId, string FactName);
public sealed record QuestionCondition(FactReference Fact, ConditionOperator Operator, string? ExpectedValue = null);
public sealed record QuestionOption(string Id, string Title, string Description, string IconKey, bool RepresentsUnknown = false);

public sealed record QuestionDefinition
{
    public required string Id { get; init; }
    public required string Section { get; init; }
    public required string Prompt { get; init; }
    public required FactReference SuppliesFact { get; init; }
    public QuestionSelection Selection { get; init; } = QuestionSelection.SingleChoice;
    public ImmutableArray<QuestionOption> Options { get; init; } = [];
    // All relevance conditions must match. No condition means always relevant.
    public ImmutableArray<QuestionCondition> Relevance { get; init; } = [];
    public ImmutableArray<string> DependsOnQuestionIds { get; init; } = [];
    public string? HelpText { get; init; }
}

public sealed record QuestionAnswer(string QuestionId, ImmutableArray<string> SelectedOptionIds, string? TextValue = null);

public sealed record AnswerChangeImpact(
    ImmutableArray<string> InvalidatedQuestionIds,
    ImmutableArray<string> InvalidatedRelationshipIds,
    ImmutableArray<string> InvalidatedGuideStepIds,
    string Explanation)
{
    public FactReference? ChangedFact { get; init; }
}
