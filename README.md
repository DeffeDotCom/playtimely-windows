# Playtimely Windows Agent

Open-source Windows client for parent-configured game-time boundaries. MIT licensed, maintained by WebFinance Digital i Sverige AB. Windows10/11 x64; .NET10 source, self-contained releases.

This repository contains **only** the Windows background service, tray, installer, tests and Windows build support. The separately operated Playtimely website, backend, database, parent mobile apps and Mac client are **not** included or licensed by this repository. Using the hosted service requires a Playtimely family account; this does not change the MIT licence of this client. No production credentials or family connection files belong here.

## What the Windows client does
- Fetches and validates signed game rules over HTTPS; continues with the last verified rules when offline.
- Measures recognised game time, applies schedules/budgets/checklists, and supports temporary parent time adjustments and game stops.
- Provides a local child-facing notification-area application and bounded extra-time requests. No remote shell, screen recording, key logging or browsing-history capture.
- Runs enforcement as a LocalSystem service across Windows user sessions. Child standard accounts are recommended; Windows administrators can disable or uninstall it.
- Closing games may lose unsaved work. Immediate parent termination requires acknowledgement in the parent interface; normal closing has a save/exit opportunity but cannot guarantee a game saves.

## Download and setup
Use [Playtimely downloads](https://playtimely.com/downloads) and the parent dashboard's personal installer to connect a gaming PC. A generic unpaired build is not an already configured family installation. Source releases are also available from the downloads page.

See [BUILDING.md](BUILDING.md), [PRIVACY.md](PRIVACY.md) and [Code signing policy](CODE_SIGNING.md). SignPath Foundation application was submitted on 2026-11-21; **no SignPath approval or signature is claimed**. Existing previews remain unsigned by an Authenticode publisher until signing is actually configured.

The older component READMEs contain chronological prototype notes. This root README and the current source describe present behaviour, including aggregate usage reporting, signed update downloads, daily budgets and closing notices.

MSIX material under `packaging/msix` is a **non-installable, unsigned packaging prototype**, not a supported release. Store capability approval, pairing/state initialisation and Store-compatible updates remain gates.
