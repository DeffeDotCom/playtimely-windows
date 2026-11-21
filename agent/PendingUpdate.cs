using System.Security.Cryptography;
namespace GameControl;
// A persisted candidate is never trusted just because it was verified before a restart.
public static class PendingUpdate {
 public static async Task<DesktopRelease> Verify(string directory,string publicKey,CancellationToken stop){
  if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Linked update directory");
  if(DateTime.UtcNow-Directory.GetCreationTimeUtc(directory)>TimeSpan.FromDays(7))throw new InvalidDataException("Expired pending update");
  string manifest=Path.Combine(directory,"release.json"),binary=Path.Combine(directory,"Playtimely.Setup.exe");
  foreach(var path in new[]{manifest,binary})if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Linked update file");
  if(new FileInfo(manifest).Length>16384)throw new InvalidDataException("Manifest too large");
  var release=DesktopRelease.Verify(await File.ReadAllTextAsync(manifest,stop),publicKey);
  if(!release.IsNewer||new FileInfo(binary).Length!=release.Size)throw new InvalidDataException("Obsolete or incomplete update");
  await using var stream=File.OpenRead(binary);
  if(!Convert.ToHexString(await SHA256.HashDataAsync(stream,stop)).Equals(release.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Update hash mismatch");
  return release;
 }
}
