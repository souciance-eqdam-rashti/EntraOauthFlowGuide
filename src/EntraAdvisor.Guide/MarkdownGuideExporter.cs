using System.Text;
using EntraAdvisor.Guide.Contracts;
namespace EntraAdvisor.Guide;
public sealed class MarkdownGuideExporter : IGuideExporter
{
    public string ExportMarkdown(ImplementationGuide guide)
    {
        var b=new StringBuilder("# Entra delegated API chain implementation\n\n");
        b.AppendLine($"Plan: {guide.PlanId}\n\nVersions: schema {guide.Versions.Schema}, rules {guide.Versions.Rules}, templates {guide.Versions.Templates}\n");
        b.AppendLine("## Architecture\n");
        foreach(var c in guide.Architecture.Scenario.Components) b.AppendLine($"- {c.Name}: {c.Stack.Value}");
        foreach(var h in guide.Architecture.Relationships) b.AppendLine($"- {h.RelationshipId}: {h.Identity}, {h.Acquisition}, audience `{h.AudienceValueKey}`, authorization `{h.Authorization.DeveloperValueKey}`");
        b.AppendLine("\n## Assumptions\n"); foreach(var a in guide.Assumptions) b.AppendLine("- "+a);
        foreach(var s in guide.Steps) {
            b.AppendLine($"\n## {s.Title}\n\nStep ID: {s.Id} · component: {s.ComponentId}\n\n{s.Purpose}\n\nAction: {s.Action}\n");
            foreach(var c in s.Content) switch(c) {
                case InstructionContent i: b.AppendLine(i.Text+"\n"); break;
                case PortalActionContent p: b.AppendLine(string.Join(" → ",p.Breadcrumbs)+"\n\n"+p.Action+"\n"); break;
                case CopyableValueContent v: b.AppendLine($"**{v.Value.Label}**: `{v.Value.Value}`\n\n{v.Value.Guidance}\n"); break;
                case CodeContent code: var fence=new string('`',Math.Max(3,LongestRun(code.Artifact.Content)+1)); b.AppendLine($"### {code.Artifact.DestinationFile}\n\n{fence}{code.Artifact.Language}\n{code.Artifact.Content}\n{fence}\n"); break;
                case ExplanationContent e: b.AppendLine($"**{e.Title}**\n\n{e.Text}\n"); break;
            }
            b.AppendLine("Expected result: "+s.ExpectedResult+"\n");
            foreach(var source in s.Sources) b.AppendLine($"- [{source.Title}]({source.Url})");
        }
        return b.ToString();
    }
    private static int LongestRun(string s) { var max=0;var run=0;foreach(var c in s) { run=c=='`'?run+1:0;max=Math.Max(max,run); } return max; }
}
