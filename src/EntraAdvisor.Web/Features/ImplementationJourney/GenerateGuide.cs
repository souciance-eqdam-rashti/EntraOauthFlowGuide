using EntraAdvisor.Guide;
using EntraAdvisor.Web.State;

namespace EntraAdvisor.Web.Features.ImplementationJourney;

/// <summary>Generate from the current validated plan and let the workspace reconcile completion.</summary>
public sealed class GenerateGuide(AdvisorWorkspace workspace)
{
    public void Execute()
    {
        var plan = workspace.Session.Evaluation.Plan ??
            throw new InvalidOperationException("Answer the required architecture questions before generating a guide.");
        workspace.SetGuide(new ArchitectureGuideGenerator().Generate(plan, new([])));
    }
}
