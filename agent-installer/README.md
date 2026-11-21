# Native Windows installer

MIT-licensed along with the agent. Build the agent and agent-tray first, then:

```sh
dotnet publish GameControl.Setup.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o out
```

Setup embeds both the agent and tray binaries. UAC administrator elevation is declared in app.manifest. The UI reads a bundled connection, reuses an existing installation for updates, or lets advanced users select a connection.json. It validates its HTTPS URL/schema/public key, keeps connection credentials private to SYSTEM/Administrators, grants users read/execute on binaries and read-only on the separate public status projection, and creates an automatic Windows service. It registers tray autostart for user sign-ins and a Start-menu shortcut. It can stop/delete the service and remove tray/autostart; local files remain for deliberate manual cleanup. The preview is not Authenticode signed. CI verifies native installation and upgrade on Windows.

## Personal installation and updates (0.6+)
The authenticated `/devices/installer` response appends bounded connection JSON, a little-endian uint32 length and ASCII `PLAYTIMELY-CONN1` to the ordinary single-file EXE. It is private/no-store and validates family, role, session expiry and the current device key. No per-user file is retained on the server. Treat the downloaded personal installer as a credential; delete it after installation. The link expires, but the embedded device key remains valid until rotated/deleted.

Setup detects this footer automatically. A generic installer on an existing installation instead offers **Update**, retaining the protected connection and signed offline rules. UAC elevation remains mandatory via the manifest; ordinary users need administrator credentials. CLI QA: `--install-bundled`, `--upgrade`, existing `--install path`, `--uninstall`.

From Windows 0.8.9, the LocalSystem service checks for automatic updates after a 10–15 minute startup delay and roughly every six hours. It downloads/verifies while enforcement continues, defers during tracked game activity, and uses staged binaries plus a previous-version backup. A failed swap/start/healthy-policy heartbeat restores the previous executable pair and restarts the old service. Connection, signed offline rules and daily budget data are retained.

The tray still checks for an available manual update and keeps its parent-approved/UAC installer path as a fallback. Automatic updating requires one initial manual upgrade from older versions. Both download paths verify the RS256 release envelope, fixed HTTPS download URL, bounded size and SHA256. Release-envelope signing is not Authenticode; the preview EXE remains unsigned by Windows publisher certificates. See [automatic update behavior and validation](../docs/AUTOMATIC-WINDOWS-UPDATES.md).

## Desktop presentation (0.6.1)
Setup uses the Playtimely website illustration and a three-step introduction. Shared
`desktop-ui/ContentWindow.cs` sizes the setup, status and request forms to their
content, capped to the current monitor working area. A separate scrolling viewport
keeps tall content reachable; card widths and wrapping follow the viewport. Native
button painting preserves readable disabled/busy/completed states and focus cues.
The status UI distinguishes allowed and locked games using both text and color.
Include `desktop-ui/` when distributing/building the desktop source archive.

## Immediate status-app launch

After installation or upgrade starts the Windows service, Setup starts the installed tray with `--show` so game times are visible immediately. It uses the shell token from the same interactive desktop session, not the elevated installer token (including when UAC uses another parent's administrator account). The user's environment is preserved. The normal sign-in Run entry still starts the tray quietly.

An unattended session without a desktop, an elevated shell, or a failed launch does not undo a successful service installation. Interactive Setup then explains that the service is running and gives the Start-menu fallback. Closing the window or tray does not stop the service.

Windows QA exercises the installed executable with a standard-user token in the current session and requires a visible game-times window. Hosted-runner validation is not a physical Windows/UAC credential-switch test.
