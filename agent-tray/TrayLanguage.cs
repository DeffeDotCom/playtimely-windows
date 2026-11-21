using System.Globalization;
using System.Text.Json;
namespace GameControl;
public static class TrayLanguage {
 public static readonly string[] Supported=["en","sv","de","da","nb","fi","fr","es","it","pt","nl","pl","cs","ro","uk","ru","tr","ja","ko","zh"];
 private static readonly Dictionary<string,Dictionary<string,string>> Catalog=Load();
 public static string Current {get;private set;}=Resolve(CultureInfo.CurrentUICulture.Name);
 public static string Resolve(string? culture){
  try {var code=CultureInfo.GetCultureInfo(culture??"").TwoLetterISOLanguageName;if(code is "no" or "nn")code="nb";return Supported.Contains(code)?code:"en";}catch(CultureNotFoundException){return "en";}
 }
 public static void Select(string? language)=>Current=Resolve(language);
 public static string Get(string key)=>Catalog[Current].TryGetValue(key,out var text)?text:Catalog["en"].GetValueOrDefault(key,key);
 public static string Day(int day)=>CultureInfo.GetCultureInfo(Current).DateTimeFormat.GetAbbreviatedDayName((DayOfWeek)(day%7));
 public static IReadOnlyDictionary<string,string> Strings(string language)=>Catalog[language];
 private static Dictionary<string,Dictionary<string,string>> Load(){
  var result=new Dictionary<string,Dictionary<string,string>>();
  foreach(var language in Supported){using var stream=typeof(TrayLanguage).Assembly.GetManifestResourceStream("Playtimely.Languages."+language+".json")??throw new InvalidDataException("Missing language resource");result[language]=JsonSerializer.Deserialize<Dictionary<string,string>>(stream)??throw new InvalidDataException("Invalid language resource");}
  return result;
 }
}
