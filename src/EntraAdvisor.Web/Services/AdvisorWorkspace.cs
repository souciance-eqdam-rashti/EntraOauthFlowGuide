using System.Collections.Immutable;
using EntraAdvisor.Engine.Contracts;
using EntraAdvisor.Engine.Questionnaire;
using EntraAdvisor.Guide;
using EntraAdvisor.Guide.Contracts;

namespace EntraAdvisor.Web.Services;

public enum JourneyStage { Design, Recommendation, Implement }

/// <summary>Circuit-scoped state; no credentials, tokens, database or browser persistence.</summary>
public sealed class AdvisorWorkspace
{
    public QuestionnaireSession Session { get; private set; } = QuestionnaireSession.Create(TopologyPresets.Create("api-chain"));
    public JourneyStage Stage { get; set; } = JourneyStage.Design;
    public bool Started { get; set; }
    public string PresetId { get; private set; } = "api-chain";
    public ImplementationGuide? Guide { get; private set; }
    public ImmutableArray<StepCompletion> Completion { get; private set; } = [];
    public int ActiveStep { get; private set; }
    public bool GuideIsCurrent => Guide is not null && Session.Evaluation.Plan?.Id == Guide.PlanId;

    public AnswerChangeImpact Apply(QuestionAnswer answer)
    {
        var change = Session.Apply(answer);
        if (Guide is not null && change.Impact.ChangedFact is not null)
            Completion = GuideCompletionInvalidator.Apply(Guide, Completion, change.Impact, change.Impact.ChangedFact.EntityId).Completion;
        Session = change.Session;
        return change.Impact;
    }

    public void ChoosePreset(string id)
    {
        Session = QuestionnaireSession.Create(TopologyPresets.Create(id));
        PresetId = id;
        Guide = null;
        Completion = [];
        ActiveStep = 0;
        Stage = JourneyStage.Design;
    }

    public void SetGuide(ImplementationGuide guide)
    {
        if (guide.PlanId != Session.Evaluation.Plan?.Id) throw new ArgumentException("The guide must match the current validated plan.", nameof(guide));
        var retained = Guide?.Versions.Templates == guide.Versions.Templates ? Completion.ToDictionary(item => item.StepId) : [];
        Guide = guide;
        Completion = guide.Steps.Select(step => retained.TryGetValue(step.Id, out var prior) ? prior : new StepCompletion(step.Id, CompletionState.Pending)).ToImmutableArray();
        Stage = JourneyStage.Implement;
        SelectStep(0);
    }

    public void SelectStep(int index)
    {
        if (!GuideIsCurrent || Guide is null || index < 0 || index >= Guide.Steps.Length) throw new ArgumentOutOfRangeException(nameof(index));
        ActiveStep = index;
        var current = Completion[index];
        if (current.State == CompletionState.Pending) Completion = Completion.SetItem(index, current with { State = CompletionState.InProgress });
    }

    public void SetCompletion(CompletionState state)
    {
        if (!GuideIsCurrent) throw new InvalidOperationException("Regenerate the guide for the current architecture first.");
        if (Completion[ActiveStep].State == CompletionState.Complete && state != CompletionState.Complete && Guide is not null)
        {
            var impact = new AnswerChangeImpact([], [], [Completion[ActiveStep].StepId], "A completed prerequisite was reopened.");
            Completion = GuideCompletionInvalidator.Apply(Guide, Completion, impact, "completion").Completion;
        }
        Completion = Completion.SetItem(ActiveStep, new(Completion[ActiveStep].StepId, state, state == CompletionState.Blocked ? "Waiting for a prerequisite or configuration change." : null));
    }

    public void Reset()
    {
        ChoosePreset("api-chain");
        Started = false;
    }
}

