using GameControl;
using Xunit;
using System.Security.Cryptography;
using System.Text.Json;
public class PendingUpdateTests {
 [Fact] public async Task ResumeRevalidatesSignatureSizeHashAndVersion(){
  var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());Directory.CreateDirectory(dir);
  using var rsa=RSA.Create(2048);var bytes=new byte[]{1,2,3,4};
  string Sign(string version){var p=JsonSerializer.SerializeToUtf8Bytes(new{version,url="https://playtimely.com/downloads/Playtimely.Setup-preview.exe",sha256=Convert.ToHexString(SHA256.HashData(bytes)),size=bytes.Length});return JsonSerializer.Serialize(new{payload=Convert.ToBase64String(p),signature=Convert.ToBase64String(rsa.SignData(p,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1)),algorithm="RS256"});}
  var manifest=Path.Combine(dir,"release.json");var binary=Path.Combine(dir,"Playtimely.Setup.exe");
  try{
   await File.WriteAllTextAsync(manifest,Sign("999.0.0"));await File.WriteAllBytesAsync(binary,bytes);
   var release=await PendingUpdate.Verify(dir,rsa.ExportSubjectPublicKeyInfoPem(),default);Assert.Equal("999.0.0",release.Version);
   // Simulated worker restart: no in-memory state is necessary to reuse the download.
   Assert.Equal(release,await PendingUpdate.Verify(dir,rsa.ExportSubjectPublicKeyInfoPem(),default));
   using var wrong=RSA.Create(2048);await Assert.ThrowsAsync<InvalidDataException>(()=>PendingUpdate.Verify(dir,wrong.ExportSubjectPublicKeyInfoPem(),default));
   await File.WriteAllBytesAsync(binary,new byte[]{4,3,2,1});await Assert.ThrowsAsync<InvalidDataException>(()=>PendingUpdate.Verify(dir,rsa.ExportSubjectPublicKeyInfoPem(),default));
   await File.WriteAllBytesAsync(binary,new byte[]{1});await Assert.ThrowsAsync<InvalidDataException>(()=>PendingUpdate.Verify(dir,rsa.ExportSubjectPublicKeyInfoPem(),default));
   await File.WriteAllBytesAsync(binary,bytes);await File.WriteAllTextAsync(manifest,Sign(DesktopRelease.CurrentVersion));await Assert.ThrowsAsync<InvalidDataException>(()=>PendingUpdate.Verify(dir,rsa.ExportSubjectPublicKeyInfoPem(),default));
  }finally{Directory.Delete(dir,true);}
 }
 [Fact] public void HistoryBatchesWritesRetriesFailuresAndDoesNotReplayMissedTicks(){
  var checkpoint=new HistoryCheckpoint();int writes=0;
  for(int t=0;t<=60;t+=2)checkpoint.SaveWhenDue(t,()=>writes++);
  Assert.Equal(4,writes);
  Assert.Throws<IOException>(()=>checkpoint.SaveWhenDue(100,()=>throw new IOException()));
  checkpoint.SaveWhenDue(100,()=>writes++);Assert.Equal(5,writes);
  checkpoint.SaveWhenDue(3600,()=>writes++);Assert.Equal(6,writes);
 }
 [Fact] public void NonGraceExhaustionDoesNotDirtyClockEveryHeartbeat(){
  foreach(var action in new[]{"notify","finish","stop"}){
   var budget=new BudgetExhaustion();var now=DateTimeOffset.UtcNow;
   budget.Update("2026-11-07",0,action,now,0,["1:2"]);
   var before=JsonSerializer.Serialize(budget);
   budget.Update("2026-11-07",0,action,now.AddSeconds(2),2,["1:2"]);
   Assert.Equal(before,JsonSerializer.Serialize(budget));
  }
 }
 [Theory]
 [InlineData("dotnet","dotnet.exe exec VBCSCompiler.dll",false)]
 [InlineData("dotnet","dotnet.exe tModLoader.dll",true)]
 [InlineData("java","java.exe -jar server.jar",false)]
 [InlineData("javaw","javaw.exe net.minecraft.client.main.Main",true)]
 [InlineData("java",null,true)]
 public void UpdateCheckDistinguishesSharedRuntimes(string name,string? command,bool blocked)=>Assert.Equal(blocked,GameCatalog.RuntimeBlocksUpdate(name,command));
 [Theory][InlineData("notify",0,true,false)][InlineData("notify",20,false,false)][InlineData("finish",0,true,true)][InlineData("stop",0,true,true)][InlineData("grace",0,true,true)][InlineData("stop",20,false,true)][InlineData("stop",60,false,false)]
 public void ReminderDoesNotAccelerateDiscoveryButEnforcementStillDoes(string action,int remaining,bool exhausted,bool fast)=>Assert.Equal(fast,ProcessScanSchedule.NeedsFastDiscovery(action,remaining,exhausted));
}
