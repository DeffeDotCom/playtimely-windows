using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace GameControl;
// Separate hosted worker: slow or failed downloads never block enforcement.
public sealed class AutomaticUpdate(ILogger<AutomaticUpdate> logger):BackgroundService {
 static readonly string Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"GameControl");
 static readonly string Status=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"PlaytimelyStatus","status.json");
 protected override async Task ExecuteAsync(CancellationToken stop){
  try{await Task.Delay(TimeSpan.FromMinutes(Random.Shared.Next(10,16)),stop);
   while(!stop.IsCancellationRequested){
    try{await Attempt(stop);}catch(OperationCanceledException)when(stop.IsCancellationRequested){break;}catch(Exception ex){logger.LogWarning("Automatic update deferred ({Type}); existing game controls remain active.",ex.GetType().Name);}
    // Cheap idle recheck; the persisted timestamp still limits network attempts to six hours.
    await Task.Delay(TimeSpan.FromMinutes(Random.Shared.Next(5,11)),stop);
   }
  }catch(OperationCanceledException)when(stop.IsCancellationRequested){}
 }
 bool Idle(){try{var s=JsonSerializer.Deserialize<DisplayStatus>(File.ReadAllText(Status),PolicyEngine.Json);return s is not null&&s.HasPolicy&&DateTimeOffset.UtcNow-s.UpdatedAt<TimeSpan.FromSeconds(15)&&s.RunningGames is {Length:0}&&s.ClosingGames is not {Length:>0};}catch{return false;}}
 async Task Attempt(CancellationToken stop){
  using var key=Assembly.GetExecutingAssembly().GetManifestResourceStream("Playtimely.ReleaseKey")!;using var reader=new StreamReader(key);
  string publicKey=reader.ReadToEnd();
  if(!Idle())return;
  // Resume a complete signed candidate before the network backoff check.
  foreach(var pending in Directory.EnumerateDirectories(Root,"update-*")){
   if(!File.Exists(Path.Combine(pending,"release.json")))continue;
   DesktopRelease candidate;
   try {candidate=await PendingUpdate.Verify(pending,publicKey,stop);}
   catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or CryptographicException or FormatException){
    if((File.GetAttributes(pending)&FileAttributes.ReparsePoint)==0)Directory.Delete(pending,true);
    continue;
   }
   await Install(pending,candidate,stop);return;
  }
  foreach(var old in Directory.EnumerateDirectories(Root,"update-*")){
   try{if((File.GetAttributes(old)&FileAttributes.ReparsePoint)==0&&Directory.GetCreationTimeUtc(old)<DateTime.UtcNow.AddHours(-6))Directory.Delete(old,true);}catch(IOException){}catch(UnauthorizedAccessException){}
  }
  if(!Idle())return;
  var stamp=Path.Combine(Root,"update-attempt.txt");
  if(File.Exists(stamp)&&DateTimeOffset.TryParse(await File.ReadAllTextAsync(stamp,stop),out var last)&&DateTimeOffset.UtcNow-last<TimeSpan.FromHours(6))return;
  // Persist before network I/O to bound retry frequency even across service restarts.
  await File.WriteAllTextAsync(stamp,DateTimeOffset.UtcNow.ToString("O"),stop);
  using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromMinutes(10),MaxResponseContentBufferSize=16384};
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(stop);deadline.CancelAfter(TimeSpan.FromSeconds(30));
  var envelope=await http.GetStringAsync("https://playtimely.com/desktop/latest",deadline.Token);
  var release=DesktopRelease.Verify(envelope,publicKey);if(!release.IsNewer)return;
  var directory=Path.Combine(Root,"update-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
  var file=Path.Combine(directory,"Playtimely.Setup.exe");bool staged=false;
  try{
   using var download=CancellationTokenSource.CreateLinkedTokenSource(stop);download.CancelAfter(TimeSpan.FromMinutes(10));
   using var response=await http.GetAsync(release.Url+"?v="+Uri.EscapeDataString(release.Version),HttpCompletionOption.ResponseHeadersRead,download.Token);response.EnsureSuccessStatusCode();
   await using(var input=await response.Content.ReadAsStreamAsync(download.Token))await using(var output=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
    var buffer=new byte[65536];long total=0;int count;
    while((count=await input.ReadAsync(buffer,download.Token))>0){total+=count;if(total>release.Size)throw new InvalidDataException("Update size mismatch");await output.WriteAsync(buffer.AsMemory(0,count),download.Token);}if(total!=release.Size)throw new InvalidDataException("Incomplete update");
   }
   await using(var stream=File.OpenRead(file)){if(!Convert.ToHexString(await SHA256.HashDataAsync(stream,stop)).Equals(release.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Update hash mismatch");}
   // Marker is committed only after complete download/hash validation.
   await File.WriteAllTextAsync(Path.Combine(directory,"release.json"),envelope,stop);staged=true;
   await Install(directory,release,stop);
  }finally{if(!staged){try{Directory.Delete(directory,true);}catch{}}}
 }
 async Task Install(string directory,DesktopRelease release,CancellationToken stop){
   if(!Idle()||UpdateActivity.HasPossibleGame())return;
   var stamp=Path.Combine(directory,"install-attempt.txt");
   if(File.Exists(stamp)&&DateTimeOffset.TryParse(await File.ReadAllTextAsync(stamp,stop),out var last)&&DateTimeOffset.UtcNow-last<TimeSpan.FromMinutes(30))return;
   await File.WriteAllTextAsync(stamp,DateTimeOffset.UtcNow.ToString("O"),stop);
   using var installer=Process.Start(new ProcessStartInfo(Path.Combine(directory,"Playtimely.Setup.exe"),"--auto-upgrade"){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=directory})??throw new IOException("Installer did not start");
   logger.LogInformation("Automatic update {Version} started.",release.Version);
   await installer.WaitForExitAsync(stop);
   if(installer.ExitCode!=0)logger.LogWarning("Automatic update returned {Code}; candidate retained for retry.",installer.ExitCode);
 }
}
