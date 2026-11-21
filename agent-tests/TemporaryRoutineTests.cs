using GameControl;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;
public class TemporaryRoutineTests {
 [Fact] public void Cached_policy_activates_and_expires_without_network_and_restores_original_budget() {
  var start=DateTimeOffset.Parse("2026-10-23T22:00:00Z");var end=DateTimeOffset.Parse("2026-10-25T23:00:00Z");
  var baseline=new Rule("roblox",["RobloxPlayerBeta.exe"],"blocked","18:00","20:00",[1,2,3,4,5,6,7]);
  var holiday=baseline with {Mode="allowed"};
  var p=new Policy(1,1,start,"Europe/Stockholm",60,[baseline],new(90,120),new(start,end,[holiday],new(180,240)));
  Assert.Same(p,PolicyEngine.Effective(p,start.AddTicks(-1)));
  var active=PolicyEngine.Effective(p,start)!;
  Assert.Equal("allowed",active.Rules[0].Mode);Assert.Equal(240,active.DailyBudget!.WeekendMinutes);
  Assert.Equal("allowed",PolicyEngine.Effective(p,end.AddTicks(-1))!.Rules[0].Mode);
  var restored=PolicyEngine.Effective(p,end)!;
  Assert.Same(p,restored);Assert.Equal("blocked",restored.Rules[0].Mode);Assert.Equal(120,restored.DailyBudget!.WeekendMinutes);
  Assert.Equal("blocked",p.Rules[0].Mode);
 }
 [Fact] public void Signed_temporary_snapshot_validates_process_allowlist_budget_and_dates() {
  using var key=RSA.Create(2048);
  var connection=new Connection(1,1,"https://example.test/api/v1/policy",new string('a',64),key.ExportSubjectPublicKeyInfoPem());
  var now=DateTimeOffset.UtcNow;
  var rule=new Rule("roblox",["RobloxPlayerBeta.exe"],"allowed","18:00","20:00",[1]);
  var temporary=new TemporarySchedule(now,now.AddDays(2),[rule],new(90,120));
  var policy=new Policy(1,1,now,"UTC",60,[],TemporarySchedule:temporary);
  string Sign(Policy value){var raw=JsonSerializer.SerializeToUtf8Bytes(value,PolicyEngine.Json);return JsonSerializer.Serialize(new Envelope(Convert.ToBase64String(raw),Convert.ToBase64String(key.SignData(raw,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1)),"RS256"),PolicyEngine.Json);}
  Assert.NotNull(PolicyEngine.Verify(Sign(policy),connection).TemporarySchedule);
  Assert.Throws<InvalidDataException>(()=>PolicyEngine.Verify(Sign(policy with {TemporarySchedule=temporary with {Rules=[rule with {Processes=["explorer.exe"]}]}}),connection));
  Assert.Throws<InvalidDataException>(()=>PolicyEngine.Verify(Sign(policy with {TemporarySchedule=temporary with {EndsAt=now}}),connection));
  Assert.Throws<InvalidDataException>(()=>PolicyEngine.Verify(Sign(policy with {TemporarySchedule=temporary with {EndsAt=now.AddDays(92)}}),connection));
  Assert.Throws<InvalidDataException>(()=>PolicyEngine.Verify(Sign(policy with {TemporarySchedule=temporary with {DailyBudget=new(721,120)}}),connection));
 }

}
