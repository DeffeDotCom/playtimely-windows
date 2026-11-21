using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using System.Security.Principal;

// Borrow only the desktop shell's user token. Never inherit Setup's elevated identity,
// including when a parent enters different administrator credentials at the UAC prompt.
internal static class InteractiveLaunch
{
    internal static bool Start(string executable)
    {
        try
        {
            var window = GetShellWindow();
            if (window == IntPtr.Zero) return false; // unattended / no desktop
            GetWindowThreadProcessId(window, out var pid);
            return StartForShell(executable, (int)pid);
        }
        catch (Win32Exception) { return false; }
    }
    internal static bool StartForShell(string executable, int shellPid, bool systemUpdate=false)
    {
        try
        {
            using var shell = Process.GetProcessById(shellPid);
            if(systemUpdate){using var me=WindowsIdentity.GetCurrent();if(!me.IsSystem||shell.SessionId==0||!string.Equals(shell.MainModule?.FileName,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"),StringComparison.OrdinalIgnoreCase))return false;}
            else if (shell.SessionId != Process.GetCurrentProcess().SessionId) return false;
            using var process = OpenProcess(0x1000, false, (uint)shellPid);
            if (process.IsInvalid || !OpenProcessToken(process, 0x0002 | 0x0008, out var token)) return false;
            using (token)
            {
                if (!DuplicateTokenEx(token, 0x02000000, IntPtr.Zero, 2, 1, out var primary)) return false;
                using (primary)
                {
                    // An elevated desktop is not a safe source of a tray identity.
                    if (!GetTokenInformation(primary, 20, out var elevated, sizeof(int), out _) || elevated != 0) return false;
                    if (!CreateEnvironmentBlock(out var environment, primary, false)) return false;
                    try
                    {
                        var startup = new StartupInfo { cb = Marshal.SizeOf<StartupInfo>(), desktop = @"winsta0\default" };
                        var command=new StringBuilder('"' + executable + (systemUpdate?"\"":"\" --show"));
                        ProcessInfo child;
                        var launched=systemUpdate?CreateProcessAsUserW(primary,executable,command,IntPtr.Zero,IntPtr.Zero,false,0x400,environment,Path.GetDirectoryName(executable)!,ref startup,out child):CreateProcessWithTokenW(primary,0,executable,command,0x400,environment,Path.GetDirectoryName(executable)!,ref startup,out child);
                        if(!launched)return false;
                        CloseHandle(child.thread);
                        CloseHandle(child.process);
                        return true;
                    }
                    finally { DestroyEnvironmentBlock(environment); }
                }
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ArgumentException)
        {
            return false;
        }
    }
    internal static void StartForLoggedOnUsers(string executable){
        foreach(var shell in Process.GetProcessesByName("explorer"))using(shell){StartForShell(executable,shell.Id,true);}
    }
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)] private static extern bool CreateProcessAsUserW(SafeAccessTokenHandle token,string app,StringBuilder command,IntPtr processAttributes,IntPtr threadAttributes,bool inherit,int flags,IntPtr environment,string directory,ref StartupInfo startup,out ProcessInfo process);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int cb; public string? reserved; public string? desktop; public string? title;
        public int x, y, xSize, ySize, xCountChars, yCountChars, fillAttribute, flags;
        public short showWindow, reservedSize;
        public IntPtr reservedBytes, stdin, stdout, stderr;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInfo { public IntPtr process, thread; public uint processId, threadId; }
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(SafeProcessHandle process, uint access, out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool DuplicateTokenEx(SafeAccessTokenHandle token, uint access, IntPtr attributes, int impersonation, int type, out SafeAccessTokenHandle duplicate);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool GetTokenInformation(SafeAccessTokenHandle token, int type, out int value, int size, out int needed);
    [DllImport("userenv.dll", SetLastError = true)] private static extern bool CreateEnvironmentBlock(out IntPtr environment, SafeAccessTokenHandle token, bool inherit);
    [DllImport("userenv.dll")] private static extern bool DestroyEnvironmentBlock(IntPtr environment);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CreateProcessWithTokenW(SafeAccessTokenHandle token, int logonFlags, string app, StringBuilder command, int flags, IntPtr environment, string directory, ref StartupInfo startup, out ProcessInfo process);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
}
