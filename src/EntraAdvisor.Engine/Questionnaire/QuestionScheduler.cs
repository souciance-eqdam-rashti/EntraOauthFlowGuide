using System.Collections.Immutable;
using EntraAdvisor.Engine.Contracts;

namespace EntraAdvisor.Engine.Questionnaire;

public sealed record QuestionScreen(string Section, ImmutableArray<QuestionDefinition> Questions);

public sealed class QuestionScheduler
{
    public QuestionScreen? Next(ArchitectureScenario scenario, EvaluationResult evaluation)
    {
        if (evaluation.Status is EvaluationStatus.Ready or EvaluationStatus.Unsupported) return null;
        var definitions = QuestionCatalog.ForScenario(scenario);
        var requested = evaluation.NextQuestionIds.ToHashSet(StringComparer.Ordinal);
        var applicable = definitions.Where(question => requested.Contains(question.Id) && QuestionCatalog.IsRelevant(question, scenario)).ToArray();
        if (applicable.Length == 0)
            return new("Design", [definitions.Single(question => question.Id == "architecture.structure")]);
        // Keep the journey focused on one architecture decision per page.
        var first = applicable[0];
        return new(first.Section, [first]);
    }
}

