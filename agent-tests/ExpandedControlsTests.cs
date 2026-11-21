using GameControl;
using Xunit;
public class ExpandedControlsTests {
 [Fact] public void Windows_union_and_temporary_pause_have_exact_boundaries() {
  var rule=new Rule("roblox",[],"schedule","18:00","20:00",[1,2,3,4,5],Windows:[new("10:00","12:00",[6,7])]);
  Assert.True(PolicyEngine.IsAllowed(rule,DateTimeOffset.Parse("2026-11-02T18:00:00Z"),"Europe/Stockholm"));
  Assert.True(PolicyEngine.IsAllowed(rule,DateTimeOffset.Parse("2026-11-07T10:00:00Z"),"Europe/Stockholm"));
  Assert.False(PolicyEngine.IsAllowed(rule,DateTimeOffset.Parse("2026-11-07T18:00:00Z"),"Europe/Stockholm"));
  var now=DateTimeOffset.Parse("2026-11-02T18:00:00Z");
  var end=now.AddSeconds(35);
  rule=rule with {OverrideMode="blocked",OverrideUntil=end,AllowedUntil=now.AddHours(1)};
  Assert.False(PolicyEngine.IsAllowed(rule,now,"Europe/Stockholm"));
  Assert.Equal(end,ScheduleStatus.NextChange(rule,now,"Europe/Stockholm"));
  Assert.True(PolicyEngine.IsAllowed(rule,end,"Europe/Stockholm"));
 }
}
