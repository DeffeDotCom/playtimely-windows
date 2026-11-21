using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
namespace GameControl;
public record TimeRequest(string App,string Option,string Reason,string? Message=null,string? TaskId=null,string? Day=null,long? Revision=null);
public static class RequestPipe {
 public const string Name="Playtimely.TimeRequests.v1";
 public static bool Valid(TimeRequest r)=>r.App=="chores" ? Guid.TryParse(r.TaskId,out _) && DateOnly.TryParseExact(r.Day,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out _) && r.Revision>0 && r.Option is ("done" or "undo") && r.Message is null && r.Reason=="" : (r.App=="daily_budget"||GameCatalog.Contains(r.App))&&(r.App!="daily_budget"||r.Option!="today")&&(r.Message is null||r.Message.Length<=300)&&new[]{"15","30","60","today"}.Contains(r.Option)&&new[]{"finish_round","friends","homework_done","other"}.Contains(r.Reason);
 public static async Task Serve(Func<TimeRequest,CancellationToken,Task<string>> submit,CancellationToken stop){
  var security=new PipeSecurity();
  security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid,null),PipeAccessRights.FullControl,AccessControlType.Deny));
  security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null),PipeAccessRights.FullControl,AccessControlType.Allow));
  security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null),PipeAccessRights.FullControl,AccessControlType.Allow));
  security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.InteractiveSid,null),PipeAccessRights.ReadWrite,AccessControlType.Allow));
  DateTimeOffset last=DateTimeOffset.MinValue;
  while(!stop.IsCancellationRequested){
   try {
    using var pipe=NamedPipeServerStreamAcl.Create(Name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.FirstPipeInstance,512,512,security);
    await pipe.WaitForConnectionAsync(stop);
    using var deadline=CancellationTokenSource.CreateLinkedTokenSource(stop);deadline.CancelAfter(TimeSpan.FromSeconds(15));
    // A fixed-size request, never a file path, command, token or executable payload.
    var buffer=new byte[4096];int count=0;
    while(count<buffer.Length){int n=await pipe.ReadAsync(buffer.AsMemory(count,1),deadline.Token);if(n==0)break;count+=n;if(buffer[count-1]==10)break;}
    var reply="invalid";
    if(count>0&&count<4096&&buffer[count-1]==10){
     var request=JsonSerializer.Deserialize<TimeRequest>(buffer.AsSpan(0,count),PolicyEngine.Json);
     if(request is not null&&Valid(request)){
      if(request.App!="chores" && DateTimeOffset.UtcNow-last<TimeSpan.FromSeconds(10))reply="wait";
      else{last=DateTimeOffset.UtcNow;reply=await submit(request,deadline.Token);}
     }
    }
    await pipe.WriteAsync(Encoding.UTF8.GetBytes(reply+"\n"),deadline.Token);
   }catch(OperationCanceledException)when(stop.IsCancellationRequested){break;}
   catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or JsonException or OperationCanceledException){try{await Task.Delay(500,stop);}catch(OperationCanceledException){break;}}
  }
 }
}
