using GameControl;
using System.Text.Json;
using Xunit;
public class DisplayTests {
 static Rule Rule(string start="18:00",string end="21:00",int[]? days=null)=>new("roblox",["RobloxPlayerBeta.exe"],"schedule",start,end,days??[1,2,3,4,5,6,7]);
 [Theory]
 [InlineData("2026-10-30T19:00:00Z","2026-10-30T20:00:00Z")]
 [InlineData("2026-10-30T19:59:59Z","2026-10-30T20:00:00Z")]
 [InlineData("2026-10-30T20:00:00Z","2026-10-31T17:00:00Z")]
 public void Countdown_matches_enforcement_boundary(string now,string expected)=>Assert.Equal(DateTimeOffset.Parse(expected),ScheduleStatus.NextChange(Rule(),DateTimeOffset.Parse(now),"Europe/Stockholm"));
 [Fact]public void Overnight_countdown_and_next_week(){
  Assert.Equal(DateTimeOffset.Parse("2026-10-31T01:00:00Z"),ScheduleStatus.NextChange(Rule("22:00","02:00",[5]),DateTimeOffset.Parse("2026-10-30T23:00:00Z"),"Europe/Stockholm"));
  Assert.Equal(DateTimeOffset.Parse("2026-11-02T17:00:00Z"),ScheduleStatus.NextChange(Rule(days:[1]),DateTimeOffset.Parse("2026-10-30T18:00:00Z"),"Europe/Stockholm"));
 }
 [Fact]public void Spring_DST_skips_nonexistent_closing_hour(){
  Assert.Equal(DateTimeOffset.Parse("2026-03-29T01:00:00Z"),ScheduleStatus.NextChange(Rule("01:00","02:30"),DateTimeOffset.Parse("2026-03-29T00:15:00Z"),"Europe/Stockholm"));
 }
 [Fact]public void Fall_DST_repeated_hour_uses_actual_next_change(){
  Assert.Equal(DateTimeOffset.Parse("2026-10-25T00:30:00Z"),ScheduleStatus.NextChange(Rule("01:00","02:30"),DateTimeOffset.Parse("2026-10-25T00:15:00Z"),"Europe/Stockholm"));
  Assert.Equal(DateTimeOffset.Parse("2026-10-25T01:00:00Z"),ScheduleStatus.NextChange(Rule("01:00","02:30"),DateTimeOffset.Parse("2026-10-25T00:45:00Z"),"Europe/Stockholm"));
 }
 [Theory][InlineData("blocked")][InlineData("allowed")]
 public void Static_modes_have_no_countdown(string mode)=>Assert.Null(ScheduleStatus.NextChange(Rule() with{Mode=mode},DateTimeOffset.UtcNow,"Europe/Stockholm"));
 [Fact]public void Subminute_grant_expiry_before_schedule_opens_is_not_skipped(){
  var until=DateTimeOffset.Parse("2026-10-30T16:59:30Z");
  Assert.Equal(until,ScheduleStatus.NextChange(Rule() with{AllowedUntil=until},until.AddMinutes(-1),"Europe/Stockholm"));
 }
 [Fact]public void Public_snapshot_contains_no_connection_or_process_information(){
  var now=DateTimeOffset.UtcNow;var s=DisplayStatus.Create(new Policy(1,123,now,"Europe/Stockholm",60,[Rule()]),now,now);
  string json=JsonSerializer.Serialize(s,PolicyEngine.Json);Assert.DoesNotContain("token",json);Assert.DoesNotContain("policy_url",json);Assert.DoesNotContain("processes",json);Assert.DoesNotContain("device_id",json);Assert.True(s.HasPolicy);
  Assert.False(DisplayStatus.Create(null,now,null).HasPolicy);
 }
}
