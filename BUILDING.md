# Build the Windows-only source

Install .NET10 SDK and PowerShell7 on Windows. No private server repository, production credentials, token, database or connection.json is needed to compile or run the portable tests.

```powershell
pwsh ./build.ps1 -Stage All
```

Stages: `Components` builds agent/tray; `Installer` embeds their existing output; `All` runs portable tests and both stages. Output: `agent/out/GameControl.Agent.exe`, `agent-tray/out/Playtimely.Tray.exe`, `agent-installer/out/GameControl.Setup.exe`. Do not use test-generated/broken installer candidates as releases.

The manual GitHub workflow runs on GitHub-hosted Windows runners. It uploads unsigned client executables, optionally obtains SignPath signatures for those two first, embeds the signed executables in a freshly built installer, then optionally signs the installer. No automatic deployment or release publication occurs. This ordering matters: signing only the outside installer does not sign the embedded programs.

## Before enabling SignPath requests
1. Foundation approval and an account/project for **this Windows repository only**.
2. MFA for maintainers and signing approvers; Richard Andersson is the proposed human approver, not an automated agent. Configure protected branches/review and the `signpath-release` environment with required human reviewers.
3. Install the SignPath GitHub App with access to **only this repository**. Never choose all repositories or the private service repository.
4. SignPath production policy with manual approval and trusted GitHub-origin verification. All contributing jobs must use GitHub-hosted runners, not our local VM. Configure separate artifact configurations for the two client executables and for the installer; enforce product/version metadata and sign our own executables only.
5. Repository variables: `SIGNPATH_ENABLED=true`, `SIGNPATH_ORGANIZATION_ID`, `SIGNPATH_PROJECT_SLUG`, `SIGNPATH_SIGNING_POLICY_SLUG`, `SIGNPATH_COMPONENTS_CONFIG`, `SIGNPATH_INSTALLER_CONFIG`. Repository/environment secret: `SIGNPATH_API_TOKEN`. Values come from the approved account, not placeholders.
6. Manually run the workflow with `sign=true`. A normal unsigned build requires neither SignPath credentials nor its GitHub App. Review the first signed artifacts on an isolated Windows VM before publishing.

A build succeeding in GitHub is not a claim of native install/enforcement QA. The existing `deploy/local-windows-qa.ps1` requires an elevated interactive disposable `PT-BUILD-*` Windows VM and deliberately refuses an existing family installation. Never run it on a customer PC. Existing local VM QA remains useful but cannot replace SignPath's hosted build provenance.
