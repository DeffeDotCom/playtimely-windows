using GameControl;
using Xunit;
public class ProcessScanTests {
 [Fact] public void ThirtySecondDiscoveryDoesNotFollowTwoSecondHeartbeat(){
  var schedule=new ProcessScanSchedule();Assert.True(schedule.IsDue(0));
  for(int t=2;t<30;t+=2)Assert.False(schedule.IsDue(t));
  Assert.True(schedule.IsDue(30));Assert.True(schedule.IsDue(32,true));Assert.False(schedule.IsDue(60));Assert.True(schedule.IsDue(62));Assert.True(schedule.IsDue(3600));
 }
 [Fact] public void ProcessIndexFindsGamesDeduplicatesAndKeepsJavaVerificationCandidate(){
  var games=GameCatalog.MatchingProcesses(["FortniteClient-Win64-Shipping","FORTNITECLIENT-WIN64-SHIPPING","javaw","java","not-a-game"]).ToArray();
  Assert.Single(games.Where(g=>g.Key=="fortnite"));Assert.Single(games.Where(g=>g.Key=="minecraft"));Assert.DoesNotContain(games,g=>g.Key=="not-a-game");
 }
 [Fact] public void RidersRepublicIsDetectedWithoutMatchingLauncherOrAnticheat(){
  var games=GameCatalog.MatchingProcesses(["RIDERSREPUBLIC","UbisoftConnect","upc","RidersRepublic_BE"]).ToArray();
  var game=Assert.Single(games);Assert.Equal("game-1440143395619344515",game.Key);
  Assert.True(GameCatalog.Allows(game.Key,"RidersRepublic.exe"));Assert.False(GameCatalog.Allows(game.Key,"upc.exe"));
 }
}
