# Playtimely Windows agent — 0.1 preview

Windows 10/11, x64. Open-source component, MIT licensed. The web platform is not covered by this license.

## Build

Install .NET 10 SDK, then:

```sh
dotnet publish GameControl.Agent.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o out
```

Package `out/GameControl.Agent.exe`, `install.ps1`, `uninstall.ps1`, this README, LICENSE. Run `install.ps1` in elevated PowerShell and select `connection.json` from the dashboard. Unsigned preview: do not bypass Windows security warnings; production release needs Authenticode signing and a verified installer. No claim of Windows integration testing until tested on an actual Windows machine.

## Protocol and threat model

- HTTPS GET to the configured policy endpoint; bounded playtime requests and their status use the same HTTPS origin. No redirects, credentials in Authorization header, never URL.
- Per-device 256+ bit secret stored under ProgramData with SYSTEM/Administrators-only ACL. Server stores its SHA-256 hash.
- RSA SHA-256 signature checked against public key pinned in the initial connection file; schema, device ID, dates, time zone, known process names validated before use.
- Last verified JSON survives restarts and network outages. A revoked token stops fetching, not existing offline enforcement. Uninstall is the local recovery path; rotating keys requires reinstalling connection config.
- Fetch every 55–65 s with randomized startup, conditional ETag/304; enforce every 2 s. Only last fetch time recorded at server. No process lists/content uploads. First installation without a valid policy allows apps until first successful fetch.
- Default allow. Fixed safe game catalog: unknown executable names cannot be injected even by signed policy. Minecraft Java matches the explicit client main class, not all Java; mods/custom launchers need separate validation. Store Roblox generic process requires Roblox package path.
- Local Windows clock and configured IANA timezone, DST supported. Start inclusive/end exclusive; overnight rules belong to their starting weekday. Admin-level clock manipulation/renaming/replacement and alternate game clients can bypass rules. Use child standard accounts; this is a lightweight boundaries tool, not anti-tamper DRM.
- No remote shell, executable downloads, auto-updater, screenshots, key capture, browsing data or arbitrary process commands. Updates are manual and require an administrator.
- Game termination can lose unsaved progress. The tray shows time remaining; proactive warning notifications are not implemented.

The signing private key stays on the server. Never publish connection.json, policy caches, secrets, or signing private keys.

## Child-facing status (0.2)

The installer now includes `Playtimely.Tray.exe`, a separate, unprivileged Windows notification-area app. It starts at each user's Windows sign-in; an existing session can open **Playtimely** from Start. Windows may put its icon behind the notification-area arrow. Left-click opens game times; closing the window hides it. Exiting the icon does not stop the service.

The UI displays locked/allowed, the next opening/closing time, countdown until the current schedule ends, family timezone, last sync and stale-service/offline state. The UI follows the Windows display language, with a per-user language picker and English fallback. Included: English, Swedish, German, Danish, Norwegian Bokmål, Finnish, French, Spanish, Italian, Portuguese, Dutch, Polish, Czech, Romanian, Ukrainian, Russian, Turkish, Japanese, Korean and Simplified Chinese. Time remaining is **schedule time**, not a measured daily game-time budget. No local rule editing or credentials are exposed.

`ProgramData/PlaytimelyStatus/status.json` is a separate secret-free projection written atomically by the service. Users get read-only access; SYSTEM and Administrators can write. `ProgramData/GameControl/connection.json` and the signed cache remain private. The tray does not inspect processes. From 0.6 it checks a signed public release manifest and can download a checksum-verified installer after parent consent. Both use the same policy engine, including overnight rules and DST. First-run/no-policy and stale heartbeat are shown explicitly.

Build tray before the installer: `dotnet publish ../agent-tray/Playtimely.Tray.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ../agent-tray/out`.

## Playtime requests (0.4)
The tray can ask for 15/30/60 extra minutes or unlock until family-local midnight, with a fixed reason. It sends only this bounded request through a local interactive-user named pipe to the service; the connection key stays SYSTEM/admin-only. Network pipe clients are denied. The service posts to the same HTTPS origin and reads the computer's own status, never parent credentials or arbitrary commands. Requests expire after two hours or midnight, deduplicate per game, and are capped at six/hour and twenty/day/device. Parent owner/admin approval is required, with optional revocation. Email plus web/app inbox delivery; no native background push yet.

New service sends X-Playtimely-Policy: 2 to receive optional signed allowed_until per rule. Legacy agents still receive the original strict JSON but do not support temporary permissions: update the installer before using requests. The exact UTC expiry is enforced locally, including offline. Normal schedules remain unchanged. Parent editing/pausing the game clears the temporary permission. Status reflects the next policy poll (about one minute). From 0.6, the icon menu checks for updates at startup/every six hours. Installation requires parent consent and Windows administrator permission; the existing connection is retained. Older installations need one manual installer update.

## Network resilience and localization QA
Rule/status downloads run independently of the two-second local enforcement heartbeat. Slow TLS or offline requests cannot suspend schedule enforcement. `deploy/windows-smoke.ps1` tests a stalled local TLS endpoint, heartbeat freshness and termination of a harmless test process. `agent-tray-qa` opens status/request windows in all 20 languages and captures representative screenshots. Translations are bundled locally; no child text is sent for online translation.

For ordinary users, use the personal installer from the family dashboard. It includes the connection configuration; no PowerShell or separate JSON is required. See `agent-installer/README.md` for bundle and update details.

## Graceful game shutdown (0.7.3)
When a controlled game becomes blocked, the service starts a monotonic 30-second
shutdown deadline for that process instance. Its read-only status projection contains
only the selected game key, PID and creation timestamp as a close intent (no paths,
command lines or credentials). The tray in the player's session checks that identity
and posts WM_CLOSE to the process's visible windows without activating them. A
localized notification explains that game time is over. Windows notification settings
can suppress that notice. The tray must be running for the normal close attempt and
notice; the service still enforces the deadline if it is not running.

The service forces termination only if the same process remains blocked after the
deadline. A received policy allowing the game cancels the pending force deadline;
it cannot undo a normal close already delivered. Game-specific save/exit dialogs,
anti-cheat restrictions and elevated game windows may prevent a graceful exit.
Automatic saving and error-free exits are not guaranteed. The five-minute reminder
remains useful for saving and leaving a match before time runs out.
