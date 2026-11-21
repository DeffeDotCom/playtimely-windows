using System.Text.Json;
namespace PlaytimelyTray;
// Per-user presentation receipt only; never controls policy enforcement.
public sealed class SubscriptionNoticeReceipt(string? path=null) {
 private sealed record Receipt(string Scope,string State);
 private Receipt? receipt=Load(path);
 static Receipt? Load(string? path){try{return path is not null&&File.Exists(path)&&new FileInfo(path).Length<4096?JsonSerializer.Deserialize<Receipt>(File.ReadAllText(path)):null;}catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException){return null;}}
 public bool Observe(string? state,string? scope,bool fresh){
  if(!fresh||string.IsNullOrEmpty(scope)||state is not ("active" or "expired"))return false;
  var notify=state=="expired"&&(receipt?.Scope!=scope||receipt?.State!="expired");
  if(receipt?.Scope==scope&&receipt?.State==state)return false;
  receipt=new(scope,state);
  if(path is not null)try{Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path+".tmp",JsonSerializer.Serialize(receipt));File.Move(path+".tmp",path,true);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){}
  return notify;
 }
}
