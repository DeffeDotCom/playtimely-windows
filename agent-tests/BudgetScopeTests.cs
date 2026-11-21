using GameControl;
using Xunit;
public class BudgetScopeTests {
 readonly DateTimeOffset now=DateTimeOffset.Parse("2026-11-03T12:00:00Z");
 Policy P(string scope)=>new(1,1,now,"UTC",60,[],new(90,120,scope));
 Rule R(string mode)=>new("fortnite",["FortniteClient-Win64-Shipping.exe"],mode,"00:00","23:59",[1,2,3,4,5,6,7]);
 [Fact] public void GlobalCountsUnconfiguredAndAllowedGamesButPreservesBlocks(){
  Assert.True(PolicyEngine.UsesBudget(P("all_known"),null,now));
  Assert.True(PolicyEngine.UsesBudget(P("all_known"),R("allowed"),now));
  Assert.False(PolicyEngine.UsesBudget(P("all_known"),R("blocked"),now));
 }
 [Fact] public void SelectedCountsAllConfiguredGamesIncludingAllowed(){
  Assert.False(PolicyEngine.UsesBudget(P("selected"),null,now));
  Assert.False(PolicyEngine.UsesBudget(P("selected"),R("allowed"),now));
  Assert.True(PolicyEngine.UsesBudget(P("selected") with {Rules=[R("allowed")]},R("allowed"),now));
  Assert.False(PolicyEngine.UsesBudget(P("selected"),R("blocked"),now));
  Assert.True(PolicyEngine.UsesBudget(P("selected") with {Rules=[R("schedule")]},R("schedule"),now));
 }
 [Fact] public void ExplicitExtraTimeRemainsAnExceptionToExhaustion(){
  Assert.False(PolicyEngine.HasExtraTime(R("allowed"),now));
  Assert.True(PolicyEngine.HasExtraTime(R("schedule") with {AllowedUntil=now.AddMinutes(10)},now));
  Assert.False(PolicyEngine.HasExtraTime(R("schedule") with {AllowedUntil=now},now));
 }
}
