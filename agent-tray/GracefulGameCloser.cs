using System.Diagnostics;
using System.Runtime.InteropServices;
using GameControl;

// Runs in the player's desktop session, not the isolated SYSTEM service session.
// Post WM_CLOSE without activating the window, injecting keys or killing anything.
internal sealed class GracefulGameCloser {
 readonly HashSet<(int,long)> requested=new();
 internal event Action<string>? Closing;
 internal void Apply(DisplayStatus status,DateTimeOffset now){
  if(now-status.UpdatedAt>TimeSpan.FromSeconds(20)||status.UpdatedAt>now.AddSeconds(5))return;
  var active=new HashSet<(int,long)>();var announced=new HashSet<string>();
  foreach(var target in (status.ClosingGames??[]).Take(256)){
   if(!GameCatalog.Contains(target.App)||!status.Rules.Any(r=>r.App==target.App&&status.Timezone is {} tz&&!PolicyEngine.IsAllowed(r.AsRule(),now,tz)))continue;
   var key=(target.ProcessId,target.StartedAtUtcTicks);active.Add(key);
   if(requested.Contains(key))continue;
   if(RequestClose(target.ProcessId,target.StartedAtUtcTicks)){
    requested.Add(key);if(announced.Add(target.App))Closing?.Invoke(target.App);
   }
  }
  requested.IntersectWith(active);
 }
 internal static bool RequestClose(int processId,long start){
  try{
   using var process=Process.GetProcessById(processId);
   using var self=Process.GetCurrentProcess();
   if(processId==self.Id||process.SessionId!=self.SessionId||process.StartTime.ToUniversalTime().Ticks!=start)return false;
   // Validate identity again immediately before each message. Handles/PIDs can be reused.
   bool found=false;
   EnumWindows((handle,_)=>{
    GetWindowThreadProcessId(handle,out uint owner);
    if(owner!=(uint)processId||!IsWindowVisible(handle))return true;
    process.Refresh();if(process.HasExited||process.StartTime.ToUniversalTime().Ticks!=start)return true;
    if(PostMessage(handle,0x0010,IntPtr.Zero,IntPtr.Zero))found=true;
    return true;
   },IntPtr.Zero);
   return found;
  }catch(Exception ex)when(ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException){return false;}
 }
 delegate bool EnumWindow(IntPtr window,IntPtr param);
 [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindow callback,IntPtr param);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
 [DllImport("user32.dll",CharSet=CharSet.Auto)] static extern bool PostMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
}
