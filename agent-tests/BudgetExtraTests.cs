using GameControl;
using Xunit;
public class BudgetExtraTests {
 [Fact] public void ExtraMinutesExpireOfflineAtFamilyMidnight(){
  var limit=new BudgetLimit(300,360,"all_known","finish",45,"2026-11-08");
  Assert.Equal(405,limit.Minutes(DateTimeOffset.Parse("2026-11-08T22:59:59Z"),"Europe/Stockholm"));
  Assert.Equal(300,limit.Minutes(DateTimeOffset.Parse("2026-11-08T23:00:00Z"),"Europe/Stockholm"));
 }
 [Fact] public void GrantDoesNotResetMeasuredUseOrChangeGameSchedule(){
  var budget=new DailyBudget{Day="2026-11-08",UsedSeconds=300*60};
  var basic=new BudgetLimit(300,300,"all_known","stop");
  var extra=basic with {ExtraMinutes=45,ExtraDay="2026-11-08"};
  var now=DateTimeOffset.Parse("2026-11-08T12:00:00Z");
  Assert.Equal(45*60,budget.Remaining(extra.Minutes(now,"UTC"))-budget.Remaining(basic.Minutes(now,"UTC")));
  Assert.Equal(300*60,budget.UsedSeconds);
  var rule=new Rule("roblox",["RobloxPlayerBeta.exe"],"blocked","00:00","00:00",[]);
  Assert.False(PolicyEngine.IsAllowed(rule,now,"UTC"));
 }
}
