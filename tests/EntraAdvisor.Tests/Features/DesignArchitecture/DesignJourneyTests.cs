using EntraAdvisor.Web.Features.DesignArchitecture;
using EntraAdvisor.Web.State;

namespace EntraAdvisor.Tests;

public sealed class DesignJourneyTests
{
    [Fact]
    public void Draft_is_not_committed_until_continue_and_back_recovers_the_answer()
    {
        var workspace = new AdvisorWorkspace();
        var design = new DesignJourneyState(workspace);
        design.ChoosePreset("client-api");
        var question = Assert.Single(design.Screen!.Questions);
        var option = question.Options[0].Id;
        var prior = workspace.Session;
        design.Select(question.Id, option);
        Assert.Same(prior, workspace.Session);
        design.Enter(); // Stage navigation must not discard the pending selection.
        Assert.Equal(option, design.Selected(question.Id));
        design.Continue();
        Assert.Equal(option, workspace.Session.Answers[question.Id].SelectedOptionIds[0]);
        design.Back();
        Assert.Equal(question.Id, Assert.Single(design.Screen!.Questions).Id);
        Assert.Equal(option, design.Selected(question.Id));
    }

    [Fact]
    public void Reset_discards_drafts_and_history_without_maintaining_a_second_session()
    {
        var workspace = new AdvisorWorkspace();
        var design = new DesignJourneyState(workspace);
        design.ChoosePreset("client-api");
        var question = Assert.Single(design.Screen!.Questions);
        design.Select(question.Id, question.Options[0].Id);
        design.Continue();
        workspace.Reset();
        design.Reset();
        Assert.Empty(workspace.Session.Answers);
        Assert.Null(design.Selected(question.Id));
        design.Back();
        Assert.False(workspace.Started);
        Assert.Equal(workspace.Session.NextScreen, design.Screen);
    }
}
