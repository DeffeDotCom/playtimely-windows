using System.Text.Json;
using GameControl;
// Per-user receipt cursor: no credentials. A restart or language change must not replay a grant.
public sealed class BudgetGrantNotifications {
 public record Receipt(string Scope,string Day,int Total,int LatestMinutes);
 readonly string? path;Receipt? receipt;
 public int LatestMinutes=>receipt?.LatestMinutes??0;
 public BudgetGrantNotifications(string? path=null){this.path=path;if(path is null)return;try{if(File.Exists(path)&&new FileInfo(path).Length<4096)receipt=JsonSerializer.Deserialize<Receipt>(File.ReadAllText(path));}catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException){}}
 public int Observe(DisplayStatus status,bool fresh){
  if(!fresh||!status.HasPolicy||string.IsNullOrWhiteSpace(status.BudgetReceiptScope)||status.BudgetExtraMinutes is not {} total||total<0||total>720||!DateOnly.TryParseExact(status.BudgetDay,"yyyy-MM-dd",out _))return 0;
  var sameDevice=receipt?.Scope==status.BudgetReceiptScope;
  var sameDay=sameDevice&&receipt!.Day==status.BudgetDay;
  // First observation establishes a baseline; never announce historical time on installation.
  var added=receipt is not null&&sameDevice?Math.Max(0,total-(sameDay?receipt!.Total:0)):0;
  var latest=added>0?added:sameDay?receipt!.LatestMinutes:0;
  var next=new Receipt(status.BudgetReceiptScope!,status.BudgetDay!,total,latest);
  if(next==receipt)return 0;receipt=next;
  if(path is not null)try{Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path+".tmp",JsonSerializer.Serialize(receipt));File.Move(path+".tmp",path,true);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){}
  return added;
 }
}
