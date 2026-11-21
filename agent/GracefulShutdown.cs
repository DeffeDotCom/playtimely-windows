namespace GameControl;
// Monotonic deadlines and process creation identity prevent clock changes or PID
// reuse from shortening another process's grace period. Only the service uses this.
public sealed class GracefulShutdown {
 public const long GraceMilliseconds=30_000;
 readonly Dictionary<(int Pid,long Start),long> pending=new();
 public bool ShouldForce(int pid,long start,long monotonicNow){
  var key=(pid,start);
  if(!pending.TryGetValue(key,out var began)){pending[key]=monotonicNow;return false;}
  return monotonicNow-began>=GraceMilliseconds;
 }
 public void RetainOnly(HashSet<(int Pid,long Start)> blocked){
  foreach(var key in pending.Keys.Where(k=>!blocked.Contains(k)).ToArray())pending.Remove(key);
 }
}
