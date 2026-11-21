using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Reflection;
namespace GameControl;
internal sealed class DesktopUpdater:IDisposable {
 readonly HttpClient http=new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromMinutes(5)};
 readonly ToolStripMenuItem item;readonly NotifyIcon tray;readonly System.Windows.Forms.Timer timer=new(){Interval=6*60*60*1000};
 DesktopRelease? release;bool busy;string? prompted;
 static string L(string s)=>TrayLanguage.Get(s);
 public DesktopUpdater(ContextMenuStrip menu,NotifyIcon tray){
  this.tray=tray;item=new ToolStripMenuItem(L("Check for updates")+" · "+DesktopRelease.CurrentVersion);item.Click+=async(_,_)=>{if(release?.IsNewer==true)await Update();else await Check(true);};menu.Items.Insert(1,item);
  timer.Tick+=async(_,_)=>await Check(false);timer.Start();_ = Check(false);
 }
 async Task Check(bool manual){
  if(busy)return;busy=true;
  try{
   using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(20));
   using var response=await http.GetAsync("https://playtimely.com/desktop/latest",HttpCompletionOption.ResponseHeadersRead,deadline.Token);response.EnsureSuccessStatusCode();
   await using var input=await response.Content.ReadAsStreamAsync(deadline.Token);using var bytes=new MemoryStream();var buffer=new byte[4096];int n;
   while((n=await input.ReadAsync(buffer,deadline.Token))>0){if(bytes.Length+n>16384)throw new InvalidDataException();bytes.Write(buffer,0,n);}
   using var keyStream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Playtimely.ReleaseKey")!;using var keyReader=new StreamReader(keyStream);
   release=DesktopRelease.Verify(System.Text.Encoding.UTF8.GetString(bytes.ToArray()),keyReader.ReadToEnd());
   item.Text=release.IsNewer?L("Update available")+" · "+release.Version:L("Check for updates")+" · "+DesktopRelease.CurrentVersion;
   if(release.IsNewer&&prompted!=release.Version){prompted=release.Version;tray.ShowBalloonTip(8000,"Playtimely",L("Update available. Ask a parent to install it from the icon menu."),ToolTipIcon.Info);

   }else if(manual)MessageBox.Show(release.IsNewer?L("Update available. Ask a parent to install it from the icon menu."):L("Playtimely is up to date."),"Playtimely");
  }catch(Exception){if(manual)MessageBox.Show(L("Could not check for updates. Try again later. Your game rules are unchanged."),"Playtimely");}
  finally{busy=false;}
 }
 async Task Update(){
  if(busy||release is null||!release.IsNewer)return;
  if(MessageBox.Show(L("Install the Playtimely update? Windows will ask for administrator permission. Your connection and rules will be kept."),"Playtimely · "+release.Version,MessageBoxButtons.YesNo,MessageBoxIcon.Information)!=DialogResult.Yes)return;
  busy=true;item.Enabled=false;
  string? path=null;
  try{
   // Never accept commands or download locations from unsigned metadata.
   var selected=release;var dir=Path.Combine(Path.GetTempPath(),"Playtimely-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);path=Path.Combine(dir,"Playtimely-Update.exe");
   using var response=await http.GetAsync(selected.Url+"?v="+Uri.EscapeDataString(selected.Version),HttpCompletionOption.ResponseHeadersRead);response.EnsureSuccessStatusCode();
   await using(var input=await response.Content.ReadAsStreamAsync())await using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
    var buffer=new byte[81920];long total=0;int n;using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(5));
    while((n=await input.ReadAsync(buffer,deadline.Token))>0){total+=n;if(total>selected.Size)throw new InvalidDataException("Update size mismatch.");await output.WriteAsync(buffer.AsMemory(0,n),deadline.Token);}if(total!=selected.Size)throw new InvalidDataException("Update download incomplete.");
   }
   await using(var file=File.OpenRead(path)){var hash=Convert.ToHexString(await SHA256.HashDataAsync(file));if(!hash.Equals(selected.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Update checksum mismatch.");}
   using var installer=Process.Start(new ProcessStartInfo(path){UseShellExecute=true,Verb="runas",Arguments="--update-ui"});
   if(installer is not null)await installer.WaitForExitAsync();
   // The elevated installer asks for confirmation and preserves the protected connection.
  }catch(System.ComponentModel.Win32Exception ex)when(ex.NativeErrorCode==1223){MessageBox.Show(L("Administrator permission was declined. Ask a parent and try again. Nothing was changed."),"Playtimely");if(path is not null)File.Delete(path);}
  catch(Exception){MessageBox.Show(L("The update could not be installed. Try again later. Your game rules are unchanged."),"Playtimely");if(path is not null&&File.Exists(path))File.Delete(path);}
  finally{busy=false;item.Enabled=true;}
 }
 public void Dispose(){timer.Stop();timer.Dispose();http.Dispose();}
}
