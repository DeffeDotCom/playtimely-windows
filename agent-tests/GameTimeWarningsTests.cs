using GameControl;
using Xunit;
public class GameTimeWarningsTests {
 [Fact] public void OnlyWarnsOncePerClosingAndNeverForStaleOrBlockedRules(){
  var w=new GameTimeWarnings();var now=DateTimeOffset.Parse("2026-11-01T18:00:00Z");var end=now.AddMinutes(5);
  Assert.False(w.ShouldNotify("game",end,now.AddSeconds(-1),true,true));
  Assert.False(w.ShouldNotify("game",end,now,false,true));
  Assert.False(w.ShouldNotify("game",end,now,true,false));
  Assert.True(w.ShouldNotify("game",end,now,true,true));
  Assert.False(w.ShouldNotify("game",end,now.AddSeconds(1),true,true));
  Assert.True(w.ShouldNotify("game",end.AddMinutes(15),now.AddMinutes(15),true,true));
  Assert.False(w.ShouldNotify("game",end,now.AddMinutes(6),true,true));
 }
 [Fact] public void Finish_reminder_every_fifteen_minutes_names_only_active_non_exempt_games(){
  var w=new GameTimeWarnings();var began=DateTimeOffset.UtcNow;RunningGame[] games=[new("fortnite",true),new("minecraft",true,true),new("roblox",false)];
  Assert.Empty(w.FinishReminders(began,"finish",0,games,true,0));
  Assert.Empty(w.FinishReminders(began,"finish",0,games,true,899));
  Assert.Equal(new[]{"fortnite"},w.FinishReminders(began,"finish",0,games,true,900));
  Assert.Empty(w.FinishReminders(began,"finish",0,games,true,901));
  Assert.Equal(new[]{"fortnite"},w.FinishReminders(began,"finish",0,games,true,1800));
  Assert.Empty(w.FinishReminders(began,"finish",0,games,false,2700));
  Assert.Equal(new[]{"fortnite"},w.FinishReminders(began,"finish",0,games,true,3000));
  Assert.Empty(w.FinishReminders(began,"finish",0,games,true,3001));
  Assert.Empty(w.FinishReminders(began,"finish",0,[],true,3900));
  Assert.Empty(w.FinishReminders(began,"finish",0,games,true,4800));
  Assert.Empty(w.FinishReminders(began,"finish",60,games,true,5700));
  foreach(var action in new[]{"notify","grace","stop"})Assert.Empty(w.FinishReminders(began,action,0,games,true,6600));
 }
}
