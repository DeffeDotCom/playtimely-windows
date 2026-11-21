using System.Globalization;
using GameControl;
internal static class LanguagePreference {
 private static readonly string PathName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Playtimely","language.txt");
 public static string? Override {get;private set;}
 public static void Load(){
  try{if(File.Exists(PathName)&&new FileInfo(PathName).Length<20){var saved=File.ReadAllText(PathName).Trim();if(TrayLanguage.Supported.Contains(saved))Override=saved;}}catch(IOException){}catch(UnauthorizedAccessException){}
  TrayLanguage.Select(Override??CultureInfo.CurrentUICulture.Name);
 }
 public static void Save(string? language){
  Override=language;TrayLanguage.Select(language??CultureInfo.CurrentUICulture.Name);
  try{Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);File.WriteAllText(PathName,language??"");}catch(IOException){}catch(UnauthorizedAccessException){}
 }
}
