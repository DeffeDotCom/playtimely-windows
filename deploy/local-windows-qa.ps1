# Run in an elevated interactive PowerShell 7 terminal on a dedicated disposable Windows VM only.
$ErrorActionPreference='Stop'
if ($env:PLAYTIMELY_ISOLATED_WINDOWS_QA -ne '1') { throw 'Set PLAYTIMELY_ISOLATED_WINDOWS_QA=1 on the dedicated disposable QA VM first.' }
if (-not $env:COMPUTERNAME.StartsWith('PT-BUILD-')) { throw 'Refusing: dedicated QA computer name must start PT-BUILD-.' }
if (Get-Service GameControlAgent -ErrorAction SilentlyContinue) { throw 'Refusing to overwrite an existing Playtimely installation. Restore the clean VM snapshot first.' }
if (Test-Path "$env:ProgramData/GameControl/connection.json") { throw 'Refusing to touch an existing device connection.' }
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run elevated on the QA VM.' }
Set-Location (Split-Path $PSScriptRoot -Parent)
$env:RUNNER_TEMP=Join-Path $env:TEMP ('Playtimely-QA-'+[guid]::NewGuid())
New-Item -ItemType Directory -Force $env:RUNNER_TEMP | Out-Null
try {
  dotnet test agent-tests/Agent.Tests.csproj
  if ($LASTEXITCODE) { throw 'Core tests failed' }
  foreach ($entry in @(@('agent/GameControl.Agent.csproj','agent/out'),@('agent-tray/Playtimely.Tray.csproj','agent-tray/out'),@('agent-installer/GameControl.Setup.csproj','agent-installer/out'))) {
    dotnet publish $entry[0] -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $entry[1]
    if ($LASTEXITCODE) { throw 'Build failed' }
  }
  [IO.File]::WriteAllText("$env:RUNNER_TEMP/broken-agent.exe",'isolated invalid rollback candidate')
  dotnet publish agent-installer/GameControl.Setup.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true "-p:AgentBinaryPath=$env:RUNNER_TEMP/broken-agent.exe" -o "$env:RUNNER_TEMP/broken-setup"
  if ($LASTEXITCODE) { throw 'Rollback fixture failed' }
  & ./deploy/windows-smoke.ps1
} finally {
  Write-Host 'Restore the clean VM snapshot before the next run. No deployment or publication was performed.'
}
