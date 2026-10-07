using EntraAdvisor.Engine.Contracts;
using EntraAdvisor.Engine.Questionnaire;
using EntraAdvisor.Web.State;

namespace EntraAdvisor.Web.Features.DesignArchitecture;

/// <summary>Transient navigation and drafts, separate from the authoritative architecture session.</summary>
public sealed class DesignJourneyState(AdvisorWorkspace workspace)
{
    private readonly Stack<QuestionScreen> history = new();
    private readonly Dictionary<string, string> drafts = new();
    public QuestionScreen? Screen { get; private set; } = workspace.Session.NextScreen;

    public string? Selected(string id) => drafts.GetValueOrDefault(id) ??
        (workspace.Session.Answers.TryGetValue(id, out var answer) ? answer.SelectedOptionIds[0] : null);

    public void Select(string id, string value) => drafts[id] = value;

    public void ChoosePreset(string id)
    {
        if (workspace.Started) return;
        workspace.ChoosePreset(id);
        Reset();
        workspace.Started = true;
    }

    public string Continue()
    {
        if (Screen is not { } prior) return "";
        var message = "";
        foreach (var question in prior.Questions)
            if (Selected(question.Id) is { } value && QuestionCatalog.IsRelevant(question, workspace.Session.Scenario))
            {
                var impact = workspace.Apply(new(question.Id, [value]));
                message = workspace.Guide is null ? "" : impact.Explanation;
            }
        history.Push(prior);
        drafts.Clear();
        Enter();
        return message;
    }

    public void Back()
    {
        if (history.TryPop(out var prior))
        {
            Screen = prior;
            drafts.Clear();
        }
        else workspace.Started = false;
    }

    // Match the existing tab navigation: reschedule the screen without clearing history/drafts.
    public void Enter() => Screen = workspace.Session.NextScreen;

    public void Reset()
    {
        history.Clear();
        drafts.Clear();
        Enter();
    }
}
