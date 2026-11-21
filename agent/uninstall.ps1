#Requires -RunAsAdministrator
$ErrorActionPreference = 'Stop'
$setup = Join-Path $PSScriptRoot 'Playtimely.Setup-preview.exe'
if (-not (Test-Path $setup)) { throw 'Run Uninstall agent from the downloaded Playtimely setup.' }
$result = Start-Process -FilePath $setup -ArgumentList '--uninstall' -Wait -PassThru
if ($result.ExitCode -ne 0) { throw 'Uninstall failed. Run setup interactively for details.' }
Write-Host 'Service and tray autostart removed. Local files retained for deliberate cleanup.'
