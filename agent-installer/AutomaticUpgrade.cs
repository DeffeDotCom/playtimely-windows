using GameControl;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text.Json;
sealed partial class Setup {
 internal static bool UpgradeExisting(bool automatic){
  using var identity=WindowsIdentity.GetCurrent();
  if(!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))throw new UnauthorizedAccessException();
  if(automatic&&!identity.IsSystem)throw new UnauthorizedAccessException("Automatic updates require the installed service.");
  var bin=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"GameControl");
  var data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"GameControl");
  SecureDirectory(bin,true);SecureDirectory(data);
  if(!File.Exists(ConnectionPath))throw new IOException("No existing installation");
  var stage=Path.Combine(data,"upgrade-"+Guid.NewGuid().ToString("N"));SecureDirectory(stage,true);
  var names=new[]{"GameControl.Agent.exe","Playtimely.Tray.exe"};var resources=new[]{"AgentBinary","TrayBinary"};
  var installed=names.Select(n=>Path.Combine(bin,n)).ToArray();var candidates=names.Select(n=>Path.Combine(stage,n)).ToArray();
  // Extract everything first: disk/download/extraction failures leave the old service untouched.
  for(int i=0;i<names.Length;i++){using var source=Assembly.GetExecutingAssembly().GetManifestResourceStream(resources[i])??throw new IOException("Missing binary");using var target=new FileStream(candidates[i],FileMode.CreateNew);source.CopyTo(target);target.Flush(true);}
  var status=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"PlaytimelyStatus","status.json");
  if(automatic){using var s=JsonDocument.Parse(File.ReadAllText(status));var root=s.RootElement;
   if(DateTimeOffset.UtcNow-root.GetProperty("updated_at").GetDateTimeOffset()>TimeSpan.FromSeconds(15)||!root.GetProperty("has_policy").GetBoolean()||root.GetProperty("running_games").GetArrayLength()!=0)throw new IOException("Game activity or stale status; update postponed");}
  if(automatic && UpdateActivity.HasPossibleGame())throw new IOException("Possible game activity; update postponed");
  bool requirePolicy=automatic;
  try{using var prior=JsonDocument.Parse(File.ReadAllText(status));requirePolicy|=prior.RootElement.GetProperty("has_policy").GetBoolean();}catch(IOException){}catch(JsonException){}
  void StartAndVerify(){
   ConfigureService(installed[0]);
   var after=DateTimeOffset.UtcNow;Run("sc.exe",["start","GameControlAgent"]);
   using var service=new ServiceController("GameControlAgent");service.WaitForStatus(ServiceControllerStatus.Running,TimeSpan.FromSeconds(30));
   var timer=Stopwatch.StartNew();TimeSpan? healthySince=null;while(timer.Elapsed<TimeSpan.FromSeconds(30)){
    try{using var state=JsonDocument.Parse(File.ReadAllText(status));if(state.RootElement.GetProperty("updated_at").GetDateTimeOffset()>DateTimeOffset.UtcNow.AddSeconds(-5)&&state.RootElement.GetProperty("updated_at").GetDateTimeOffset()>after&&(!requirePolicy||state.RootElement.GetProperty("has_policy").GetBoolean())){healthySince??=timer.Elapsed;if(timer.Elapsed-healthySince.Value>=TimeSpan.FromSeconds(5))return;}else healthySince=null;}catch(IOException){}catch(JsonException){}
    Thread.Sleep(500);
   }throw new IOException("Updated service did not produce a healthy heartbeat");
  }
  try{UpgradeTransaction.Run(installed,candidates,Path.Combine(stage,"backup"),()=>{StopService();StopTray(bin);},StartAndVerify);}
  finally{if(automatic)InteractiveLaunch.StartForLoggedOnUsers(installed[1]);}
  foreach(var old in Directory.EnumerateDirectories(data,"upgrade-*")){
   try{if(old!=stage&&(File.GetAttributes(old)&FileAttributes.ReparsePoint)==0)Directory.Delete(old,true);}catch(IOException){}catch(UnauthorizedAccessException){}
  }
  RegisterTray(installed[1],bin);
  // Keep a protected previous-version backup for manual repair; credentials/rules/budgets are never rewritten.
  return automatic||InteractiveLaunch.Start(installed[1]);
 }
}
