param([Parameter(Mandatory)][switch]$Prototype,[Parameter(Mandatory)][string]$MakeAppxPath)
$ErrorActionPreference='Stop'
if(!$Prototype){throw 'This is packaging proof only, not a supported installation'}
$root=(Resolve-Path "$PSScriptRoot/../..").Path
$out=Join-Path $root 'artifacts/msix-prototype'
if(Test-Path $out){throw 'Use a clean output directory; prototype output already exists'}
New-Item -ItemType Directory -Force "$out/payload/Assets" | Out-Null
Copy-Item "$root/agent/out/GameControl.Agent.exe" "$out/payload/"
Copy-Item "$root/agent-tray/out/Playtimely.Tray.exe" "$out/payload/"
Copy-Item "$PSScriptRoot/AppxManifest.xml" "$out/payload/"
Add-Type -AssemblyName System.Drawing
$icon=[Drawing.Icon]::new("$root/agent-tray/playtimely.ico")
try {
 $image=$icon.ToBitmap()
 try {
  foreach($asset in @(@('StoreLogo',50),@('Square44x44Logo',44),@('Square150x150Logo',150))){
   $bitmap=[Drawing.Bitmap]::new([int]$asset[1],[int]$asset[1])
   try {$g=[Drawing.Graphics]::FromImage($bitmap);try{$g.Clear([Drawing.Color]::Transparent);$g.DrawImage($image,0,0,$bitmap.Width,$bitmap.Height)}finally{$g.Dispose()};$bitmap.Save("$out/payload/Assets/$($asset[0]).png",[Drawing.Imaging.ImageFormat]::Png)}finally{$bitmap.Dispose()}
  }
 }finally{$image.Dispose()}
}finally{$icon.Dispose()}
# Do NOT pass /nv: schema and packaging validation must remain enabled.
& $MakeAppxPath pack /d "$out/payload" /p "$out/Playtimely-UNSIGNED-PROTOTYPE.msix" /o
if($LASTEXITCODE){throw 'MakeAppx validation/package build failed'}
Get-FileHash "$out/Playtimely-UNSIGNED-PROTOTYPE.msix" -Algorithm SHA256
Write-Host 'UNSIGNED PACKAGING PROOF ONLY. Not installed, submitted or published.'
