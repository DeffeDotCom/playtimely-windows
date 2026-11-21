using GameControl;
using Xunit;
using System.Text.Json;
public class BudgetExhaustionTests {
 readonly DateTimeOffset now=DateTimeOffset.Parse("2026-11-07T12:00:00Z");
 [Fact] public void FinishAllowsOnlyExistingIdentityAndNeverRegrantsAfterExitOrRestart(){
  var s=new BudgetExhaustion();s.Update("2026-11-07",0,"finish",now,10,["1:100"]);
  Assert.False(s.Blocks("1:100"));Assert.True(s.ForceNow("1:101"));
  s=JsonSerializer.Deserialize<BudgetExhaustion>(JsonSerializer.Serialize(s))!;
  s.Update("2026-11-07",0,"finish",now.AddSeconds(3),13,["1:100","2:200"]);
  Assert.False(s.Blocks("1:100"));Assert.True(s.Blocks("2:200"));
  s.Update("2026-11-07",0,"finish",now.AddSeconds(5),15,[]);
  s.Update("2026-11-07",0,"finish",now.AddSeconds(6),16,["1:100"]);
  Assert.True(s.Blocks("1:100"));
 }
 [Fact] public void GraceHasOneDeadlineForAllLaunchesAndSurvivesRestartAndClockRollback(){
  var s=new BudgetExhaustion();s.Update("2026-11-07",0,"grace",now,10,["1:100"]);
  s.Update("2026-11-07",0,"grace",now.AddMinutes(9),550,["2:200"]);Assert.False(s.Blocks("2:200"));
  s=JsonSerializer.Deserialize<BudgetExhaustion>(JsonSerializer.Serialize(s))!;
  s.Update("2026-11-07",0,"grace",now.AddSeconds(570),1,["2:200"]);Assert.False(s.Blocks("2:200"));Assert.False(s.ForceNow("2:200"));
  s.Update("2026-11-07",0,"grace",now.AddMinutes(-5),31,["2:200"]);Assert.True(s.Blocks("2:200"));
 }
 [Fact] public void ReminderDoesNotBlockAndFreshBudgetOrDayResets(){
  var s=new BudgetExhaustion();s.Update("2026-11-07",0,"notify",now,1,[]);Assert.False(s.Blocks("new"));
  s.Update("2026-11-07",300,"grace",now,2,[]);Assert.Null(s.BeganAt);
  s.Update("2026-11-08",0,"finish",now.AddDays(1),3,["new"]);Assert.False(s.Blocks("new"));
 }
}
