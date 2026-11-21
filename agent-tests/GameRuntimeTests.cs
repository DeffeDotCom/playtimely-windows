using GameControl;
using Xunit;
public class GameRuntimeTests {
 [Theory]
 [InlineData("dotnet.exe build project.csproj",false)]
 [InlineData("dotnet.exe C:\\sdk\\MSBuild.dll /build",false)]
 [InlineData("dotnet.exe other.dll tModLoader.dll",false)]
 [InlineData("dotnet.exe tModLoader.dll",true)]
 [InlineData("\"C:\\Program Files\\dotnet\\dotnet.exe\" \"D:\\Steam Games\\tModLoader\\tModLoader.dll\"",true)]
 public void Shared_runtime_requires_actual_game_assembly(string command,bool expected)=>Assert.Equal(expected,GameCatalog.IsGameRuntime("game-1124349969906815007","dotnet.exe",command));
 [Fact] public void Shell_and_unknown_runtime_are_not_games(){Assert.False(GameCatalog.IsGameRuntime("game-1124349969906815007","busybox64.exe",null));Assert.False(GameCatalog.IsGameRuntime("other","dotnet.exe","dotnet.exe tModLoader.dll"));Assert.False(GameCatalog.IsGameRuntime("game-1124349969906815007","dotnet.exe",null));}
}
