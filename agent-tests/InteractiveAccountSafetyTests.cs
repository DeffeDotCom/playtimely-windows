using GameControl;
using Xunit;
public class InteractiveAccountSafetyTests {
 [Theory][InlineData("admin,unknown","admin")][InlineData("standard,unknown","unknown")][InlineData("standard,no_user","standard")][InlineData("no_user","no_user")][InlineData("","no_user")][InlineData("unexpected","unknown")]
 public void AggregateDoesNotMistakePartialInspectionForSafety(string input,string expected)=>Assert.Equal(expected,InteractiveAccountSafety.Combine(input.Split(',',StringSplitOptions.RemoveEmptyEntries)));
}
