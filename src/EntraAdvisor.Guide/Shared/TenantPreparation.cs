using EntraAdvisor.Engine.Contracts;

namespace EntraAdvisor.Guide;

public static class TenantPreparation
{
    public static string Roles(OAuthPlan plan)
    {
        var graphApplication = plan.Relationships.Any(h => h.Identity == ActingIdentity.Application &&
            plan.Scenario.Resources.Single(r => r.Id == plan.Scenario.Relationships.Single(s => s.Id == h.RelationshipId).TargetResourceId).Category.Value == ResourceCategory.MicrosoftGraph);
        var text = "You need Cloud Application Administrator or Application Administrator in each tenant where you configure registrations and grant consent.";
        if (graphApplication) text += " Granting Microsoft Graph application permissions requires Privileged Role Administrator in the resource tenant.";
        if (plan.Scenario.Resources.Any(r => r.Category.Value == ResourceCategory.AzureResource)) text += " Azure role assignments also require Role Based Access Control Administrator, User Access Administrator or Owner at the target resource scope.";
        return text;
    }
}
