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
            b.AppendLine($"\n## {s.Title}\n\nStep ID: {s.Id} · component: {s.ComponentId}\n\n{s.Action}\n");
            foreach(var group in InstructionGroups.Create(s,guide.Architecture)) {
            b.AppendLine($"### {group.Title}\n\n{group.System}: {string.Join(" → ",group.Location)}\n");
            foreach(var c in group.Actions) switch(c) {
                case InstructionContent i: if(!i.Location.IsEmpty) b.AppendLine(i.LocationLabel+": "+string.Join(" → ",i.Location)+"\n"); if(!string.IsNullOrEmpty(i.Title)) b.AppendLine("### "+i.Title+"\n"); b.AppendLine(i.Text+"\n"); break;
                case PortalActionContent p: b.AppendLine(string.Join(" → ",p.Breadcrumbs)+"\n\n"+p.Action+"\n"); break;
                case CopyableValueContent v when v.Value.ReferenceOnly: b.AppendLine($"**{v.Value.Label}**\n\n{v.Value.Guidance}\n"); break;
                case CopyableValueContent v: b.AppendLine($"**{v.Value.Label}**: `{v.Value.Value}`\n\n{v.Value.Guidance}\n"); break;
                case ConfigurationRowsContent rows:
                    b.AppendLine($"**{rows.Title}**\n\nOpen in Entra: {string.Join(" → ", rows.Breadcrumbs)}\n");
                    if(!string.IsNullOrEmpty(rows.Introduction)) b.AppendLine(rows.Introduction+"\n");
                    foreach(var value in rows.Values) b.AppendLine($"**{value.Label}** ({value.Kind}): {(value.IsTechnical ? "`"+value.Value+"`" : value.Value)}\n\n{value.Guidance}\n");
                    b.AppendLine(rows.Instruction+"\n"); break;
                case CodeContent code: if(code.Artifact.ExecutionLocation is not null) b.AppendLine("Execution location: "+code.Artifact.ExecutionLocation+"\n"); var fence=new string('`',Math.Max(3,LongestRun(code.Artifact.Content)+1)); b.AppendLine($"### {code.Artifact.DestinationFile}\n\n{fence}{code.Artifact.Language}\n{code.Artifact.Content}\n{fence}\n"); break;
                case ExplanationContent e: b.AppendLine($"**{e.Title}**\n\n{e.Text}\n"); break;
            }
            }
            b.AppendLine("Expected result: "+s.ExpectedResult+"\n");
            foreach(var source in s.Sources) b.AppendLine($"- [{source.Title}]({source.Url})");
        }
        return b.ToString();
    }
    private static int LongestRun(string s) { var max=0;var run=0;foreach(var c in s) { run=c=='`'?run+1:0;max=Math.Max(max,run); } return max; }
}
