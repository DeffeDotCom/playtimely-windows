namespace GameControl;
public record PlayedGame(string App,int Seconds);
public record GameNotice(string Id,string App,string Reason,string Outcome,DateTimeOffset At,int? UsedSeconds,int? LimitMinutes);
// Service-authored, bounded informational events. No credentials or executable data.
public sealed class GameNotices {
 readonly Dictionary<string,GameNotice> pending=new();
 readonly List<GameNotice> recent=[];
 Dictionary<string,string> previous=new();
 HashSet<string> previousEligible=[];
 public void Observe(Dictionary<string,string> active,DateTimeOffset now,bool finishExhausted,int? used,int? limit,HashSet<string>? eligible=null){
  foreach(var (id,app) in previous.Where(p=>!active.ContainsKey(p.Key))){
   if(pending.Remove(id,out var notice))Add(notice with {Id=Guid.NewGuid().ToString("N"),Outcome="closed",At=now});
   else if(finishExhausted&&previousEligible.Contains(id))Add(new(Guid.NewGuid().ToString("N"),app,"budget","finished",now,used,limit));
  }
  previous=active;previousEligible=eligible??[];
  recent.RemoveAll(n=>now-n.At>TimeSpan.FromMinutes(5));
 }
 public void Stopping(string identity,string app,string reason,bool immediate,DateTimeOffset now,int? used,int? limit){
  if(pending.ContainsKey(identity))return;
  var notice=new GameNotice(Guid.NewGuid().ToString("N"),app,reason,immediate?"blocked":"closing",now,used,limit);
  pending[identity]=notice;Add(notice);
 }
 public void RetainStopping(IEnumerable<string> identities){var keep=identities.ToHashSet();foreach(var key in pending.Keys.Where(k=>!keep.Contains(k)).ToArray())pending.Remove(key);}
 void Add(GameNotice notice){recent.Add(notice);if(recent.Count>20)recent.RemoveAt(0);}
 public GameNotice[] Snapshot()=>recent.ToArray();
}
