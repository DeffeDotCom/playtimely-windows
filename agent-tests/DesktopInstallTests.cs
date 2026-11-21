using GameControl;
using Xunit;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
public class DesktopInstallTests {
 [Fact] public void InstalledVersionUsesAssemblyAndSameReleaseIsNotAnUpdate(){
  Assert.Equal(typeof(DesktopRelease).Assembly.GetName().Version!.ToString(3),DesktopRelease.CurrentVersion);
  Assert.False(new DesktopRelease(DesktopRelease.CurrentVersion,"", "",1).IsNewer);
  Assert.False(new DesktopRelease("0.0.0","", "",1).IsNewer);
  var v=Version.Parse(DesktopRelease.CurrentVersion);
  Assert.True(new DesktopRelease(new Version(v.Major,v.Minor,v.Build+1).ToString(),"", "",1).IsNewer);
 }

 [Fact] public void Personal_bundle_reads_only_bounded_footer_and_plain_installer_has_none(){
  var path=Path.GetTempFileName();try{File.WriteAllText(path,"MZ ordinary binary");Assert.Null(InstallerBundle.Read(path));
   var json=Encoding.UTF8.GetBytes("{\"token\":\"private\"}");using(var s=new FileStream(path,FileMode.Append)){s.Write(json);s.Write(BitConverter.GetBytes(json.Length));s.Write(Encoding.ASCII.GetBytes("PLAYTIMELY-CONN1"));}
   Assert.Equal(Encoding.UTF8.GetString(json),InstallerBundle.Read(path));
   using(var s=new FileStream(path,FileMode.Open)){s.Seek(-Encoding.ASCII.GetByteCount("PLAYTIMELY-CONN1")-4,SeekOrigin.End);s.Write(BitConverter.GetBytes(int.MaxValue));}Assert.Throws<InvalidDataException>(()=>InstallerBundle.Read(path));
  }finally{File.Delete(path);}
 }
 [Fact] public void Release_requires_signature_fixed_origin_bounded_size_and_newer_version(){
  using var rsa=RSA.Create(2048);
  string Sign(string url,string hash,long size){var payload=JsonSerializer.SerializeToUtf8Bytes(new{version=new Version(Version.Parse(DesktopRelease.CurrentVersion).Major+1,0,0).ToString(),url,sha256=hash,size});return JsonSerializer.Serialize(new{payload=Convert.ToBase64String(payload),signature=Convert.ToBase64String(rsa.SignData(payload,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1)),algorithm="RS256"});}
  var url="https://playtimely.com/downloads/Playtimely.Setup-preview.exe";var valid=Sign(url,new string('a',64),1000);
  Assert.True(DesktopRelease.Verify(valid,rsa.ExportSubjectPublicKeyInfoPem()).IsNewer);
  using var other=RSA.Create(2048);Assert.Throws<InvalidDataException>(()=>DesktopRelease.Verify(valid,other.ExportSubjectPublicKeyInfoPem()));
  Assert.Throws<InvalidDataException>(()=>DesktopRelease.Verify(Sign("https://evil.test/app.exe",new string('a',64),1000),rsa.ExportSubjectPublicKeyInfoPem()));
  Assert.Throws<InvalidDataException>(()=>DesktopRelease.Verify(Sign(url,new string('x',64),1000),rsa.ExportSubjectPublicKeyInfoPem()));
  Assert.Throws<InvalidDataException>(()=>DesktopRelease.Verify(Sign(url,new string('a',64),400000001),rsa.ExportSubjectPublicKeyInfoPem()));
 }
}
