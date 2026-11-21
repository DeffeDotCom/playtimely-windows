using GameControl;
using Xunit;
public class SharedRuleTests {
 [Fact] public void SharedRuleAppliesOnlyToCatalogAndIndividualExceptionWins(){
  var now=DateTimeOffset.Parse("2026-11-07T12:00:00Z");
  var all=new Rule("all_known",[],"blocked","13:00","21:00",[1,2,3,4,5]);
  var p=new Policy(1,1,now,"UTC",60,[all],new(90,120));
  var r=PolicyEngine.RuleFor(p,"minecraft");Assert.NotNull(r);Assert.Equal("minecraft",r.App);Assert.NotEmpty(r.Processes);Assert.False(PolicyEngine.IsAllowed(r,now,"UTC"));
  Assert.Null(PolicyEngine.RuleFor(p,"explorer"));
  var allow=r with {Mode="allowed"};p=p with {Rules=[all,allow]};
  Assert.True(PolicyEngine.IsAllowed(PolicyEngine.RuleFor(p,"minecraft")!,now,"UTC"));
  Assert.True(PolicyEngine.UsesBudget(p,allow,now));
 }
}
