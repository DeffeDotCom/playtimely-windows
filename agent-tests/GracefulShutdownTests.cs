using GameControl;
using Xunit;
public class GracefulShutdownTests {
 [Fact] public void Gives_full_thirty_seconds_without_refreshing_deadline(){
  var s=new GracefulShutdown();Assert.False(s.ShouldForce(10,1,100));
  Assert.False(s.ShouldForce(10,1,30099));Assert.True(s.ShouldForce(10,1,30100));
 }
 [Fact] public void Reused_pid_gets_its_own_deadline(){
  var s=new GracefulShutdown();s.ShouldForce(10,1,0);Assert.False(s.ShouldForce(10,2,31000));
 }
 [Fact] public void Allowed_or_exited_process_cancels_deadline(){
  var s=new GracefulShutdown();s.ShouldForce(10,1,0);s.RetainOnly([]);
  Assert.False(s.ShouldForce(10,1,40000));Assert.False(s.ShouldForce(10,1,69999));Assert.True(s.ShouldForce(10,1,70000));
 }
}
