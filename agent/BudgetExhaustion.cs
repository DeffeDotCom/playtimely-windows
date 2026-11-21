using System.Text.Json.Serialization;
namespace GameControl;
// Persisted in the service-owned budget file. A PID alone is never an identity.
public sealed class BudgetExhaustion {
 public string Day {get;set;}="";
 public string Action {get;set;}="stop";
 public DateTimeOffset? BeganAt {get;set;}
 public DateTimeOffset? LatestAt {get;set;}
 public HashSet<string> Continuing {get;set;}=[];
 [JsonIgnore] double? previousUptime;
 public void Update(string day,int? remaining,string action,DateTimeOffset now,double uptime,IEnumerable<string> running) {
  var active=running.ToHashSet();
  if(remaining!=0){Day=day;Action=action;BeganAt=null;LatestAt=null;Continuing.Clear();previousUptime=null;return;}
  if(Day!=day||Action!=action||BeganAt is null){Day=day;Action=action;BeganAt=now;LatestAt=now;Continuing=active;previousUptime=uptime;}
  // Clock rollback cannot extend an already running grace; UTC preserves it across restarts.
  var elapsed=previousUptime.HasValue?Math.Max(0,uptime-previousUptime.Value):0;
  var advanced=(LatestAt??now).AddSeconds(elapsed);
  if(action=="grace")LatestAt=now>advanced?now:advanced;previousUptime=uptime;
  Continuing.IntersectWith(active);
 }
 [JsonIgnore] public DateTimeOffset? Deadline=>Action=="grace"?BeganAt?.AddMinutes(10):null;
 [JsonIgnore] public bool GraceExpired=>Deadline is {} deadline && LatestAt>=deadline;
 public bool Blocks(string identity)=>BeganAt is not null && Action switch {
  "notify"=>false,"finish"=>!Continuing.Contains(identity),"grace"=>LatestAt>=Deadline,_=>true
 };
 public bool ForceNow(string identity)=>BeganAt is not null && (Action=="finish"&&!Continuing.Contains(identity));
}
