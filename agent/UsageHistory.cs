using System.Globalization;
using System.Text.Json;
namespace GameControl;

// Durable cumulative samples. Re-sending the entire bounded window is safe after any network failure.
public sealed class UsageHistory {
 public long DeviceId {get;set;}
 public string Collector {get;set;}=Guid.NewGuid().ToString();
 public List<UsageCounter> Counters {get;set;}=[];
 private double? previous;
 private string previousDay="";
 private HashSet<string> previousGames=[];
 public void Tick(DateTimeOffset now,string timezone,double uptime,IEnumerable<string> games) {
  string day=TimeZoneInfo.ConvertTime(now,TimeZoneInfo.FindSystemTimeZoneById(timezone)).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
  var active=games.ToHashSet(StringComparer.Ordinal);
  double elapsed=previous.HasValue?uptime-previous.Value:0;
  // Never charge sleep, restart, unobserved gaps, or cross-midnight intervals to a guessed date.
  if(previousDay==day&&elapsed>0&&elapsed<=6) {
   Add(day,"__observed__",elapsed);
   if(active.Count>0&&previousGames.Count>0)Add(day,"__total__",elapsed);
   foreach(var app in active.Intersect(previousGames))Add(day,app,elapsed);
  }
  previous=uptime;previousDay=day;previousGames=active;
  string cutoff=DateOnly.ParseExact(day,"yyyy-MM-dd").AddDays(-34).ToString("yyyy-MM-dd");
  Counters=Counters.Where(c=>string.CompareOrdinal(c.Day,cutoff)>=0).OrderByDescending(c=>c.Day).ThenBy(c=>c.App).Take(1000).ToList();
 }
 private void Add(string day,string app,double seconds) {
  var counter=Counters.FirstOrDefault(c=>c.Day==day&&c.App==app);
  if(counter is null){counter=new UsageCounter{Day=day,App=app};Counters.Add(counter);}
  counter.Seconds=Math.Min(90000,counter.Seconds+seconds);
 }
 public object Report()=>new {collector=Collector,counters=Counters.Select(c=>new {day=c.Day,app=c.App,seconds=(int)Math.Floor(c.Seconds)}).ToArray()};
 public static UsageHistory Load(string path) {
  if(!File.Exists(path))return new();
  var value=JsonSerializer.Deserialize<UsageHistory>(File.ReadAllText(path))??throw new InvalidDataException();
  if(!Guid.TryParse(value.Collector,out _)||value.Counters.Count>1000||value.Counters.Any(c=>!DateOnly.TryParseExact(c.Day,"yyyy-MM-dd",out _)||!double.IsFinite(c.Seconds)||c.Seconds<0||c.Seconds>90000||c.App.Length>100))throw new InvalidDataException();
  return value;
 }
 public void Save(string path){File.WriteAllText(path+".tmp",JsonSerializer.Serialize(this));File.Move(path+".tmp",path,true);}
}
public sealed class UsageCounter {public string Day {get;set;}=""; public string App {get;set;}=""; public double Seconds {get;set;}}
