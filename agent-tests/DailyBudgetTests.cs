using GameControl;
using Xunit;
public class DailyBudgetTests {
 [Fact] public void Running_union_sleep_idle_and_day_rollover() {
  var b=new DailyBudget();var now=DateTimeOffset.Parse("2026-11-02T18:00:00Z");
  b.Tick(now,"UTC",100,true);b.Tick(now.AddSeconds(2),"UTC",102,true);Assert.Equal(898,b.Remaining(15));
  b.Tick(now.AddHours(1),"UTC",3702,true);Assert.Equal(898,b.Remaining(15));
  b.Tick(now.AddHours(1).AddSeconds(2),"UTC",3704,false);Assert.Equal(898,b.Remaining(15));
  b.Tick(now.AddDays(1),"UTC",100000,true);Assert.Equal(900,b.Remaining(15));
  b.Tick(now.AddDays(1).AddSeconds(2),"UTC",100002,true);
  b.Tick(now,"UTC",100004,true);Assert.Equal(896,b.Remaining(15));
 }
 [Fact] public void Restart_preserves_consumption_but_not_elapsed_offline_gap() {
  var path=Path.GetTempFileName();try {
   var b=new DailyBudget();var now=DateTimeOffset.Parse("2026-11-02T18:00:00Z");b.Tick(now,"UTC",100,true);b.Tick(now.AddSeconds(2),"UTC",102,true);b.Save(path);
   var reloaded=DailyBudget.Load(path);reloaded.Tick(now.AddHours(1),"UTC",4000,true);Assert.Equal(898,reloaded.Remaining(15));
   File.WriteAllText(path,"{}");Assert.Throws<InvalidDataException>(()=>DailyBudget.Load(path));
  }finally{File.Delete(path);}
 }
}
