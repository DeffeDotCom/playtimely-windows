using GameControl;using Xunit;
public class ChoreTests {
 readonly string id=Guid.NewGuid().ToString();
 [Fact] public void Checklist_is_additional_gate_and_resets_exactly_at_local_midnight(){
  var c=new ChoreList(1,"2026-11-13",[id],[new(id,5,"Finish homework"),new(Guid.NewGuid().ToString(),6,"Help tidy")]);c.Validate();
  Assert.False(c.Blocks(DateTimeOffset.Parse("2026-11-13T22:59:59Z"),"Europe/Stockholm"));
  Assert.True(c.Blocks(DateTimeOffset.Parse("2026-11-13T23:00:00Z"),"Europe/Stockholm"));
  Assert.Empty(c.Today(DateTimeOffset.Parse("2026-11-15T12:00:00Z"),"Europe/Stockholm"));
  var rule=new Rule("minecraft",["Minecraft.exe"],"blocked","00:00","23:59",[1,2,3,4,5,6,7]);Assert.False(PolicyEngine.IsAllowed(rule,DateTimeOffset.Parse("2026-11-13T12:00:00Z"),"Europe/Stockholm"));
 }
 [Fact] public void Invalid_or_duplicate_tasks_and_unrelated_completions_are_rejected(){
  var c=new ChoreList(1,"2026-11-13",[],[new(id,5,"Homework")]);c.Validate();
  Assert.Throws<InvalidDataException>(()=>(c with {Tasks=[new(id,8,"Homework")]}).Validate());
  Assert.Throws<InvalidDataException>(()=>(c with {Tasks=[new(id,5,"Homework"),new(id,5,"Homework")]}).Validate());
  Assert.Throws<InvalidDataException>(()=>(c with {CompletedIds=[Guid.NewGuid().ToString()]}).Validate());
 }
}
