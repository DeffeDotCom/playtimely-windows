using System.Reflection;
using System.Text.Json;
namespace GameControl;
public sealed record CatalogGame(string Key,string Name,string[] Processes);
public static class GameCatalog {
 private static readonly Dictionary<string,CatalogGame> Games=Load();
 private static Dictionary<string,CatalogGame> Load(){
  using var stream=typeof(GameCatalog).Assembly.GetManifestResourceStream("Playtimely.GameCatalog")??throw new InvalidDataException("Missing game catalog");
  return (JsonSerializer.Deserialize<CatalogGame[]>(stream,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??[]).ToDictionary(g=>g.Key,StringComparer.Ordinal);
 }
 public static IEnumerable<CatalogGame> All=>Games.Values;
 private static readonly Dictionary<string,CatalogGame[]> ProcessIndex=Games.Values.SelectMany(g=>g.Processes.Select(p=>(Name:Path.GetFileNameWithoutExtension(p),Game:g)))
  .GroupBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.Select(x=>x.Game).DistinctBy(x=>x.Key).ToArray(),StringComparer.OrdinalIgnoreCase);
 public static IEnumerable<CatalogGame> MatchingProcesses(IEnumerable<string> names){
  var found=new HashSet<string>(StringComparer.Ordinal);
  foreach(var name in names){
   if(ProcessIndex.TryGetValue(name,out var games))foreach(var game in games)if(found.Add(game.Key))yield return game;
   if((name.Equals("java",StringComparison.OrdinalIgnoreCase)||name.Equals("javaw",StringComparison.OrdinalIgnoreCase))&&Games.TryGetValue("minecraft",out var minecraft)&&found.Add(minecraft.Key))yield return minecraft;
  }
 }

 // Shared runtime names alone are not evidence of a running game.
 public static bool IsGameRuntime(string app,string executable,string? commandLine){
  var name=executable.Replace('\\','/').Split('/').Last();
  if(name.Equals("busybox64.exe",StringComparison.OrdinalIgnoreCase))return false;
  if(!name.Equals("dotnet.exe",StringComparison.OrdinalIgnoreCase))return true;
  if(app!="game-1124349969906815007"||string.IsNullOrWhiteSpace(commandLine))return false;
  var args=System.Text.RegularExpressions.Regex.Matches(commandLine,"\"([^\"]+)\"|([^\\s]+)")
   .Select(m=>m.Groups[1].Success?m.Groups[1].Value:m.Groups[2].Value).Skip(1);
  var assembly=args.FirstOrDefault(a=>a.EndsWith(".dll",StringComparison.OrdinalIgnoreCase));
  return assembly is not null&&assembly.Replace('\\','/').Split('/').Last().Equals("tModLoader.dll",StringComparison.OrdinalIgnoreCase);
 }
 // Missing command-line access is a conservative deferral, not proof of idle.
 public static bool RuntimeBlocksUpdate(string name,string? commandLine){
  if(string.IsNullOrWhiteSpace(commandLine))return true;
  if(name.Equals("dotnet",StringComparison.OrdinalIgnoreCase))return IsGameRuntime("game-1124349969906815007","dotnet.exe",commandLine);
  return commandLine.Contains("net.minecraft.client.main.Main",StringComparison.Ordinal);
 }
 public static bool Contains(string key)=>key is not null&&Games.ContainsKey(key);
 public static string Name(string key)=>key=="all_known"?"All known games":Games.TryGetValue(key,out var game)?game.Name:key;
 public static bool Allows(string key,string process)=>Games.TryGetValue(key,out var game)&&game.Processes.Contains(process,StringComparer.OrdinalIgnoreCase);
}
