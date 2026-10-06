using System.Collections.Immutable;
using EntraAdvisor.Engine.Contracts;

namespace EntraAdvisor.Engine.Questionnaire;

public sealed record SessionChange(QuestionnaireSession Session, AnswerChangeImpact Impact);

/// <summary>Immutable in-memory state. The latest evaluation replaces all stale recommendations.</summary>
public sealed class QuestionnaireSession
{
    public ArchitectureScenario Scenario { get; }
    public ImmutableDictionary<string, QuestionAnswer> Answers { get; }
    public EvaluationResult Evaluation { get; }
    public QuestionScreen? NextScreen => new QuestionScheduler().Next(Scenario, Evaluation);

    private QuestionnaireSession(ArchitectureScenario scenario, EvaluationResult evaluation)
    {
        Scenario = scenario;
        Evaluation = evaluation;
        Answers = QuestionCatalog.ForScenario(scenario).Where(q => q.Id != "architecture.structure")
            .Select(q => (Question: q, Fact: ScenarioFacts.Read(scenario, q.SuppliesFact)))
            .Where(pair => pair.Fact.State == FactState.Known && pair.Fact.Origin == FactOrigin.Supplied)
            .ToImmutableDictionary(pair => pair.Question.Id, pair => new QuestionAnswer(pair.Question.Id, [pair.Fact.Value!]), StringComparer.Ordinal);
    }

    public static QuestionnaireSession Create(ArchitectureScenario scenario)
    {
        if (!ScenarioValidation.Shape(scenario).IsEmpty) throw new ArgumentException("Use initialized typed facts and architecture collections.", nameof(scenario));
        // The questionnaire targets Enterprise Entra workforce identities.
        // Preserve explicit environments so unsupported inputs remain detectable.
        if (scenario.Tenants.Domain.State == FactState.Unknown)
            scenario = scenario with { Tenants = scenario.Tenants with { Domain = Fact<IdentityDomain>.Supplied(IdentityDomain.Workforce) } };
        var normalized = ScenarioNormalizer.Normalize(scenario);
        return new(normalized, new ArchitectureEvaluator().Evaluate(normalized));
    }

    public SessionChange Apply(QuestionAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var questions = QuestionCatalog.ForScenario(Scenario);
        var question = questions.SingleOrDefault(q => q.Id == answer.QuestionId) ?? throw new ArgumentException("Unknown question identifier.", nameof(answer));
        if (!QuestionCatalog.IsRelevant(question, Scenario)) throw new ArgumentException("This question is no longer relevant.", nameof(answer));
        if (answer.SelectedOptionIds.IsDefault || answer.SelectedOptionIds.Length != 1 || answer.TextValue is not null)
            throw new ArgumentException("Choose one listed card option.", nameof(answer));
        var option = question.Options.SingleOrDefault(o => o.Id == answer.SelectedOptionIds[0]) ?? throw new ArgumentException("Unknown card option.", nameof(answer));
        if (answer.QuestionId == "architecture.structure") return ReplaceTopology(TopologyPresets.Create(option.Id));
        var value = option.RepresentsUnknown ? null : option.Id;
        var priorFact = ScenarioFacts.Read(Scenario, question.SuppliesFact);
        if (priorFact.State == FactState.Known && priorFact.Value == value || priorFact.State == FactState.Unknown && value is null)
            return new(this, new([], [], [], "The answer did not change."));

        var invalidated = new HashSet<string>(StringComparer.Ordinal) { question.Id };
        var grew = true;
        while (grew)
        {
            grew = false;
            foreach (var candidate in questions)
                if (candidate.DependsOnQuestionIds.Any(invalidated.Contains)) grew |= invalidated.Add(candidate.Id);
        }
        var updated = ScenarioNormalizer.WithoutDerivedFacts(Scenario);
        foreach (var dependent in questions.Where(q => q.Id != question.Id && invalidated.Contains(q.Id)))
            updated = ScenarioFacts.Write(updated, dependent.SuppliesFact, null);
        updated = ScenarioFacts.Write(updated, question.SuppliesFact, value);
        // Category edits may remove a custom API branch. Skip removed entities when checking relevance.
        foreach (var definition in QuestionCatalog.ForScenario(updated).Where(q => q.Id != "architecture.structure"))
            if (!QuestionCatalog.IsRelevant(definition, updated))
            {
                if (ScenarioFacts.Read(updated, definition.SuppliesFact).State == FactState.Known) invalidated.Add(definition.Id);
                updated = ScenarioFacts.Write(updated, definition.SuppliesFact, null);
            }
        var affectedEntities = questions.Where(q => invalidated.Contains(q.Id)).Select(q => q.SuppliesFact.EntityId).ToHashSet(StringComparer.Ordinal);
        var affectedHops = Scenario.Relationships.Where(h => affectedEntities.Contains(h.Id) || affectedEntities.Contains(h.CallerComponentId) || affectedEntities.Contains(h.TargetResourceId) || affectedEntities.Contains("tenants"))
            .Select(h => h.Id).ToHashSet(StringComparer.Ordinal);
        // A changed API token contract affects dependent downstream calls, including later middle tiers.
        grew = true;
        while (grew)
        {
            grew = false;
            var affectedApis = Scenario.Relationships.Where(h => affectedHops.Contains(h.Id)).Select(h => Scenario.Resources.First(r => r.Id == h.TargetResourceId).ApiComponentId).Where(id => id is not null).ToHashSet();
            foreach (var hop in Scenario.Relationships)
                if (affectedApis.Contains(hop.CallerComponentId) || hop.IncomingRelationshipId.State == FactState.Known && affectedHops.Contains(hop.IncomingRelationshipId.Value!))
                    grew |= affectedHops.Add(hop.Id);
        }
        return new(Create(updated), new(invalidated.Where(id => id != question.Id).Order(StringComparer.Ordinal).ToImmutableArray(), affectedHops.Order(StringComparer.Ordinal).ToImmutableArray(), [],
            "Updated the architecture, removed dependent answers and replaced the recommendation. Affected implementation steps must be reopened.") { ChangedFact = question.SuppliesFact });
    }

    public SessionChange ReplaceTopology(ArchitectureScenario scenario)
    {
        return new(Create(scenario), new(Answers.Keys.Order(StringComparer.Ordinal).ToImmutableArray(), Scenario.Relationships.Select(h => h.Id).Order(StringComparer.Ordinal).ToImmutableArray(), [],
            "Changed the topology preset. Review its editable facts and regenerate the implementation guide.") { ChangedFact = new("architecture", "Structure") });
    }
}

