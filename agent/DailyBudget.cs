using System.Globalization;
using System.Text.Json;
namespace GameControl;

// One computer-wide clock: simultaneous games count once. State lives in the protected service directory.
public sealed class DailyBudget {
 public string Day {get;set;}="";
 public BudgetExhaustion ManualStop {get;set;}=new();
 public BudgetExhaustion Exhaustion {get;set;}=new();
 public double UsedSeconds {get;set;}
 private double? previousUptime;
 private bool wasPlaying;
 public void Tick(DateTimeOffset now,string timezone,double uptime,bool playing) {
  var day=TimeZoneInfo.ConvertTime(now,TimeZoneInfo.FindSystemTimeZoneById(timezone)).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
  if(string.CompareOrdinal(day,Day)>0){Day=day;UsedSeconds=0;previousUptime=null;}
  var elapsed=previousUptime.HasValue?uptime-previousUptime.Value:0;
  // No sleep, stalled loop or reboot time. Backwards dates never refill a consumed allowance.
  if(wasPlaying&&playing&&elapsed>0&&elapsed<=6)UsedSeconds=Math.Min(86400,UsedSeconds+elapsed);
  previousUptime=uptime;wasPlaying=playing;
 }
 public int Remaining(int minutes)=>Math.Max(0,minutes*60-(int)Math.Ceiling(UsedSeconds));
 public static DailyBudget Load(string path) {
  if(!File.Exists(path))return new();
  var state=JsonSerializer.Deserialize<DailyBudget>(File.ReadAllText(path))??throw new InvalidDataException("Budget state missing");
  if(!DateOnly.TryParseExact(state.Day,"yyyy-MM-dd",out _)||!double.IsFinite(state.UsedSeconds)||state.UsedSeconds<0||state.UsedSeconds>86400)throw new InvalidDataException("Budget state invalid");
  return state;
 }
 public void Save(string path){File.WriteAllText(path+".tmp",JsonSerializer.Serialize(this));File.Move(path+".tmp",path,true);}
}
