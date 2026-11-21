#Requires -RunAsAdministrator
param([string]$ConnectionFile)
$ErrorActionPreference = 'Stop'
# The native setup owns service, tray, shortcuts and ACLs in one implementation.
$setup = Join-Path $PSScriptRoot 'Playtimely.Setup-preview.exe'
if (-not (Test-Path $setup)) { throw 'Place Playtimely.Setup-preview.exe beside this script, or run the downloaded installer directly.' }
if ($ConnectionFile) {
 $result = Start-Process -FilePath $setup -ArgumentList @('--install', ('"' + (Resolve-Path $ConnectionFile).Path + '"')) -Wait -PassThru
 if ($result.ExitCode -ne 0) { throw 'Setup failed. Run the installer interactively for details.' }
} else { Start-Process -FilePath $setup -Wait }
Write-Host 'Sign out and back in to show the tray icon, or open Playtimely from the Start menu.'
