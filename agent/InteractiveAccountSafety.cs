using System.Runtime.InteropServices;
using System.Security.Principal;
namespace GameControl;

// The SYSTEM service checks interactive user tokens, never its own privileged token.
// Only an aggregate state leaves this machine; no usernames or SIDs are reported.
public static class InteractiveAccountSafety {
 public static string Combine(IEnumerable<string> states) {
  var items=states.ToArray();
  if(items.Contains("admin"))return "admin";
  if(items.Any(s=>s!="standard"&&s!="no_user"))return "unknown";
  return items.Contains("standard")?"standard":"no_user";
 }
 public static string Read() {
  if(!OperatingSystem.IsWindows())return "unknown";
  IntPtr sessions=IntPtr.Zero;
  try {
   if(!WTSEnumerateSessions(IntPtr.Zero,0,1,out sessions,out int count)||count<0||count>4096)return "unknown";
   var states=new List<string>();int size=Marshal.SizeOf<Session>();
   for(int i=0;i<count;i++) {
    var session=Marshal.PtrToStructure<Session>(IntPtr.Add(sessions,i*size));
    if(session.Id==0||session.State!=0)continue; // WTSActive: console or active remote desktop.
    if(!WTSQueryUserToken(session.Id,out var token)){states.Add("unknown");continue;}
    try {states.Add(ReadToken(token));}finally{CloseHandle(token);}
   }
   return Combine(states);
  }catch{return "unknown";}finally{if(sessions!=IntPtr.Zero)WTSFreeMemory(sessions);}
 }
 [System.Runtime.Versioning.SupportedOSPlatform("windows")]
 internal static string ReadToken(IntPtr token) {
  GetTokenInformation(token,2,IntPtr.Zero,0,out int length); // TokenGroups
  if(length<4||length>1048576)return "unknown";
  var buffer=Marshal.AllocHGlobal(length);
  try {
   if(!GetTokenInformation(token,2,buffer,length,out _))return "unknown";
   int count=Marshal.ReadInt32(buffer);int offset=Marshal.OffsetOf<FirstGroup>(nameof(FirstGroup.Group)).ToInt32();int size=Marshal.SizeOf<SidAndAttributes>();
   if(count<0||count>4096||(long)offset+(long)count*size>length)return "unknown";
   for(int i=0;i<count;i++) {
    var group=Marshal.PtrToStructure<SidAndAttributes>(IntPtr.Add(buffer,offset+i*size));
    // Includes SE_GROUP_USE_FOR_DENY_ONLY in a UAC-filtered admin token.
    if(new SecurityIdentifier(group.Sid).IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid))return "admin";
   }
   return "standard";
  }finally{Marshal.FreeHGlobal(buffer);}
 }
 [StructLayout(LayoutKind.Sequential)]struct Session{public uint Id;public IntPtr Station;public int State;}
 [StructLayout(LayoutKind.Sequential)]struct SidAndAttributes{public IntPtr Sid;public uint Attributes;}
 [StructLayout(LayoutKind.Sequential)]struct FirstGroup{public uint Count;public SidAndAttributes Group;}
 [DllImport("wtsapi32.dll",SetLastError=true)]static extern bool WTSEnumerateSessions(IntPtr server,int reserved,int version,out IntPtr sessions,out int count);
 [DllImport("wtsapi32.dll",SetLastError=true)]static extern bool WTSQueryUserToken(uint session,out IntPtr token);
 [DllImport("wtsapi32.dll")]static extern void WTSFreeMemory(IntPtr memory);
 [DllImport("advapi32.dll",SetLastError=true)]static extern bool GetTokenInformation(IntPtr token,int informationClass,IntPtr info,int length,out int required);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
}
