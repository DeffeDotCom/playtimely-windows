# MSIX packaging prototype — NOT an installable product

This manifest/build script demonstrates packaging the Windows agent and tray with declared LocalSystem service/startup extensions. It contains no server, database, website, parent mobile app, private key or family configuration. It is unsigned, uses a deliberately non-production identity, and is not submitted/published. Do not install it or disable security protections to do so.

Run only on a disposable Windows packaging machine after building Components:

```powershell
pwsh ./packaging/msix/Build-Prototype.ps1 -Prototype -MakeAppxPath 'C:/path/to/WindowsSDK/bin/x64/makeappx.exe'
```

A successful MakeAppx build validates packaging/schema, not service behaviour or Store acceptance. The prototype cannot replace the current personal installer. Gates before a supported MSIX:

1. Verified Company Partner Center account, reserved product and exact assigned package Identity Name/Publisher; approval for `runFullTrust`, `packagedServices`, `localSystemServices`.
2. Explicit parent-only first-run pairing/connection import, protected ProgramData/state initialisation, and graceful handling of an unpaired service. No per-family credentials in a common Store package.
3. Store-managed updating: disable/replace the EXE self-updater for packaged installs; never overwrite WindowsApps, invoke the legacy EXE installer or install a second service from the package.
4. Migration from an existing EXE-installed GameControlAgent, service-name conflict handling, retained signed offline rules and safe rollback. Multi-user tray/startup availability, standard child accounts and state ACLs must be tested.
5. Complete service/tray/stop/time enforcement, cold boot, offline, upgrade, repair, cancellation and clean uninstall QA in an isolated VM; privacy/capability disclosures and review credentials. No claim that these MSIX runtime gates have passed.

Store package signing after certification does not provide a certificate for the separate website EXE. Direct sideload MSIX still requires a trusted certificate. Keep the existing EXE preview delivery until the new path is actually approved and verified.
