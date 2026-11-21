using System.Diagnostics;
using System.Management;
namespace GameControl;
public static class UpdateActivity {
 // Fresh snapshot independent of the normal discovery cache. Shared runtimes
 // require game identity, otherwise IDEs/Java servers could defer updates forever.
 public static bool HasPossibleGame(){
  var processes=Process.GetProcesses();
  try{foreach(var process in processes){
   try{
    string name=process.ProcessName;
    if(!GameCatalog.MatchingProcesses([name]).Any()||process.SessionId==0)continue;
    if(name.Equals("busybox64",StringComparison.OrdinalIgnoreCase))continue;
    if(name.Equals("Windows10Universal",StringComparison.OrdinalIgnoreCase)){
     if(process.MainModule?.FileName.Contains("ROBLOX",StringComparison.OrdinalIgnoreCase)??true)return true;
     continue;
    }
    if(name.Equals("dotnet",StringComparison.OrdinalIgnoreCase)||name.Equals("java",StringComparison.OrdinalIgnoreCase)||name.Equals("javaw",StringComparison.OrdinalIgnoreCase)){
     using var item=new ManagementObject("Win32_Process.Handle='"+process.Id+"'");
     if(GameCatalog.RuntimeBlocksUpdate(name,item["CommandLine"] as string))return true;
     continue;
    }
    return true;
   }
   catch(InvalidOperationException){} // Exited during snapshot.
   catch(System.ComponentModel.Win32Exception){return true;}
   catch(ManagementException){return true;}
   catch(System.Runtime.InteropServices.COMException){return true;}
  }}finally{foreach(var process in processes)process.Dispose();}
  return false;
 }
}
