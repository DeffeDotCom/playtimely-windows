# Code signing policy

## Current status
SignPath Foundation application was submitted on 2026-11-21. No Foundation approval or certificate is claimed. Existing previews are not Authenticode publisher-signed. A verified RSA update-metadata signature is not the same as an Authenticode publisher signature.

## Proposed signing scope
Only our Windows agent, tray and installer built from this public Windows-only repository may be submitted. No private website/server/database/mobile/Mac code, family connections, credentials, private keys or third-party executable payloads may be signed as our own code. Do not create personalised installers with a family's connection data inside the public signing pipeline.

Maintainer/reviewer: Richard Andersson ([DeffeDotCom](https://github.com/DeffeDotCom)). Proposed human release approver: Richard Andersson. Additional contributors must be assigned explicit roles, use MFA and follow protected-branch review. Automation may request a signature but must not replace SignPath's required human production approval.

Build provenance must link the exact public commit and the GitHub-hosted Windows workflow artifact. Sign the agent/tray before embedding them, then sign the installer; enforce consistent product/version metadata. Publishing is a separate reviewed action after native isolated install/enforcement/update/uninstall checks.

[Windows privacy disclosure](PRIVACY.md) · [Hosted service privacy policy](https://playtimely.com/privacy)

If accepted and production signing is actually operating, update the download/release page and this policy to acknowledge: Free code signing provided by SignPath.io, certificate by SignPath Foundation. Until then do not describe an unsigned preview as Foundation-signed.
