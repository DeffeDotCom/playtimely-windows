using GameControl;
using Xunit;
public class GameNoticesTests {
 readonly DateTimeOffset now=DateTimeOffset.Parse("2026-11-08T18:00:00Z");
 [Fact] public void Immediate_stop_survives_process_exit_and_is_not_repeated(){
  var n=new GameNotices();n.Observe(new(){["1:42"]="fortnite"},now,false,18000,300);
  n.Stopping("1:42","fortnite","budget",true,now,18000,300);n.Stopping("1:42","fortnite","budget",true,now,18000,300);
  Assert.Single(n.Snapshot());Assert.Equal("blocked",n.Snapshot()[0].Outcome);
  n.Observe(new(),now.AddSeconds(2),false,18000,300);Assert.Equal("closed",n.Snapshot().Last().Outcome);Assert.Equal(18000,n.Snapshot().Last().UsedSeconds);
  n.Observe(new(),now.AddSeconds(3),false,18000,300);Assert.Equal(2,n.Snapshot().Length);
 }
 [Fact] public void Finish_mode_does_not_claim_service_killed_voluntarily_finished_game(){
  var n=new GameNotices();n.Observe(new(){["1:42"]="fortnite",["2:9"]="minecraft"},now,true,18000,300,["1:42"]);
  n.Observe(new(),now.AddSeconds(2),true,18002,300);var notice=Assert.Single(n.Snapshot());Assert.Equal("finished",notice.Outcome);Assert.Equal("fortnite",notice.App);
 }
 [Theory][InlineData("schedule")][InlineData("paused")][InlineData("budget")][InlineData("repair")]
 public void Graceful_close_retains_real_reason(string reason){var n=new GameNotices();n.Observe(new(){["1:42"]="fortnite"},now,false,null,300);n.Stopping("1:42","fortnite",reason,false,now,null,300);Assert.Equal("closing",n.Snapshot()[0].Outcome);n.Observe(new(),now.AddSeconds(2),false,null,300);Assert.Equal(reason,n.Snapshot().Last().Reason);Assert.Null(n.Snapshot().Last().UsedSeconds);}
 [Fact] public void Permissive_policy_cancels_pending_close_and_events_expire(){var n=new GameNotices();n.Observe(new(){["1:42"]="fortnite"},now,false,0,300);n.Stopping("1:42","fortnite","schedule",false,now,0,300);n.RetainStopping([]);n.Observe(new(),now.AddMinutes(6),false,0,300);Assert.Empty(n.Snapshot());}
}
