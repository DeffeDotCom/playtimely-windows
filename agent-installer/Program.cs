using Playtimely.Desktop;
using System.Diagnostics;
using GameControl;
using System.ServiceProcess;
using System.Reflection;
using Microsoft.Win32;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Security.Cryptography;
internal static class Program {
    [STAThread] static int Main(string[] args) {
        using var gate=new Mutex(false,@"Global\Playtimely.Setup");bool acquired=false;
        try{
            try{acquired=gate.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}
            if(!acquired){if(args.Length==0||args.Contains("--update-ui"))MessageBox.Show("Playtimely Setup is already open. Finish or close that window before starting another update.","Playtimely");return 1618;}
            return Run(args);
        }finally{if(acquired)gate.ReleaseMutex();}
    }
    static int Run(string[] args) {
        if(args.Length==1&&args[0]=="--install-bundled") {try{Setup.InstallJson(InstallerBundle.Read(Environment.ProcessPath!)??throw new InvalidDataException("Missing connection."));return 0;}catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}}
        if(args.Length==1&&args[0]=="--upgrade") {try{Setup.UpgradeExisting(false);return 0;}catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}}
        if(args.Length==1&&args[0]=="--auto-upgrade") {try{Setup.UpgradeExisting(true);return 0;}catch(Exception ex){Console.Error.WriteLine(ex.GetType().Name+": "+ex.Message);return 1;}}
        if(args.Length==2&&args[0]=="--install") {try{Setup.Install(args[1]);return 0;}catch(Exception ex){Console.Error.WriteLine(ex.GetType().Name+": "+ex.Message);return 1;}}
        if(args.Length==1&&args[0]=="--uninstall") {try{Setup.Uninstall();return 0;}catch(Exception ex){Console.Error.WriteLine(ex.GetType().Name+": "+ex.Message);return 1;}}
        ApplicationConfiguration.Initialize();Application.Run(new Setup(args.Contains("--update-ui")));return 0;
    }
}
sealed partial class Setup:Form {
    private readonly Label status=new() {AutoSize=true,ForeColor=Color.FromArgb(70,90,78),Margin=new Padding(0,14,0,0)};
    readonly ContentWindow layout;
    internal static string ConnectionPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"GameControl","connection.json");
    public Setup(bool requestedUpdate=false) {
        string? bundled=null;try{bundled=InstallerBundle.Read(Environment.ProcessPath!);}catch(Exception ex){MessageBox.Show(ex.Message,"Download again");}
        bool upgrade=bundled is null && (requestedUpdate || File.Exists(ConnectionPath) || File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"GameControl","Playtimely.Tray.exe")));
        Text="Playtimely · Setup";ClientSize=new Size(600,720);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(250,249,245);ForeColor=Color.FromArgb(23,59,51);Font=new Font("Segoe UI",11);AutoScaleMode=AutoScaleMode.Dpi;MaximizeBox=false;
        layout=new ContentWindow(this,26);
        using(var iconStream=Assembly.GetExecutingAssembly().GetManifestResourceStream("SetupIcon")!)using(var icon=new Icon(iconStream))Icon=(Icon)icon.Clone();
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("SetupHero")!)using(var source=Image.FromStream(stream)) {
            var hero=new PictureBox {Image=new Bitmap(source),Tag="full-width",Height=210,SizeMode=PictureBoxSizeMode.Zoom,Margin=new Padding(0,0,0,12),AccessibleName="Playtimely: a parent manages game time on two computers from their phone"};
            hero.Disposed+=(_,_)=>hero.Image?.Dispose();layout.Content.Controls.Add(hero);
        }
        var title=new Label {Text="Time to play. Room for life.",AutoSize=true,Font=new Font("Segoe UI",22,FontStyle.Bold),Margin=new Padding(0,0,0,10)};
        var features=new Label {Text="01  Plan together — parents set each game’s schedule.\n02  See what’s next — game times stay visible on this PC.\n03  Need another round? Ask a parent for extra time.",AutoSize=true,Margin=new Padding(0,0,0,16)};
        var text=new Label {Text=bundled is not null?"Your personal installer connects this computer to your family automatically. Keep this download private.":upgrade?"Update this computer. Your family connection and saved game rules stay in place.":"Open Playtimely, add or select this computer, then download its personal installer. It will connect automatically — no extra files needed.",AutoSize=true,Margin=new Padding(0,0,0,16)};
        var install=new BrandButton {Text=bundled is not null?"Install Playtimely":upgrade?"Update Playtimely":"Open Playtimely",Height=48,Tag="full-width",BackColor=Color.FromArgb(23,59,51),ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Margin=new Padding(0,0,0,10),AccessibleName="Install or update Playtimely"};
        var dashboardUrl=new TextBox{Text="https://playtimely.com/dashboard",ReadOnly=true,Visible=false,Tag="full-width",AccessibleName="Playtimely website address"};
        install.Click+=async (_,_)=> {
            string? json=bundled;string? file=upgrade?ConnectionPath:null;
            if(json is null&&file is null){
                if(!TryOpenDashboard(()=>Process.Start(new ProcessStartInfo("https://playtimely.com/dashboard"){UseShellExecute=true}))){status.Text="Windows could not open your browser. Open your browser yourself and copy this address:";dashboardUrl.Visible=true;layout.Fit();}
                return;
            }
            if(json is null){try{json=File.ReadAllText(file!);}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){status.Text="Setup cannot read this computer’s saved connection. Close Setup and open the update again, accepting Windows administrator permission. Your existing connection and rules have not been changed. You do not need to add a new computer.";layout.Fit();return;}}
            Enabled=false;status.Text="Setting up Playtimely… Please keep this window open.";layout.Fit();try{var opened=await Task.Run(()=>json is not null?InstallJson(json):Install(file!));status.Text=opened?"Installed! Playtimely is opening with your game times. You can close this installer. The icon will also start when you sign in.":"Installed! The background service is running. We could not open the game-times window in this session; open Playtimely from the Start menu. The icon also starts next time you sign in.";install.Text="Installed";install.Enabled=false;}
            catch(Exception ex){install.Enabled=true;status.Text="Setup could not finish. Error details: " + ex.Message;}finally{Enabled=true;layout.Fit();}
        };
        var uninstall=new Button {Text="Uninstall Playtimely",AutoSize=true,FlatStyle=FlatStyle.Flat,Margin=new Padding(0,0,0,0),Visible=File.Exists(ConnectionPath)};

        uninstall.Click+=async (_,_)=>{if(MessageBox.Show("Stop the agent and remove its service? Game controls will stop applying.","Uninstall",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;Enabled=false;try{await Task.Run(Uninstall);status.Text="Service removed. Local files are retained for reinstallation.";}catch(Exception ex){MessageBox.Show(ex.Message);}finally{Enabled=true;}};
        layout.Content.Controls.Add(title);layout.Content.Controls.Add(features);layout.Content.Controls.Add(text);layout.Content.Controls.Add(install);layout.Content.Controls.Add(uninstall);layout.Content.Controls.Add(status);layout.Content.Controls.Add(dashboardUrl);

    }
    internal static bool TryOpenDashboard(Action open){
        try{open();return true;}
        catch(Exception ex)when(ex is System.ComponentModel.Win32Exception or InvalidOperationException or System.Runtime.InteropServices.COMException){return false;}
    }
    static void Run(string exe,string[] args,bool ignoreFailure=false) {
        var info=new ProcessStartInfo(exe) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var arg in args)info.ArgumentList.Add(arg);
        using var p=Process.Start(info)??throw new InvalidOperationException("Could not start the system utility.");var output=p.StandardOutput.ReadToEnd();var error=p.StandardError.ReadToEnd();p.WaitForExit();if(p.ExitCode!=0&&!(ignoreFailure&&new[]{1060,1062,1072,1073}.Contains(p.ExitCode)))throw new InvalidOperationException($"{exe} failed ({p.ExitCode}). {output} {error}");
    }
    static void StopService() {
        var running=Process.GetProcessesByName("GameControl.Agent");
        Run("sc.exe",["stop","GameControlAgent"],true);
        using var service=new ServiceController("GameControlAgent");
        try { service.WaitForStatus(ServiceControllerStatus.Stopped,TimeSpan.FromSeconds(30)); }
        catch(InvalidOperationException ex) when(ex.InnerException is System.ComponentModel.Win32Exception native&&native.NativeErrorCode==1060) { }
        foreach(var process in running)using(process){if(!process.WaitForExit(30000))throw new IOException("Playtimely is still stopping. Please try the update again.");}
    }
    static void StopTray(string bin) {
        foreach(var process in Process.GetProcessesByName("Playtimely.Tray"))using(process){
            try{if(string.Equals(process.MainModule?.FileName,Path.Combine(bin,"Playtimely.Tray.exe"),StringComparison.OrdinalIgnoreCase)){process.Kill();if(!process.WaitForExit(5000))throw new IOException("Close Playtimely status before updating.");}}
            catch(InvalidOperationException){}
        }
    }
    static void RemoveTray(){
        StopTray(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"GameControl"));
        using(var run=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",true))run?.DeleteValue("Playtimely",false);
        var link=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),"Playtimely.lnk");if(File.Exists(link))File.Delete(link);
    }
    internal static void Uninstall(){StopService();Run("sc.exe",["delete","GameControlAgent"],true);RemoveTray();}
    static void SecureDirectory(string path,bool readable=false) {
        Directory.CreateDirectory(path);
        if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Installation directories cannot be links.");
        var acl=new DirectorySecurity();acl.SetAccessRuleProtection(true,false);acl.SetOwner(new SecurityIdentifier("S-1-5-32-544"));
        foreach(var sid in new[]{"S-1-5-18","S-1-5-32-544"})acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid),FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
        if(readable)acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier("S-1-5-32-545"),FileSystemRights.ReadAndExecute,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
        new DirectoryInfo(path).SetAccessControl(acl);
    }
    // Remove an existing directory entry before writing, never truncate an attacker-created hard link.
    static void ReplacePrivateFile(string path,byte[] bytes) {if(File.Exists(path))File.Delete(path);File.WriteAllBytes(path,bytes);}
    // Reapply the product registration during both install and upgrade. Files may
    // remain after uninstall even when Windows no longer has the service entry.
    static void ConfigureService(string exe) {
        Run("sc.exe",["create","GameControlAgent","binPath=","\""+exe+"\"","start=","auto","DisplayName=","Playtimely Agent"],true);
        Run("sc.exe",["config","GameControlAgent","binPath=","\""+exe+"\"","start=","auto","obj=","LocalSystem"]);
        Run("sc.exe",["description","GameControlAgent","Downloads signed game rules. No remote control or content surveillance."]);
        Run("sc.exe",["failure","GameControlAgent","reset=","86400","actions=","restart/10000/restart/30000/restart/60000"]);
    }
    static void RegisterTray(string trayExe,string bin) {
        using(var run=Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"))run.SetValue("Playtimely", "\""+trayExe+"\"");
        // A Start-menu shortcut also lets an existing session open the status UI.
        var shortcut=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),"Playtimely.lnk");
        dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        try { dynamic link=shell.CreateShortcut(shortcut);try{link.TargetPath=trayExe;link.WorkingDirectory=bin;link.Description="Playtimely game times";link.Save();}finally{System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);} }
        finally{System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);}
    }
    internal static bool Install(string file) {
        return InstallJson(File.ReadAllText(file));
    }
    internal static bool InstallJson(string json) {
        using var identity=WindowsIdentity.GetCurrent();
        if(!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))throw new UnauthorizedAccessException("Administrator permission is required. Right-click the installer and choose Run as administrator. Ask a parent to enter administrator credentials.");
        using var doc=JsonDocument.Parse(json);var c=doc.RootElement;
        if(c.GetProperty("schema_version").GetInt32()!=1||c.GetProperty("device_id").GetInt64()<1||c.GetProperty("token").GetString()?.Length!=64||!Uri.TryCreate(c.GetProperty("policy_url").GetString(),UriKind.Absolute,out var uri)||uri.Scheme!="https"||!string.IsNullOrEmpty(uri.UserInfo)||!string.IsNullOrEmpty(uri.Query)||!string.IsNullOrEmpty(uri.Fragment))throw new InvalidDataException("Invalid connection file. A valid HTTPS endpoint is required.");
        using var rsa=RSA.Create();rsa.ImportFromPem(c.GetProperty("public_key").GetString());
        var bin=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"GameControl");var data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"GameControl");
        SecureDirectory(bin,true);SecureDirectory(data);
        if(File.Exists(Path.Combine(bin,"GameControl.Agent.exe"))&&File.Exists(Path.Combine(bin,"Playtimely.Tray.exe"))&&File.Exists(ConnectionPath)&&File.ReadAllText(ConnectionPath)==json)return UpgradeExisting(false);
        StopService();
        StopTray(bin);
        var exe=Path.Combine(bin,"GameControl.Agent.exe");
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("AgentBinary")??throw new InvalidOperationException("Agent binary missing from installer.")) {
            // Windows may take a moment to stop the service and release the executable.
            for(int n=0;;n++){try{if(File.Exists(exe))File.Delete(exe);using var target=File.Create(exe);stream.CopyTo(target);break;}catch(Exception ex)when((ex is IOException or UnauthorizedAccessException)&&n<60){Thread.Sleep(500);stream.Position=0;}}
        }
        var trayExe=Path.Combine(bin,"Playtimely.Tray.exe");
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TrayBinary")??throw new InvalidOperationException("Tray binary missing.")) {
            if(File.Exists(trayExe))File.Delete(trayExe);using var target=File.Create(trayExe);stream.CopyTo(target);
        }
        // Users may execute the UI, but not modify binaries. Credentials stay private.
        var statusDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"PlaytimelyStatus");
        SecureDirectory(statusDir,true);
        // Remove pre-existing projections after securing their parent; no links are followed.
        foreach(var name in new[]{"status.json","status.json.tmp"}){var path=Path.Combine(statusDir,name);if(File.Exists(path))File.Delete(path);}
        RegisterTray(trayExe,bin);
        var cache=Path.Combine(data,"policy.json");
        // Preserve offline rules across upgrades, but remove inherited/explicit file permissions and links.
        if(File.Exists(cache)) {
            if((File.GetAttributes(cache)&FileAttributes.ReparsePoint)!=0||new FileInfo(cache).Length>262144)File.Delete(cache);
            else ReplacePrivateFile(cache,File.ReadAllBytes(cache));
        }
        var temporary=cache+".tmp";if(File.Exists(temporary))File.Delete(temporary);
        ReplacePrivateFile(Path.Combine(data,"connection.json"),System.Text.Encoding.UTF8.GetBytes(json));
        ConfigureService(exe);
        Run("sc.exe",["start","GameControlAgent"]);
        return InteractiveLaunch.Start(trayExe);
    }
}
