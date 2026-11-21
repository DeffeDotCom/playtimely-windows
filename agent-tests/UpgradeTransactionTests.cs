using GameControl;
using Xunit;
public class UpgradeTransactionTests {
 [Theory][InlineData("success")][InlineData("health")][InlineData("second-file")][InlineData("missing-stage")]
 public void Replacement_is_reversible_and_preserves_unrelated_rules(string failure){
  var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  try{
   var installed=new[]{Path.Combine(dir,"agent"),Path.Combine(dir,"tray")};var candidates=new[]{Path.Combine(dir,"new-agent"),Path.Combine(dir,"new-tray")};
   foreach(var f in installed)File.WriteAllText(f,"old");foreach(var f in candidates)File.WriteAllText(f,"new");
   var policy=Path.Combine(dir,"policy.json");File.WriteAllText(policy,"signed rules");int stops=0,starts=0;
   if(failure=="missing-stage")File.Delete(candidates[1]);
   void Stop(){stops++;if(failure=="second-file"&&stops==1){File.Delete(candidates[1]);File.SetAttributes(installed[1],FileAttributes.ReadOnly);}}
   void Start(){starts++;if(failure=="health"&&starts==1)throw new IOException("New version unhealthy");}
   void Update()=>UpgradeTransaction.Run(installed,candidates,Path.Combine(dir,"backup"),Stop,Start);
   if(failure=="success")Update();else Assert.Throws<IOException>(Update);
   Assert.All(installed,p=>Assert.Equal(failure=="success"?"new":"old",File.ReadAllText(p)));
   Assert.Equal("signed rules",File.ReadAllText(policy));
   if(failure=="missing-stage"){Assert.Equal(0,stops);Assert.Equal(0,starts);}else Assert.True(starts>=1);
  }finally{foreach(var f in Directory.EnumerateFiles(dir,"*",SearchOption.AllDirectories))File.SetAttributes(f,FileAttributes.Normal);Directory.Delete(dir,true);}
 }
}
