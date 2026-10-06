using EntraAdvisor.Guide;
using EntraAdvisor.Guide.Contracts;
using EntraAdvisor.Web.Services;
namespace EntraAdvisor.Tests;
public sealed class WorkspaceTests
{
 private static AdvisorWorkspace Ready() {
  var w=new AdvisorWorkspace();
  foreach(var pair in new[]{("tenants.model","SingleTenant"),("tenants.includesguestusers","False"),("orders-inventory.tenantboundary","SameTenant"),("web-orders.tenantboundary","SameTenant"),("api-a.canprotectcredentials","True"),("api-a.credential","Certificate"),("web.canprotectcredentials","True"),("web.credential","Certificate")}) w.Apply(new(pair.Item1,[pair.Item2]));
  Assert.NotNull(w.Session.Evaluation.Plan);w.SetGuide(new DelegatedChainGuideGenerator().Generate(w.Session.Evaluation.Plan!,new([])));return w;
 }
 [Fact] public void Reopening_a_completed_prerequisite_clears_dependent_confirmation() {
  var w=Ready();for(var i=0;i<w.Completion.Length;i++) {w.SelectStep(i);w.SetCompletion(CompletionState.Complete);}
  w.SelectStep(0);w.SetCompletion(CompletionState.InProgress);
  Assert.Equal(CompletionState.InProgress,w.Completion[0].State);Assert.All(w.Completion.Skip(1),c=>Assert.Equal(CompletionState.Pending,c.State));
 }
 [Fact] public void Architecture_edit_blocks_stale_guide_actions_and_does_not_restore_old_completion() {
  var w=Ready();w.SetCompletion(CompletionState.Complete);w.Apply(new("web.canprotectcredentials",["False"]));
  Assert.False(w.GuideIsCurrent);Assert.Throws<InvalidOperationException>(()=>w.SetCompletion(CompletionState.Complete));
  w.Apply(new("web.canprotectcredentials",["True"]));w.Apply(new("web.credential",["Certificate"]));w.SetGuide(new DelegatedChainGuideGenerator().Generate(w.Session.Evaluation.Plan!,new([])));
  Assert.Equal(CompletionState.InProgress,w.Completion[0].State);Assert.DoesNotContain(w.Completion,c=>c.State==CompletionState.Complete);
 }
 [Fact] public void Highlighting_escapes_untrusted_markup_before_appending_fixed_spans() {
  var html=CodeHighlighter.Render("<script>alert('x')</script> public class Sample {}");
  Assert.DoesNotContain("<script>",html);Assert.Contains("&lt;script&gt;",html);Assert.Contains("syntax-keyword",html);
 }
}

