using System.Runtime.InteropServices;
using System.Security.Principal;
using GameControl;
if(!OperatingSystem.IsWindows()||!Environment.MachineName.StartsWith("PT-BUILD-"))throw new Exception("Dedicated Windows QA only");
if(WindowsIdentity.GetCurrent().User?.Value!="S-1-5-18")throw new Exception("Run this probe as SYSTEM to inspect interactive tokens");
if(InteractiveAccountSafety.Read()!="admin")throw new Exception("Active ptqa admin was not detected from SYSTEM");
Console.WriteLine("PASS SYSTEM observes active interactive administrator (including UAC-filtered membership)");
var name="ptsafety"+Guid.NewGuid().ToString("N")[..8];var password=Guid.NewGuid().ToString("N")+"!aA9";
var user=new Native.UserInfo{Name=name,Password=password,Privilege=1,Flags=0x10201};
uint result=Native.NetUserAdd(null,1,ref user,out _);if(result!=0)throw new Exception("Cannot create isolated standard QA user: "+result);
try {
 if(!Native.LogonUser(name,".",password,2,0,out var token))throw new Exception("Standard QA logon failed: "+Marshal.GetLastWin32Error());
 try {if(InteractiveAccountSafety.ReadToken(token)!="standard")throw new Exception("Standard user misclassified");}finally{Native.CloseHandle(token);}
 Console.WriteLine("PASS actual standard-user token is not administrator");
}finally{if(Native.NetUserDel(null,name)!=0)throw new Exception("Isolated QA user cleanup failed");}
Console.WriteLine("PASS temporary QA account removed; no user names or credentials reported");
static class Native {
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]internal struct UserInfo{[MarshalAs(UnmanagedType.LPWStr)]public string Name;[MarshalAs(UnmanagedType.LPWStr)]public string Password;public uint PasswordAge;public uint Privilege;[MarshalAs(UnmanagedType.LPWStr)]public string? Home;[MarshalAs(UnmanagedType.LPWStr)]public string? Comment;public uint Flags;[MarshalAs(UnmanagedType.LPWStr)]public string? Script;}
 [DllImport("netapi32.dll",CharSet=CharSet.Unicode)]internal static extern uint NetUserAdd(string? server,uint level,ref UserInfo info,out uint error);
 [DllImport("netapi32.dll",CharSet=CharSet.Unicode)]internal static extern uint NetUserDel(string? server,string user);
 [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]internal static extern bool LogonUser(string user,string domain,string password,int type,int provider,out IntPtr token);
 [DllImport("kernel32.dll")]internal static extern bool CloseHandle(IntPtr token);
}
