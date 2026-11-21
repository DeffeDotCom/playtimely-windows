using System.Security.Cryptography;
using System.Text.Json;
namespace GameControl;
public sealed record DesktopRelease(string Version,string Url,string Sha256,long Size) {
 public static string CurrentVersion => typeof(DesktopRelease).Assembly.GetName().Version!.ToString(3);
 public static DesktopRelease Verify(string envelope,string publicKey) {
  if(envelope.Length>16384)throw new InvalidDataException("Release information too large.");
  using var doc=JsonDocument.Parse(envelope);var root=doc.RootElement;
  if(root.GetProperty("algorithm").GetString()!="RS256")throw new InvalidDataException("Invalid update signature.");
  var payload=Convert.FromBase64String(root.GetProperty("payload").GetString()!);
  using var rsa=RSA.Create();rsa.ImportFromPem(publicKey);
  if(!rsa.VerifyData(payload,Convert.FromBase64String(root.GetProperty("signature").GetString()!),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1))throw new InvalidDataException("Invalid update signature.");
  using var data=JsonDocument.Parse(payload);var p=data.RootElement;
  var release=new DesktopRelease(p.GetProperty("version").GetString()!,p.GetProperty("url").GetString()!,p.GetProperty("sha256").GetString()!,p.GetProperty("size").GetInt64());
  if(!System.Version.TryParse(release.Version,out _)||release.Url!="https://playtimely.com/downloads/Playtimely.Setup-preview.exe"||release.Sha256.Length!=64||!release.Sha256.All(Uri.IsHexDigit)||release.Size<1||release.Size>400_000_000)throw new InvalidDataException("Invalid update information.");
  return release;
 }
 public bool IsNewer=>System.Version.Parse(Version)>System.Version.Parse(CurrentVersion);
}
