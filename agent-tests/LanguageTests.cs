using GameControl;
using Xunit;
public class LanguageTests {
 [Theory]
 [InlineData("de-DE","de")][InlineData("de-AT","de")][InlineData("sv-FI","sv")][InlineData("nb-NO","nb")][InlineData("nn-NO","nb")][InlineData("no-NO","nb")][InlineData("en-GB","en")][InlineData("pt-BR","pt")][InlineData("zh-CN","zh")][InlineData("ja-JP","ja")][InlineData("az-AZ","en")][InlineData("","en")]
 public void Regional_windows_languages_resolve_with_safe_fallback(string culture,string expected)=>Assert.Equal(expected,TrayLanguage.Resolve(culture));
 [Fact] public void Every_language_contains_all_text_and_no_empty_values(){
  Assert.Equal(20,TrayLanguage.Supported.Length);var english=TrayLanguage.Strings("en");Assert.Contains("Game time is over. Closing the game.",english.Keys);
  foreach(var code in TrayLanguage.Supported){var messages=TrayLanguage.Strings(code);Assert.Equal(english.Keys.OrderBy(k=>k),messages.Keys.OrderBy(k=>k));Assert.All(messages.Values,v=>Assert.False(string.IsNullOrWhiteSpace(v)));}
 }
 [Fact] public void German_child_can_understand_every_request_choice(){
  TrayLanguage.Select("de-DE");Assert.Equal("Gesperrt",TrayLanguage.Get("Locked"));Assert.Equal("Anfrage senden",TrayLanguage.Get("Send request"));Assert.Equal("Hausaufgaben sind fertig",TrayLanguage.Get("Homework is done"));Assert.Equal("Mo",TrayLanguage.Day(1).TrimEnd('.'));TrayLanguage.Select("en");
 }
}
