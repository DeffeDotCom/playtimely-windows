$ErrorActionPreference='Stop'
# Start-Process -Wait follows descendants on Windows, including the deliberately
# long-lived tray opened after install. Wait only for the installer itself.
function Wait-InstallerProcess([Diagnostics.Process]$Process) {
 $null=$Process.Handle # Keep a real handle so Windows PowerShell retains ExitCode.
 if(!$Process.WaitForExit(120000)){throw 'Installer process exceeded its two-minute deadline'}
 $Process.Refresh()
 return $Process
}
New-Item -ItemType Directory -Force qa-artifacts | Out-Null
$rsa=[System.Security.Cryptography.RSA]::Create(2048)
# Accept TCP but never answer TLS: reproduce a hung network fetch without external services.
$listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,44443);$listener.Start()
$conn=@{schema_version=1;device_id=99;token=('a'*64);policy_url='https://127.0.0.1:44443/policy';public_key=$rsa.ExportSubjectPublicKeyInfoPem()}
$conn | ConvertTo-Json | Set-Content "$env:RUNNER_TEMP/connection.json"
$setup=(Resolve-Path 'agent-installer/out/GameControl.Setup.exe').Path
# Reproduce a directory created before installation with explicit broad permissions and file hard links.
$privateDir="$env:ProgramData/GameControl"
New-Item -ItemType Directory -Force $privateDir|Out-Null
& icacls.exe $privateDir /grant '*S-1-5-32-545:(OI)(CI)F'|Out-Null
if($LASTEXITCODE){throw 'Could not seed unsafe fixture permissions'}
$sentinel="$env:ProgramData/Playtimely-QA-$([guid]::NewGuid()).txt"
Set-Content $sentinel 'untouched-install-test'
New-Item -ItemType HardLink -Path "$privateDir/connection.json" -Target $sentinel|Out-Null
New-Item -ItemType HardLink -Path "$privateDir/policy.json.tmp" -Target $sentinel|Out-Null

# Exercise the actual single-file personalized installer, including PE/.NET overlay execution.
$personal="$env:RUNNER_TEMP/Playtimely-Install.exe"
Copy-Item $setup $personal
$json=[Text.Encoding]::UTF8.GetBytes((Get-Content "$env:RUNNER_TEMP/connection.json" -Raw))
$stream=[IO.File]::Open($personal,[IO.FileMode]::Append)
$stream.Write($json);$stream.Write([BitConverter]::GetBytes([int]$json.Length));$stream.Write([Text.Encoding]::ASCII.GetBytes('PLAYTIMELY-CONN1'));$stream.Dispose()
$install=Wait-InstallerProcess (Start-Process $personal -ArgumentList @('--install-bundled') -PassThru)
if($install.ExitCode -ne 0){throw 'Installer failed'}
try {
 if((Get-Content $sentinel -Raw).Trim() -ne 'untouched-install-test'){throw 'Installer followed a pre-existing hard link'}
 $users='S-1-5-32-545'
 foreach($path in @($privateDir,"$privateDir/connection.json")){
  $leak=(Get-Acl $path).Access|Where-Object {$_.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value -eq $users}
  if($leak){throw 'Private installation retained explicit Users permissions'}
 }
 'PASS: pre-existing broad ACL removed; hard-linked target was not overwritten'|Set-Content qa-artifacts/windows-install-security.txt
 Stop-Service GameControlAgent
 $policy=@{schema_version=1;device_id=99;issued_at=[DateTimeOffset]::UtcNow.ToString('o');timezone='Europe/Stockholm';poll_seconds=60;rules=@(@{app='roblox';processes=@('RobloxPlayerBeta.exe');mode='blocked';start='18:00';end='21:00';days=@(1,2,3,4,5,6,7)},@{app='minecraft';processes=@('Minecraft.exe');mode='schedule';start='00:00';end='23:59';days=@(1,2,3,4,5,6,7)},@{app='game-1113966530531704943';processes=@('diablo iv.exe');mode='blocked';start='18:00';end='21:00';days=@(1,2,3,4,5,6,7)})}
 $bytes=[Text.Encoding]::UTF8.GetBytes(($policy|ConvertTo-Json -Depth 8 -Compress))
 $sig=$rsa.SignData($bytes,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
 @{payload=[Convert]::ToBase64String($bytes);signature=[Convert]::ToBase64String($sig);algorithm='RS256'} | ConvertTo-Json | Set-Content "$env:ProgramData/GameControl/policy.json"
 Start-Service GameControlAgent
 $statusFile="$env:ProgramData/PlaytimelyStatus/status.json"
 $deadline=(Get-Date).AddSeconds(45)
 do {Start-Sleep -Seconds 1;if(Test-Path $statusFile){$s=Get-Content $statusFile -Raw|ConvertFrom-Json;if($s.has_policy){break}}}while((Get-Date)-lt $deadline)
 if(!$s.has_policy -or $s.rules.Count -ne 3){throw 'Signed cached policy not projected to tray status'}
 if((Get-Content $statusFile -Raw)-match 'token|public_key|processes|policy_url'){throw 'Secret/private fields in status'}
 # A slow server must not stall the two-second status/enforcement loop.
 $probe="$env:RUNNER_TEMP/RobloxPlayerBeta.exe"
 Copy-Item "$env:WINDIR/System32/ping.exe" $probe
 for($n=0;$n -lt 12;$n++) {
   Start-Sleep -Seconds 2
   $state=Get-Content $statusFile -Raw|ConvertFrom-Json
   if(([DateTimeOffset]::UtcNow-[DateTimeOffset]::Parse($state.updated_at)).TotalSeconds -gt 6){throw 'Network fetch stalled the local status heartbeat'}
 }
 $closeTimer=[Diagnostics.Stopwatch]::StartNew()
 $game=Start-Process $probe -ArgumentList @('-t','127.0.0.1') -PassThru -WindowStyle Hidden
 if(!$game.WaitForExit(70000)){Stop-Process -Id $game.Id;throw 'Blocked process survived while network was stalled'}
 if($closeTimer.Elapsed.TotalSeconds -lt 29){throw 'Forced termination happened before the graceful-close deadline'}
 'PASS: stalled TLS fetch does not stall status; harmless process with controlled game filename terminated after the bounded 30-second graceful-close window'|Set-Content qa-artifacts/windows-network.txt

 Stop-Service GameControlAgent
 $originalBudgetRule=$policy.rules[1].Clone()
 $policy.rules[1].app='game-1440143395619344515';$policy.rules[1].processes=@('ridersrepublic.exe')
 $policy.daily_budget=@{weekday_minutes=15;weekend_minutes=15}
 $policy.rules[1].start='00:00';$policy.rules[1].end='23:59';$policy.rules[1].mode='allowed' # No schedule must still consume and exhaust the selected budget.
 $bytes=[Text.Encoding]::UTF8.GetBytes(($policy|ConvertTo-Json -Depth 8 -Compress))
 $sig=$rsa.SignData($bytes,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
 @{payload=[Convert]::ToBase64String($bytes);signature=[Convert]::ToBase64String($sig);algorithm='RS256'} | ConvertTo-Json | Set-Content "$privateDir/policy.json"
 $localDay=[TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTimeOffset]::UtcNow,'Europe/Stockholm').ToString('yyyy-MM-dd')
 @{Day=$localDay;UsedSeconds=896} | ConvertTo-Json | Set-Content "$privateDir/daily-budget.json"
 $budgetProbe="$env:RUNNER_TEMP/RidersRepublic.exe"
 Copy-Item "$env:WINDIR/System32/ping.exe" $budgetProbe
 Start-Service GameControlAgent
 $budgetGame=Start-Process $budgetProbe -ArgumentList @('-t','127.0.0.1') -PassThru -WindowStyle Hidden
 $deadline=(Get-Date).AddSeconds(50)
 do {Start-Sleep -Seconds 1;$bs=Get-Content $statusFile -Raw|ConvertFrom-Json}while($bs.budget_remaining_seconds -ne 0 -and (Get-Date)-lt $deadline)
 if($bs.budget_remaining_seconds -ne 0){throw 'Daily budget did not consume running game time'}
 if(!($bs.running_games | Where-Object {$_.app -eq 'game-1440143395619344515' -and $_.uses_budget})){throw 'Running budget game not reported'}
 if(!$budgetGame.WaitForExit(36000)){Stop-Process -Id $budgetGame.Id;throw 'Exhausted budget did not stop fixture'}
 Restart-Service GameControlAgent
 Start-Sleep -Seconds 3
 $bs=Get-Content $statusFile -Raw|ConvertFrom-Json
 if($bs.budget_remaining_seconds -ne 0){throw 'Restart refilled daily budget'}
 if($bs.running_games | Where-Object {$_.app -eq 'game-1440143395619344515'}){throw 'Exited game still reported as running'}
 'PASS: actual service counts harmless RidersRepublic.exe fixture, enforces exhausted budget and preserves consumption after restart'|Set-Content qa-artifacts/windows-budget.txt

 $policy.rules[1]=$originalBudgetRule
 $policy.rules[1].mode='allowed'
 $budgetProbe="$env:RUNNER_TEMP/Minecraft.exe"
 Copy-Item "$env:WINDIR/System32/ping.exe" $budgetProbe

 # End-action fixtures use only our harmless ping executable under a catalog filename.
 function Save-TestBudgetPolicy($action) {
  $policy.daily_budget=@{weekday_minutes=15;weekend_minutes=15;scope='all_known';action=$action}
  $policy.rules=@()
  $bytes=[Text.Encoding]::UTF8.GetBytes(($policy|ConvertTo-Json -Depth 8 -Compress))
  $sig=$rsa.SignData($bytes,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
  @{payload=[Convert]::ToBase64String($bytes);signature=[Convert]::ToBase64String($sig);algorithm='RS256'} | ConvertTo-Json | Set-Content "$privateDir/policy.json"
 }
 Stop-Service GameControlAgent
 Save-TestBudgetPolicy 'finish'
 @{Day=$localDay;UsedSeconds=896}|ConvertTo-Json|Set-Content "$privateDir/daily-budget.json"
 $continuing=Start-Process $budgetProbe -ArgumentList @('-t','127.0.0.1') -PassThru -WindowStyle Hidden
 Start-Service GameControlAgent
 $deadline=(Get-Date).AddSeconds(45)
 do {Start-Sleep -Seconds 1;$bs=Get-Content $statusFile -Raw|ConvertFrom-Json}while(!$bs.budget_exhausted_at -and (Get-Date)-lt $deadline)
 if(!$bs.budget_exhausted_at -or $bs.budget_action -ne 'finish'){throw 'Finish action did not activate'}
 if($continuing.HasExited){throw 'Finish action stopped existing session'}
 $newGame=Start-Process $budgetProbe -ArgumentList @('-t','127.0.0.1') -PassThru -WindowStyle Hidden
 if(!$newGame.WaitForExit(8000)){Stop-Process -Id $newGame.Id;throw 'Finish action allowed a new session'}
 Restart-Service GameControlAgent
 Start-Sleep -Seconds 8
 if($continuing.HasExited){throw 'Restart lost continuing session identity'}
 Stop-Process -Id $continuing.Id
 $newGame=Start-Process $budgetProbe -ArgumentList @('-t','127.0.0.1') -PassThru -WindowStyle Hidden
 if(!$newGame.WaitForExit(8000)){Stop-Process -Id $newGame.Id;throw 'A stopped game could restart after budget exhaustion'}
 Start-Sleep -Seconds 3
 $noticeStatus=Get-Content $statusFile -Raw|ConvertFrom-Json
 if(!($noticeStatus.notices | Where-Object {$_.reason -eq 'budget' -and $_.outcome -eq 'blocked'})){throw 'Immediate blocked-start explanation absent'}

 Stop-Service GameControlAgent
 Save-TestBudgetPolicy 'notify'
 @{Day=$localDay;UsedSeconds=900}|ConvertTo-Json|Set-Content "$privateDir/daily-budget.json"
 $noticeGame=Start-Process $budgetProbe -ArgumentList @('-t','127.0.0.1') -PassThru -WindowStyle Hidden
 Start-Service GameControlAgent
 Start-Sleep -Seconds 36
 if($noticeGame.HasExited){throw 'Reminder-only action stopped a game'}
 $bs=Get-Content $statusFile -Raw|ConvertFrom-Json
 if($bs.budget_action -ne 'notify' -or !$bs.budget_exhausted_at){throw 'Reminder notice not exposed'}
 Stop-Service GameControlAgent
 Stop-Process -Id $noticeGame.Id
 Save-TestBudgetPolicy 'grace'
 # A persisted grace begun 550 seconds ago must not restart after a service restart.
 $began=[DateTimeOffset]::UtcNow.AddSeconds(-550).ToString('o')
 @{Day=$localDay;UsedSeconds=900;Exhaustion=@{Day=$localDay;Action='grace';BeganAt=$began;LatestAt=$began;Continuing=@()}}|ConvertTo-Json -Depth 5|Set-Content "$privateDir/daily-budget.json"
 $graceGame=Start-Process $budgetProbe -ArgumentList @('-t','127.0.0.1') -PassThru -WindowStyle Hidden
 Start-Service GameControlAgent
 Start-Sleep -Seconds 8
 if($graceGame.HasExited){throw 'Ten-minute grace closed a game too early'}
 $bs=Get-Content $statusFile -Raw|ConvertFrom-Json
 if(!$bs.budget_grace_ends_at){throw 'Grace deadline not projected'}
 if(!$graceGame.WaitForExit(80000)){Stop-Process -Id $graceGame.Id;throw 'Persisted ten-minute grace was extended'}
 Start-Sleep -Seconds 3
 $noticeStatus=Get-Content $statusFile -Raw|ConvertFrom-Json
 if(!($noticeStatus.notices | Where-Object {$_.reason -eq 'budget_grace' -and $_.outcome -eq 'closed'})){throw 'Grace close explanation absent from live service status'}
 if($noticeStatus.budget_limit_minutes -ne 15 -or $noticeStatus.budget_used_seconds -lt 900){throw 'Measured summary missing from live service status'}
 'PASS: finish preserves existing process through service restart, blocks new identities; reminder allows play; persisted ten-minute grace expires and enforces'|Set-Content qa-artifacts/windows-budget-actions.txt
 # Protocol10: parent stop is independent of budgets and extra game permissions.
 function Save-TestParentStop([string]$action) {
  $policy.issued_at=[DateTimeOffset]::UtcNow.ToString('o')
  $policy.daily_budget=$null
  $policy.rules=@(@{app='all_known';processes=@();mode='allowed';start='13:00';end='21:00';days=@(1,2,3,4,5)})
  $policy.game_stop=@{id=[guid]::NewGuid().ToString();action=$action;starts_at=[DateTimeOffset]::UtcNow.AddSeconds(-1).ToString('o');ends_at=[DateTimeOffset]::UtcNow.AddMinutes(30).ToString('o')}
  $bytes=[Text.Encoding]::UTF8.GetBytes(($policy|ConvertTo-Json -Depth 8 -Compress));$sig=$rsa.SignData($bytes,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
  @{payload=[Convert]::ToBase64String($bytes);signature=[Convert]::ToBase64String($sig);algorithm='RS256'}|ConvertTo-Json|Set-Content "$privateDir/policy.json"
 }
 Stop-Service GameControlAgent
 Save-TestParentStop 'finish'
 @{Day=$localDay;UsedSeconds=0}|ConvertTo-Json|Set-Content "$privateDir/daily-budget.json"
 $continuing=Start-Process $budgetProbe -ArgumentList @('-t','127.0.0.1') -PassThru -WindowStyle Hidden
 Start-Service GameControlAgent;Start-Sleep -Seconds 8
 if($continuing.HasExited){throw 'Parent finish stopped ongoing game'}
 $freshGame=Start-Process $budgetProbe -ArgumentList @('-t','127.0.0.1') -PassThru -WindowStyle Hidden
 if(!$freshGame.WaitForExit(8000)){Stop-Process -Id $freshGame.Id;throw 'Parent finish allowed a new game'}
 Restart-Service GameControlAgent;Start-Sleep -Seconds 8
 if($continuing.HasExited){throw 'Restart lost parent-stop continuing identity'}
 Stop-Service GameControlAgent;Stop-Process -Id $continuing.Id
 Save-TestParentStop 'notify'
 $noticeGame=Start-Process $budgetProbe -ArgumentList @('-t','127.0.0.1') -PassThru -WindowStyle Hidden
 Start-Service GameControlAgent;Start-Sleep -Seconds 8
 if($noticeGame.HasExited){throw 'Parent reminder killed game'}
 Stop-Service GameControlAgent;Stop-Process -Id $noticeGame.Id
 Save-TestParentStop 'stop'
 Start-Service GameControlAgent;Start-Sleep -Seconds 3
 $immediate=Start-Process $budgetProbe -ArgumentList @('-t','127.0.0.1') -PassThru -WindowStyle Hidden
 if(!$immediate.WaitForExit(8000)){Stop-Process -Id $immediate.Id;throw 'Confirmed parent hard stop did not enforce immediately'}
 Stop-Service GameControlAgent
 Save-TestParentStop 'grace'
 $began=[DateTimeOffset]::UtcNow.AddSeconds(-560).ToString('o')
 @{Day=$localDay;UsedSeconds=0;ManualStop=@{Day=$policy.game_stop.id;Action='grace';BeganAt=$began;LatestAt=$began;Continuing=@()}}|ConvertTo-Json -Depth 6|Set-Content "$privateDir/daily-budget.json"
 $graceGame=Start-Process $budgetProbe -ArgumentList @('-t','127.0.0.1') -PassThru -WindowStyle Hidden
 Start-Service GameControlAgent;Start-Sleep -Seconds 8
 if($graceGame.HasExited){throw 'Parent grace stopped before save period'}
 if(!$graceGame.WaitForExit(80000)){Stop-Process -Id $graceGame.Id;throw 'Parent grace failed to expire across restart'}
 Stop-Service GameControlAgent
 # Cancellation received and cached: new games are allowed again, offline.
 $policy.game_stop=$null
 $policy.issued_at=[DateTimeOffset]::UtcNow.ToString('o')
 $bytes=[Text.Encoding]::UTF8.GetBytes(($policy|ConvertTo-Json -Depth 8 -Compress));$sig=$rsa.SignData($bytes,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
 @{payload=[Convert]::ToBase64String($bytes);signature=[Convert]::ToBase64String($sig);algorithm='RS256'}|ConvertTo-Json|Set-Content "$privateDir/policy.json"
 $resumed=Start-Process $budgetProbe -ArgumentList @('-t','127.0.0.1') -PassThru -WindowStyle Hidden
 Start-Service GameControlAgent;Start-Sleep -Seconds 8
 if($resumed.HasExited){throw 'Cancel parent stop did not restore normal rules'}
 Stop-Service GameControlAgent;Stop-Process -Id $resumed.Id
 'PASS: parent finish/restart/new-process rejection, reminder-only, immediate stop, persisted grace and cancellation without a daily budget'|Set-Content qa-artifacts/windows-parent-stop.txt
 # Shared rule must stop recognized games even without a budget or individual rule.
 Stop-Service GameControlAgent
 $policy.daily_budget=$null
 $policy.rules=@(@{app='all_known';processes=@();mode='blocked';start='13:00';end='21:00';days=@(1,2,3,4,5)})
 $bytes=[Text.Encoding]::UTF8.GetBytes(($policy|ConvertTo-Json -Depth 8 -Compress))
 $sig=$rsa.SignData($bytes,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
 @{payload=[Convert]::ToBase64String($bytes);signature=[Convert]::ToBase64String($sig);algorithm='RS256'}|ConvertTo-Json|Set-Content "$privateDir/policy.json"
 $sharedGame=Start-Process $budgetProbe -ArgumentList @('-t','127.0.0.1') -PassThru -WindowStyle Hidden
 Start-Service GameControlAgent
 if(!$sharedGame.WaitForExit(45000)){Stop-Process -Id $sharedGame.Id;throw 'Shared rule failed to stop recognized game'}
 'PASS: shared all-known rule enforces without daily budget or individual game rule'|Set-Content qa-artifacts/windows-shared-rule.txt
 $policy.daily_budget=@{weekday_minutes=15;weekend_minutes=15;scope='all_known'}


 # Global budget must detect and stop a catalog game with no per-game rules.
 Stop-Service GameControlAgent
 $policy.daily_budget.scope='all_known';$policy.rules=@()
 $bytes=[Text.Encoding]::UTF8.GetBytes(($policy|ConvertTo-Json -Depth 8 -Compress))
 $sig=$rsa.SignData($bytes,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
 @{payload=[Convert]::ToBase64String($bytes);signature=[Convert]::ToBase64String($sig);algorithm='RS256'} | ConvertTo-Json | Set-Content "$privateDir/policy.json"
 @{Day=$localDay;UsedSeconds=896} | ConvertTo-Json | Set-Content "$privateDir/daily-budget.json"
 Start-Service GameControlAgent
 $globalGame=Start-Process $budgetProbe -ArgumentList @('-t','127.0.0.1') -PassThru -WindowStyle Hidden
 $deadline=(Get-Date).AddSeconds(50)
 do {Start-Sleep -Seconds 1;$bs=Get-Content $statusFile -Raw|ConvertFrom-Json}while(($bs.budget_remaining_seconds -ne 0 -or $bs.budget_scope -ne 'all_known') -and (Get-Date)-lt $deadline)
 if($bs.budget_remaining_seconds -ne 0 -or $bs.budget_scope -ne 'all_known'){throw 'Global budget failed without game rules'}
 if(!($bs.running_games | Where-Object {$_.app -eq 'minecraft' -and $_.uses_budget})){throw 'Unconfigured running game not detected'}
 if(!$globalGame.WaitForExit(36000)){Stop-Process -Id $globalGame.Id;throw 'Global budget failed to stop unconfigured game'}
 'PASS: no game rules, global catalog detection, time consumption, running-game status and exhausted-budget enforcement'|Set-Content qa-artifacts/windows-global-budget.txt
 # Measure the actual installed service, not just a matching microbenchmark.
 Stop-Service GameControlAgent
 @{Day=$localDay;UsedSeconds=0} | ConvertTo-Json | Set-Content "$privateDir/daily-budget.json"
 Start-Service GameControlAgent
 Start-Sleep -Seconds 5
 # Installation already opens the single-instance tray. A second launcher can
 # exit immediately; measure the live installed desktop process, not that stub.
 $trayPath=(Resolve-Path "$env:ProgramFiles/GameControl/Playtimely.Tray.exe").Path
 Start-Process $trayPath | Out-Null
 Start-Sleep -Seconds 1
 $desktopSession=(Get-Process -Id $PID).SessionId
 $perfTray=Get-Process Playtimely.Tray | Where-Object { $_.SessionId -eq $desktopSession -and $_.MainModule.FileName -eq $trayPath } | Select-Object -First 1
 if(!$perfTray -or $perfTray.HasExited){throw 'No live installed desktop tray for performance measurement'}
 function Measure-Agent([string]$scenario) {
  $servicePid=(Get-CimInstance Win32_Service -Filter "Name='GameControlAgent'").ProcessId
  $p=Get-Process -Id $servicePid
  $cpuStart=$p.TotalProcessorTime.TotalMilliseconds
  $perfTray.Refresh();$trayCpuStart=$perfTray.TotalProcessorTime.TotalMilliseconds;$trayPeakWorking=0;$trayPeakPrivate=0
  $historyWrites=0;$historyStamp=(Get-Item "$privateDir/usage-history.json").LastWriteTimeUtc
  $timer=[Diagnostics.Stopwatch]::StartNew();$peakWorking=0;$peakPrivate=0;$peakHandles=0
  for($i=0;$i -lt 30;$i++) {
   Start-Sleep -Seconds 2;$p.Refresh();$perfTray.Refresh();if($perfTray.HasExited){throw 'Tray exited during resource measurement'}
   $updated=(Get-Item "$privateDir/usage-history.json").LastWriteTimeUtc;if($updated -ne $historyStamp){$historyWrites++;$historyStamp=$updated}
   $trayPeakWorking=[Math]::Max($trayPeakWorking,$perfTray.WorkingSet64);$trayPeakPrivate=[Math]::Max($trayPeakPrivate,$perfTray.PrivateMemorySize64)
   $peakWorking=[Math]::Max($peakWorking,$p.WorkingSet64)
   $peakPrivate=[Math]::Max($peakPrivate,$p.PrivateMemorySize64)
   $peakHandles=[Math]::Max($peakHandles,$p.HandleCount)
  }
  $cpuMs=$p.TotalProcessorTime.TotalMilliseconds-$cpuStart
  $oneCorePercent=100*$cpuMs/$timer.Elapsed.TotalMilliseconds
  "$scenario seconds=$([Math]::Round($timer.Elapsed.TotalSeconds,1)) cpu_ms=$cpuMs cpu_percent_one_core=$([Math]::Round($oneCorePercent,3)) peak_working_MB=$([Math]::Round($peakWorking/1MB,1)) peak_private_MB=$([Math]::Round($peakPrivate/1MB,1)) peak_handles=$peakHandles logical_cpus=$env:NUMBER_OF_PROCESSORS" | Add-Content qa-artifacts/windows-performance.txt
  $trayPercent=100*($perfTray.TotalProcessorTime.TotalMilliseconds-$trayCpuStart)/$timer.Elapsed.TotalMilliseconds
  "$scenario tray_cpu_percent_one_core=$([Math]::Round($trayPercent,3)) tray_peak_working_MB=$([Math]::Round($trayPeakWorking/1MB,1)) tray_peak_private_MB=$([Math]::Round($trayPeakPrivate/1MB,1))" | Add-Content qa-artifacts/windows-performance.txt
  "$scenario history_writes=$historyWrites"|Add-Content qa-artifacts/windows-performance.txt
  if($historyWrites -gt 6){throw "History write batching regressed: $historyWrites writes"}
  if($trayPercent -gt 5 -or $trayPeakPrivate -gt 250MB){throw 'Tray resource regression'}
  if($oneCorePercent -gt 5 -or $peakPrivate -gt 250MB){throw 'Service resource regression: over 5% of one core or 250MB private memory'}
 }
 Measure-Agent 'all_known_idle'
 $perfGame=Start-Process $budgetProbe -ArgumentList @('-t','127.0.0.1') -PassThru -WindowStyle Hidden
 try {Measure-Agent 'all_known_running_fixture'} finally {Stop-Process -Id $perfGame.Id -ErrorAction SilentlyContinue}
 Start-Sleep -Seconds 4
 $bs=Get-Content $statusFile -Raw|ConvertFrom-Json
 if($bs.running_games | Where-Object {$_.app -eq 'minecraft'}){throw 'Exited PID was retained until the next full discovery scan'}
 # Measure reminder-only exhaustion, which must not trigger rapid full discovery.
 Stop-Service GameControlAgent
 $policy.daily_budget.action='notify'
 $bytes=[Text.Encoding]::UTF8.GetBytes(($policy|ConvertTo-Json -Depth 8 -Compress))
 $sig=$rsa.SignData($bytes,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
 @{payload=[Convert]::ToBase64String($bytes);signature=[Convert]::ToBase64String($sig);algorithm='RS256'}|ConvertTo-Json|Set-Content "$privateDir/policy.json"
 @{Day=$localDay;UsedSeconds=86400}|ConvertTo-Json|Set-Content "$privateDir/daily-budget.json"
 Start-Service GameControlAgent
 Start-Sleep -Seconds 4
 $perfGame=Start-Process $budgetProbe -ArgumentList @('-t','127.0.0.1') -PassThru -WindowStyle Hidden
 try {Measure-Agent 'exhausted_notify';if($perfGame.HasExited){throw 'Reminder-only killed a game'}}finally{Stop-Process -Id $perfGame.Id -ErrorAction SilentlyContinue}
 Stop-Process -Id $perfTray.Id -ErrorAction SilentlyContinue
 # Signed checklist gate, independent of schedules/budget/temporary allowances.
 Stop-Service GameControlAgent
 $choreId=[guid]::NewGuid().ToString();$today=[TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTimeOffset]::UtcNow,'Europe/Stockholm');$weekday=[int]$today.DayOfWeek;if(!$weekday){$weekday=7}
 $policy.daily_budget=$null;$policy.rules=@();$policy.chores=@{revision=1;completed_day=$today.ToString('yyyy-MM-dd');completed_ids=@();tasks=@(@{id=$choreId;day=$weekday;label='Finish homework'})}
 function Save-ChecklistPolicy {
  $policy.issued_at=[DateTimeOffset]::UtcNow.ToString('o');$raw=[Text.Encoding]::UTF8.GetBytes(($policy|ConvertTo-Json -Depth 12 -Compress));$signature=$rsa.SignData($raw,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
  @{payload=[Convert]::ToBase64String($raw);signature=[Convert]::ToBase64String($signature);algorithm='RS256'}|ConvertTo-Json|Set-Content "$privateDir/policy.json"
 }
 Save-ChecklistPolicy;Start-Service GameControlAgent;Start-Sleep -Seconds 4
 $probe=Start-Process $budgetProbe -ArgumentList @('-t','127.0.0.1') -PassThru -WindowStyle Hidden
 if(!$probe.WaitForExit(40000)){Stop-Process -Id $probe.Id -Force;throw 'Unfinished checklist did not block an unscheduled known game'}
 Stop-Service GameControlAgent;$policy.chores.completed_ids=@($choreId);Save-ChecklistPolicy;Start-Service GameControlAgent;Start-Sleep -Seconds 4
 $probe=Start-Process $budgetProbe -ArgumentList @('-t','127.0.0.1') -PassThru -WindowStyle Hidden
 try {Start-Sleep -Seconds 8;if($probe.HasExited){throw 'Completed checklist blocked an otherwise unrestricted game'}}finally{Stop-Process -Id $probe.Id -ErrorAction SilentlyContinue}
 Stop-Service GameControlAgent;$policy.chores.completed_day=$today.AddDays(-1).ToString('yyyy-MM-dd');Save-ChecklistPolicy;Start-Service GameControlAgent;Start-Sleep -Seconds 4
 $probe=Start-Process $budgetProbe -ArgumentList @('-t','127.0.0.1') -PassThru -WindowStyle Hidden
 if(!$probe.WaitForExit(40000)){Stop-Process -Id $probe.Id -Force;throw 'Previous-day checkmarks bypassed the checklist'}
 $policy.Remove('chores')
 'PASS: signed checklist blocks unscheduled known game; completion allows it; previous-day marks cannot unlock it'|Set-Content qa-artifacts/windows-chores-enforcement.txt
 # Restore the normal rules for downstream UI/upgrade fixtures.
 Stop-Service GameControlAgent
 $policy.daily_budget=$null
 $policy.rules=@(@{app='roblox';processes=@('RobloxPlayerBeta.exe');mode='blocked';start='18:00';end='21:00';days=@(1,2,3,4,5,6,7)})
 $bytes=[Text.Encoding]::UTF8.GetBytes(($policy|ConvertTo-Json -Depth 8 -Compress))
 $sig=$rsa.SignData($bytes,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
 @{payload=[Convert]::ToBase64String($bytes);signature=[Convert]::ToBase64String($sig);algorithm='RS256'} | ConvertTo-Json | Set-Content "$privateDir/policy.json"
 Start-Service GameControlAgent
 Start-Sleep -Seconds 3

 $sid=[Security.Principal.SecurityIdentifier]::new('S-1-5-32-545')
 foreach($dir in @("$env:ProgramFiles/GameControl","$env:ProgramData/PlaytimelyStatus")) {
   $rights=(Get-Acl $dir).Access|Where-Object {$_.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value -eq $sid.Value}
   if(!$rights){throw "Missing users read access: $dir"}
   if($rights|Where-Object {($_.FileSystemRights -band [Security.AccessControl.FileSystemRights]::Write) -ne 0}){throw "Users can write: $dir"}
 }
 $private=(Get-Acl "$env:ProgramData/GameControl/connection.json").Access|Where-Object {$_.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value -eq $sid.Value}
 if($private){throw 'Users can access connection credentials'}
 if(!(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run').Playtimely){throw 'Missing autostart'}
 if(!(Test-Path "$env:ProgramData/Microsoft/Windows/Start Menu/Programs/Playtimely.lnk")){throw 'Missing shortcut'}
 # The local request channel accepts only the fixed request shape, never commands.
 $pipe=[IO.Pipes.NamedPipeClientStream]::new('.','Playtimely.TimeRequests.v1',[IO.Pipes.PipeDirection]::InOut)
 $pipe.Connect(5000)
 $writer=[IO.StreamWriter]::new($pipe,[Text.UTF8Encoding]::new($false),512,$true);$writer.AutoFlush=$true
 $reader=[IO.StreamReader]::new($pipe)
 $writer.WriteLine('{"app":"explorer","option":"999","reason":"run command"}')
 if($reader.ReadLine() -ne 'invalid'){throw 'Request pipe accepted unsupported request'}
 $writer.Dispose();$reader.Dispose();$pipe.Dispose()
 # Exercise the same launch helper with an ordinary user's process token, even
 # on hosted runners whose own desktop is an elevated automation account.
 $qaPassword=ConvertTo-SecureString ('Qa!'+[guid]::NewGuid().ToString('N')) -AsPlainText -Force
 New-LocalUser -Name PlaytimelyLaunchQA -Password $qaPassword | Out-Null
 $credential=[pscredential]::new("$env:COMPUTERNAME\PlaytimelyLaunchQA",$qaPassword)
 $userShell=Start-Process "$env:WINDIR/System32/cmd.exe" -ArgumentList '/c ping -n 1200 127.0.0.1 > nul' -Credential $credential -LoadUserProfile -PassThru
 $env:PLAYTIMELY_QA_SHELL_PID=[string]$userShell.Id
 try {
  dotnet run --project agent-tray-qa/Playtimely.Tray.QA.csproj -c Release
  if($LASTEXITCODE){throw "Localized tray UI checks failed"}
 } finally {
  Stop-Process -Id $userShell.Id -Force -ErrorAction SilentlyContinue
  Remove-LocalUser PlaytimelyLaunchQA
 }
 Get-Process Playtimely.Tray -ErrorAction SilentlyContinue | Stop-Process -Force
 $tray=Start-Process "$env:ProgramFiles/GameControl/Playtimely.Tray.exe" -PassThru
 Start-Sleep -Seconds 5
 if($tray.HasExited){throw 'Tray exited unexpectedly'}
 Stop-Process -Id $tray.Id
 if((Get-Service GameControlAgent).Status -ne 'Running'){throw 'Closing tray stopped enforcement'}
 $before=(Get-FileHash "$privateDir/policy.json").Hash
 $setupGate=[Threading.Mutex]::new($false,'Global\Playtimely.Setup')
 if(!$setupGate.WaitOne(0)){throw 'Unexpected pre-existing setup gate'}
 try {
  $duplicate=Wait-InstallerProcess (Start-Process $setup -ArgumentList @('--upgrade') -PassThru)
  if($duplicate.ExitCode -ne 1618){throw 'Concurrent installer was not rejected'}
 } finally {$setupGate.ReleaseMutex();$setupGate.Dispose()}
 $upgrade=Wait-InstallerProcess (Start-Process $setup -RedirectStandardError qa-artifacts/windows-upgrade-error.txt -RedirectStandardOutput qa-artifacts/windows-upgrade-output.txt -ArgumentList @('--upgrade') -PassThru)
 if($upgrade.ExitCode -ne 0){Get-Content qa-artifacts/windows-upgrade-error.txt;Get-Content qa-artifacts/windows-upgrade-output.txt;throw 'Upgrade failed'}
 if((Get-FileHash "$privateDir/policy.json").Hash -ne $before){throw 'Upgrade lost offline rules'}
 if((Get-Service GameControlAgent).Status -ne 'Running'){throw 'Upgrade did not restart service'}
 # Reproduce retained program/data files with no Windows service (error 1060).
 # Full uninstall also removes Start-menu/logon registration; repair must restore it.
 $repairConnection=(Get-FileHash "$privateDir/connection.json").Hash
 foreach($repairCase in @(@{File=$personal;Args='--install-bundled'},@{File=$setup;Args='--upgrade'})) {
  $removed=Wait-InstallerProcess (Start-Process $setup -ArgumentList '--uninstall' -PassThru)
  if($removed.ExitCode -ne 0){throw 'Repair fixture uninstall failed'}
  $until=(Get-Date).AddSeconds(30)
  while((Get-Service GameControlAgent -ErrorAction SilentlyContinue) -and (Get-Date)-lt $until){Start-Sleep -Milliseconds 500}
  if(Get-Service GameControlAgent -ErrorAction SilentlyContinue){throw 'Repair fixture service still exists'}
  $repaired=Wait-InstallerProcess (Start-Process $repairCase.File -ArgumentList $repairCase.Args -RedirectStandardError qa-artifacts/windows-repair-error.txt -PassThru)
  if($repaired.ExitCode -ne 0){Get-Content qa-artifacts/windows-repair-error.txt;throw 'Missing-service repair failed'}
  if((Get-Service GameControlAgent).Status -ne 'Running'){throw 'Repair did not start the recreated service'}
  if((Get-FileHash "$privateDir/connection.json").Hash -ne $repairConnection -or (Get-FileHash "$privateDir/policy.json").Hash -ne $before){throw 'Repair changed saved connection or offline rules'}
  if(!(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run').Playtimely){throw 'Repair did not restore logon startup'}
  if(!(Test-Path "$env:ProgramData/Microsoft/Windows/Start Menu/Programs/Playtimely.lnk")){throw 'Repair did not restore Start-menu shortcut'}
 }
 'PASS: personal installer and generic upgrade recreate missing service, preserve connection/offline rules and restore tray registration'|Set-Content qa-artifacts/windows-service-repair.txt
 # Exercise the same silent command under SYSTEM, as launched by the service.
 $connectionBefore=(Get-FileHash "$privateDir/connection.json").Hash
 $binaryBefore=(Get-FileHash "$env:ProgramFiles/GameControl/GameControl.Agent.exe").Hash
 $taskName='Playtimely-QA-Update'
 $autoRunner=Join-Path $env:RUNNER_TEMP 'pt-auto-upgrade.ps1'
 @"
`$p=Start-Process '$setup' -ArgumentList '--auto-upgrade' -RedirectStandardError '$PWD/qa-artifacts/windows-auto-error.txt' -PassThru
`$null=`$p.Handle
if(!`$p.WaitForExit(120000)){exit 1460}
exit `$p.ExitCode
"@|Set-Content $autoRunner
 $action=New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$autoRunner`""
 Register-ScheduledTask -TaskName $taskName -Action $action -User SYSTEM -RunLevel Highest -Force|Out-Null
 try {
  # A game launched since the last discovery must defer installation without stopping service.
  $serviceBefore=(Get-CimInstance Win32_Service -Filter "Name='GameControlAgent'").ProcessId
  $activeGame=Start-Process $budgetProbe -ArgumentList @('-t','127.0.0.1') -PassThru -WindowStyle Hidden
  try {
   Start-ScheduledTask $taskName;$until=(Get-Date).AddSeconds(120)
   do {Start-Sleep 2;$task=Get-ScheduledTask $taskName;$info=Get-ScheduledTaskInfo $taskName}while(($task.State -eq 'Running' -or $info.LastRunTime.Year -lt 2020) -and (Get-Date)-lt $until)
   if($task.State -eq 'Running' -or $info.LastTaskResult -eq 0){throw 'Update must defer while a possible game is running'}
   if((Get-CimInstance Win32_Service -Filter "Name='GameControlAgent'").ProcessId -ne $serviceBefore -or $activeGame.HasExited){throw 'Deferred update interrupted enforcement/game'}
   'PASS: game activity defers SYSTEM update without service restart'|Set-Content qa-artifacts/windows-update-idle.txt
  }finally{Stop-Process -Id $activeGame.Id -ErrorAction SilentlyContinue}
  Start-Sleep -Seconds 4
  $previous=$info.LastRunTime
  Start-ScheduledTask $taskName
  $until=(Get-Date).AddSeconds(120)
  do {Start-Sleep 2;$task=Get-ScheduledTask $taskName;$info=Get-ScheduledTaskInfo $taskName}while(($task.State -eq 'Running' -or $info.LastRunTime -eq $previous) -and (Get-Date)-lt $until)
  if($task.State -eq 'Running' -or $info.LastTaskResult -ne 0){Get-Content qa-artifacts/windows-auto-error.txt;Get-Content $statusFile;throw "Automatic SYSTEM upgrade failed: $($info.LastTaskResult)"}
  if((Get-Service GameControlAgent).Status -ne 'Running'){throw 'Automatic upgrade stopped enforcement'}
  if((Get-FileHash "$privateDir/connection.json").Hash -ne $connectionBefore -or (Get-FileHash "$privateDir/policy.json").Hash -ne $before){throw 'Automatic upgrade changed connection or cached rules'}
  # Lock the installed tray to simulate security software/file contention. Extraction/backup failure must not stop service.
  # Running executable images require read sharing; deny write/delete to reproduce replacement contention.
  $lock=[IO.File]::Open("$env:ProgramFiles/GameControl/Playtimely.Tray.exe",'Open','Read','Read')
  try {
   $previous=$info.LastRunTime;Start-ScheduledTask $taskName;$until=(Get-Date).AddSeconds(120)
   do {Start-Sleep 2;$task=Get-ScheduledTask $taskName;$info=Get-ScheduledTaskInfo $taskName}while(($task.State -eq 'Running' -or $info.LastRunTime -eq $previous) -and (Get-Date)-lt $until)
   if($task.State -eq 'Running' -or $info.LastTaskResult -eq 0){throw 'Expected update failure with locked binary'}
   if((Get-Service GameControlAgent).Status -ne 'Running' -or (Get-FileHash "$env:ProgramFiles/GameControl/GameControl.Agent.exe").Hash -ne $binaryBefore){throw 'Failed automatic update damaged working service'}
  }finally{$lock.Dispose()}
  # Fail AFTER swapping binaries: the candidate cannot start as a Windows service.
  # Only this isolated installer contains the broken test payload; it is never published.
  $broken=Join-Path $env:RUNNER_TEMP 'broken-setup/GameControl.Setup.exe'
  $action=New-ScheduledTaskAction -Execute $broken -Argument '--auto-upgrade'
  Set-ScheduledTask -TaskName $taskName -Action $action|Out-Null
  $previous=$info.LastRunTime;Start-ScheduledTask $taskName;$until=(Get-Date).AddSeconds(120)
  do {Start-Sleep 2;$task=Get-ScheduledTask $taskName;$info=Get-ScheduledTaskInfo $taskName}while(($task.State -eq 'Running' -or $info.LastRunTime -eq $previous) -and (Get-Date)-lt $until)
  if($task.State -eq 'Running' -or $info.LastTaskResult -eq 0){throw 'Broken candidate must report failure'}
  if((Get-Service GameControlAgent).Status -ne 'Running'){throw 'Rollback failed to restart original service'}
  if((Get-FileHash "$env:ProgramFiles/GameControl/GameControl.Agent.exe").Hash -ne $binaryBefore){throw 'Rollback did not restore original executable'}
  if((Get-FileHash "$privateDir/connection.json").Hash -ne $connectionBefore -or (Get-FileHash "$privateDir/policy.json").Hash -ne $before){throw 'Rollback changed private policy/connection'}
  $state=Get-Content $statusFile -Raw|ConvertFrom-Json
  if(!$state.has_policy -or ([DateTimeOffset]::UtcNow-[DateTimeOffset]::Parse($state.updated_at)).TotalSeconds -gt 8){throw 'Rollback did not restore healthy signed policy enforcement'}
  'PASS: SYSTEM silent upgrade, preserved credentials/offline rules; locked-file failure retains running service; broken candidate rolls back to healthy original service'|Set-Content qa-artifacts/windows-auto-update.txt
 }finally{Unregister-ScheduledTask $taskName -Confirm:$false}
 'PASS: personal single-file install and connection-free upgrade preserves signed offline rules and restarts service'|Set-Content qa-artifacts/windows-upgrade.txt
 'PASS: native setup, signed offline policy, secret-free status, ACLs, logon autostart, Start shortcut, tray startup, restricted request IPC and independent enforcement'|Set-Content qa-artifacts/windows-smoke.txt
} finally {
 $listener.Stop()
 if(Test-Path $sentinel){Remove-Item $sentinel}
 $uninstall=Wait-InstallerProcess (Start-Process $setup -ArgumentList '--uninstall' -PassThru)
 if($uninstall.ExitCode -ne 0){throw 'Uninstaller failed'}
 if((Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run').Playtimely){throw 'Tray autostart survived uninstall'}
}
