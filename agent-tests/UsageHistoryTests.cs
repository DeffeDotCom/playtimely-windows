using GameControl;
using Xunit;
public class UsageHistoryTests {
 [Fact] public void MeasuresUnionAndIndividualGamesWithoutSleepOrMidnightGuesses(){
  var h=new UsageHistory();var t=DateTimeOffset.Parse("2026-11-05T12:00:00Z");
  h.Tick(t,"UTC",0,["a","b"]);h.Tick(t.AddSeconds(2),"UTC",2,["a","b"]);
  h.Tick(t.AddSeconds(4),"UTC",4,["b"]);h.Tick(t.AddMinutes(10),"UTC",600,["b"]);
  Assert.Equal(4,h.Counters.Single(x=>x.App=="__total__").Seconds);
  Assert.Equal(2,h.Counters.Single(x=>x.App=="a").Seconds);Assert.Equal(4,h.Counters.Single(x=>x.App=="b").Seconds);
  h.Tick(t.AddDays(1),"UTC",602,["b"]);Assert.DoesNotContain(h.Counters,c=>c.Day=="2026-11-06");
 }
 [Fact] public void PersistsCollectorAndCountersButDoesNotChargeRestartGap(){
  var path=Path.GetTempFileName();File.Delete(path);
  try {var h=new UsageHistory();var t=DateTimeOffset.UtcNow;h.Tick(t,"UTC",0,["a"]);h.Tick(t,"UTC",2,["a"]);h.Save(path);
   var restored=UsageHistory.Load(path);Assert.Equal(h.Collector,restored.Collector);restored.Tick(t,"UTC",400,["a"]);Assert.Equal(2,restored.Counters.Single(c=>c.App=="a").Seconds);
  }finally{File.Delete(path);}
 }
 [Fact] public void LocalMidnightAndOfflineRetentionUseCalendarDays(){
  var h=new UsageHistory();var t=DateTimeOffset.Parse("2026-11-04T22:59:58Z");
  h.Tick(t,"Europe/Stockholm",0,["a"]);h.Tick(t.AddSeconds(1),"Europe/Stockholm",1,["a"]);
  h.Tick(t.AddSeconds(3),"Europe/Stockholm",3,["a"]); // midnight boundary not assigned to either day
  h.Tick(t.AddSeconds(4),"Europe/Stockholm",4,["a"]);
  Assert.Equal(1,h.Counters.Single(c=>c.Day=="2026-11-04"&&c.App=="a").Seconds);
  Assert.Equal(1,h.Counters.Single(c=>c.Day=="2026-11-05"&&c.App=="a").Seconds);
  h.Tick(t.AddDays(36),"Europe/Stockholm",5,["a"]);
  Assert.Empty(h.Counters);
 }

}
