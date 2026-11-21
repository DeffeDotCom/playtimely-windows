param([ValidateSet('All','Components','Installer')][string]$Stage='All')
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
function Invoke-Dotnet([string[]]$Arguments) {
 & dotnet @Arguments
 if($LASTEXITCODE){throw "dotnet failed: $($Arguments[0])"}
}
if($Stage -eq 'All'){Invoke-Dotnet @('test','agent-tests/Agent.Tests.csproj','--nologo')}
if($Stage -in @('All','Components')){
 foreach($entry in @(@('agent/GameControl.Agent.csproj','agent/out'),@('agent-tray/Playtimely.Tray.csproj','agent-tray/out'))){
  Invoke-Dotnet @('publish',$entry[0],'-c','Release','-r','win-x64','--self-contained','true','-p:PublishSingleFile=true','-p:IncludeNativeLibrariesForSelfExtract=true','-o',$entry[1],'--nologo')
 }
}
if($Stage -in @('All','Installer')){
 foreach($file in @('agent/out/GameControl.Agent.exe','agent-tray/out/Playtimely.Tray.exe')){if(!(Test-Path $file)){throw "Missing client component: $file"}}
 Invoke-Dotnet @('publish','agent-installer/GameControl.Setup.csproj','-c','Release','-r','win-x64','--self-contained','true','-p:PublishSingleFile=true','-p:IncludeNativeLibrariesForSelfExtract=true','-o','agent-installer/out','--nologo')
}
