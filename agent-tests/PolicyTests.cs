using System.Security.Cryptography;
using System.Text.Json;
using GameControl;
using Xunit;
public class PolicyTests {
    private static Rule Schedule(string start="18:00",string end="21:00",int[]? days=null)=>new("minecraft",["Minecraft.exe"],"schedule",start,end,days??[1,2,3,4,5,6,7]);
    [Theory]
    [InlineData("2026-10-30T16:59:59Z",false)]
    [InlineData("2026-10-30T17:00:00Z",true)]
    [InlineData("2026-10-30T19:59:59Z",true)]
    [InlineData("2026-10-30T20:00:00Z",false)]
    public void Exact_boundaries_in_family_timezone(string instant,bool expected) => Assert.Equal(expected,PolicyEngine.IsAllowed(Schedule(),DateTimeOffset.Parse(instant),"Europe/Stockholm"));
    [Theory]
    [InlineData("2026-10-30T22:00:00Z",true)]
    [InlineData("2026-10-31T00:30:00Z",true)]
    [InlineData("2026-10-31T01:00:00Z",false)]
    [InlineData("2026-10-31T22:00:00Z",false)]
    public void Overnight_uses_starting_weekday(string instant,bool expected) => Assert.Equal(expected,PolicyEngine.IsAllowed(Schedule("22:00","02:00",[5]),DateTimeOffset.Parse(instant),"Europe/Stockholm"));
    [Fact] public void Summer_time_and_off_days() {
        Assert.True(PolicyEngine.IsAllowed(Schedule(),DateTimeOffset.Parse("2026-07-01T16:00:00Z"),"Europe/Stockholm"));
        Assert.False(PolicyEngine.IsAllowed(Schedule(days:[1]),DateTimeOffset.Parse("2026-10-30T18:00:00Z"),"Europe/Stockholm"));
    }
    [Fact] public void Tampering_wrong_device_and_arbitrary_process_rejected() {
        using var key=RSA.Create(2048);var conn=new Connection(1,12,"https://example.test/api/v1/policy",new string('a',64),key.ExportSubjectPublicKeyInfoPem());
        var p=new Policy(1,12,DateTimeOffset.UtcNow,"Europe/Stockholm",60,[Schedule()]);
        string Sign(Policy value) {var raw=JsonSerializer.SerializeToUtf8Bytes(value,PolicyEngine.Json);return JsonSerializer.Serialize(new Envelope(Convert.ToBase64String(raw),Convert.ToBase64String(key.SignData(raw,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1)),"RS256"),PolicyEngine.Json);}
        Assert.Equal(12,PolicyEngine.Verify(Sign(p),conn).DeviceId);
        Assert.Throws<InvalidDataException>(()=>PolicyEngine.Verify(Sign(p with { DeviceId=13 }),conn));
        Assert.Throws<InvalidDataException>(()=>PolicyEngine.Verify(Sign(p with { Rules=[Schedule() with { Processes=["explorer.exe"] }] }),conn));
        Assert.Throws<InvalidDataException>(()=>PolicyEngine.Verify(Sign(p),conn,p.IssuedAt.AddSeconds(10)));
        var envelope=JsonSerializer.Deserialize<Envelope>(Sign(p),PolicyEngine.Json)!;
        var invalid=envelope with {Payload=Convert.ToBase64String("{}"u8.ToArray())};
        Assert.Throws<InvalidDataException>(()=>PolicyEngine.Verify(JsonSerializer.Serialize(invalid,PolicyEngine.Json),conn));
    }
}

public class TemporaryPermissionTests {
 [Fact] public void Permission_expires_exactly_even_offline_and_projection_keeps_it(){
  var end=DateTimeOffset.Parse("2026-10-30T20:30:17Z");var rule=new Rule("roblox",["RobloxPlayerBeta.exe"],"blocked","18:00","21:00",[1,2,3,4,5,6,7],end);
  Assert.True(PolicyEngine.IsAllowed(rule,end.AddSeconds(-1),"Europe/Stockholm"));Assert.False(PolicyEngine.IsAllowed(rule,end,"Europe/Stockholm"));
  Assert.Equal(end,ScheduleStatus.NextChange(rule,end.AddMinutes(-15),"Europe/Stockholm"));
  var status=DisplayStatus.Create(new Policy(1,1,end.AddHours(-1),"Europe/Stockholm",60,[rule]),end.AddMinutes(-5),null);
  Assert.Equal(end,status.Rules[0].AsRule().AllowedUntil);
 }
 [Fact] public void Schedule_resumes_after_extra_time_without_drift(){
  var end=DateTimeOffset.Parse("2026-10-30T20:30:17Z");var rule=new Rule("roblox",["RobloxPlayerBeta.exe"],"schedule","18:00","21:00",[1,2,3,4,5,6,7],end);
  Assert.True(PolicyEngine.IsAllowed(rule,end.AddSeconds(-1),"Europe/Stockholm"));Assert.False(PolicyEngine.IsAllowed(rule,end,"Europe/Stockholm"));
  Assert.Equal(end,ScheduleStatus.NextChange(rule,end.AddMinutes(-15),"Europe/Stockholm"));
  Assert.True(PolicyEngine.IsAllowed(rule,DateTimeOffset.Parse("2026-10-31T18:30:00Z"),"Europe/Stockholm"));
 }
}

public class ExpandedCatalogTests {
 [Fact] public void Imported_game_policy_accepted_but_arbitrary_process_rejected(){
  const string game="game-1113966530531704943";
  Assert.Equal("Diablo IV",GameCatalog.Name(game));Assert.True(GameCatalog.Allows(game,"Diablo IV.exe"));Assert.False(GameCatalog.Allows(game,"cmd.exe"));
  using var rsa=RSA.Create(2048);var connection=new Connection(1,88,"https://example.test/api/v1/policy",new string('a',64),rsa.ExportSubjectPublicKeyInfoPem());
  var p=new Policy(1,88,DateTimeOffset.UtcNow,"Europe/Stockholm",60,[new Rule(game,["diablo iv.exe"],"blocked","18:00","21:00",[1,2,3])]);
  string Sign(Policy value){var bytes=JsonSerializer.SerializeToUtf8Bytes(value,PolicyEngine.Json);return JsonSerializer.Serialize(new Envelope(Convert.ToBase64String(bytes),Convert.ToBase64String(rsa.SignData(bytes,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1)),"RS256"),PolicyEngine.Json);}
  Assert.Equal(game,PolicyEngine.Verify(Sign(p),connection).Rules[0].App);
  Assert.Throws<InvalidDataException>(()=>PolicyEngine.Verify(Sign(p with{Rules=[p.Rules[0] with{Processes=["cmd.exe"]}]}),connection));
 }
}
