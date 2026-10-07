using System.Collections.Immutable;
using EntraAdvisor.Engine.Contracts;
using EntraAdvisor.Guide.Contracts;

namespace EntraAdvisor.Guide;

public sealed record CompletionInvalidation(AnswerChangeImpact Impact, ImmutableArray<StepCompletion> Completion);

public static class GuideCompletionInvalidator
{
    public static CompletionInvalidation Apply(ImplementationGuide guide, ImmutableArray<StepCompletion> completion,
        AnswerChangeImpact impact, string changedEntityId)
    {
        if (completion.IsDefault) throw new ArgumentException("Completion must be initialized.", nameof(completion));
        if (impact.ChangedFact is null && impact.InvalidatedQuestionIds.IsEmpty && impact.InvalidatedRelationshipIds.IsEmpty && impact.InvalidatedGuideStepIds.IsEmpty)
            return new(impact, completion);
        var affectedHops = impact.InvalidatedRelationshipIds.ToHashSet(StringComparer.Ordinal);
        var invalidSteps = impact.InvalidatedGuideStepIds.ToHashSet(StringComparer.Ordinal);
        var changedAll = changedEntityId is "architecture" or "tenants";
        foreach (var step in guide.Steps)
            if (changedAll || step.ComponentId == changedEntityId || step.RelatedRelationshipIds.Any(affectedHops.Contains)) invalidSteps.Add(step.Id);
        var grew = true;
        while (grew)
        {
            grew = false;
            foreach (var step in guide.Steps)
                if (step.DependsOnStepIds.Any(invalidSteps.Contains)) grew |= invalidSteps.Add(step.Id);
        }
        var knownSteps = guide.Steps.Select(step => step.Id).ToHashSet(StringComparer.Ordinal);
        if (completion.Any(item => !knownSteps.Contains(item.StepId)) || completion.Select(item => item.StepId).Distinct().Count() != completion.Length)
            throw new ArgumentException("Completion entries must uniquely reference this guide's steps.", nameof(completion));
        return new(impact with { InvalidatedGuideStepIds = invalidSteps.Order(StringComparer.Ordinal).ToImmutableArray() },
            completion.Select(item => invalidSteps.Contains(item.StepId) ? new StepCompletion(item.StepId, CompletionState.Pending) : item).ToImmutableArray());
    }
}
