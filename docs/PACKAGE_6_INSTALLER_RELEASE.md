# Package 6 installer and release checklist

## Deliverables

- `installer/StarlingServiceChecker.iss` — stable per-user Inno Setup installer definition.
- `build/Build-Installer.ps1` — self-contained publish, Microsoft bootstrapper verification, optional signing, installer compilation, SHA-256 checksum, and release manifest.
- `build/Test-InstallerLifecycle.ps1` — unattended install, upgrade, rollback, signature, and uninstall checks in an isolated program directory.
- `.github/workflows/ci.yml` — unsigned installer and lifecycle proof on each pull request and `main` update.
- `.github/workflows/release.yml` — protected signing and draft-release workflow for existing `vMAJOR.MINOR.PATCH` tags.
- Settings **Check for updates** action — opens the build-configured HTTPS location through the restricted native bridge.

## Installer behavior

- Target: Windows 10/11 x64.
- Install context: current user, without an administrator prompt.
- Default location: `%LOCALAPPDATA%\Programs\Starling Service Checker`.
- Stable application ID: `{D2E88890-062E-4821-A5F5-FDF1D46EDD9C}`.
- Shortcuts: current-user Start menu entry and an optional, default-off desktop shortcut.
- Upgrade and rollback: the stable ID and previous-directory/task settings permit a newer or older signed package to replace the installed application in place.
- Uninstall: removes program files and shortcuts. Interactive uninstall asks whether to also delete the current user's encrypted Google key, coverage copies, WebView data, and other local application data; the default is to preserve them for reinstall.
- No Excel, .NET runtime, Git, developer tools, database, backend, or fibre-map data is installed.

## WebView2 prerequisite

The installer checks Microsoft's documented per-user and per-machine Evergreen Runtime registration. If missing, it runs the small Microsoft online bootstrapper using `/silent /install`. The build script downloads the bootstrapper from Microsoft's stable link and refuses to package it unless Windows reports a valid Microsoft Authenticode signature.

The installer shows actionable guidance when the runtime remains unavailable. The application also catches the runtime-not-found condition before loading its local UI and directs the user to rerun setup while online or contact IT.

The version 1 installer is online-only. It deliberately does not bundle the much larger fixed/offline runtime.

## Signing-path proof

Pull-request CI creates a one-day, self-signed code-signing certificate on the disposable Windows runner and exercises the same application, installer, signed-uninstaller, and signature commands used by production. It verifies integrity and the exact expected test-certificate thumbprint without adding that fake issuer to Windows' trusted roots. A bounded `always()` cleanup step deletes the test certificate, and the test-signed installer is never uploaded as an artifact.

This proves the build mechanics without exposing or requiring the company's production certificate. It does not establish a trusted publisher or replace the timestamped production-signing and clean-machine gates. `AllowEphemeralTestSigning` exists only for this isolated test path; normal signed builds still require an HTTPS RFC 3161 timestamp URL and trusted-chain verification.

## Unsigned proof build

Prerequisites: .NET SDK from `global.json`, Inno Setup 6 or 7, and internet access.

```powershell
dotnet restore SimpleServiceChecker.sln --locked-mode
.\build\Build-Installer.ps1 -Version 1.0.0 -SkipRestore
```

Generated files are placed under `artifacts\installer` and remain ignored by Git:

- `StarlingServiceChecker-Setup-1.0.0.exe`
- `StarlingServiceChecker-Setup-1.0.0.exe.sha256`
- `StarlingServiceChecker-1.0.0-release.json`

## Protected production setup

Create a GitHub environment named `production-signing` and require an authorized reviewer before jobs can access its configuration. Restrict deployment to release tags where the account plan supports that control.

Environment secrets:

- `CODE_SIGNING_PFX_BASE64` — Base64 encoding of the company's password-protected Authenticode PFX.
- `CODE_SIGNING_PASSWORD` — PFX password.

Environment variables:

- `UPDATE_LOCATION_URL` — final HTTPS SharePoint or OneDrive folder/page used by **Check for updates**.
- `CODE_SIGNING_TIMESTAMP_URL` — the HTTPS RFC 3161 timestamp endpoint approved by the certificate issuer.

Never commit the certificate, password, API key, workbook, or decoded temporary PFX. The workflow imports the certificate into the ephemeral runner's current-user store, signs the application executable and first-party DLLs, lets Inno Setup sign the installer and uninstaller, verifies each relevant signature, and removes imported certificate material in an `always()` cleanup step.

The signing secrets are scoped only to the configuration-check and certificate-import steps rather than exposed to unrelated checkout, restore, build, or test actions. If a PFX includes a certificate chain, the workflow selects a currently valid private-key certificate with the Code Signing extended-key usage and removes every imported certificate from the runner afterward.

## Release procedure

1. Merge an owner-approved Package 6 branch with all checks green.
2. Confirm the `production-signing` environment requires approval and has the four required values.
3. Confirm `UPDATE_LOCATION_URL` is the real company-controlled download page—not the development GitHub fallback.
4. Create and push an annotated `vMAJOR.MINOR.PATCH` tag on the reviewed merge commit.
5. Approve the waiting `production-signing` job.
6. Confirm the workflow passes build, tests, signing, signature verification, lifecycle installation, and checksums.
7. Review the draft GitHub release; do not publish it yet.
8. Run the clean-machine matrix below using the signed installer.
9. Copy the signed installer and checksum to the company SharePoint/OneDrive download location.
10. Confirm the in-app update action opens that location.
11. Publish the release only after owner acceptance.

## Clean-machine matrix

Run on both clean Windows 10 x64 and Windows 11 x64 machines or snapshots with no Excel, separate .NET runtime, Git, or developer tools:

- online install without administrator credentials;
- WebView2 already present;
- WebView2 absent with successful bootstrap;
- WebView2 bootstrap blocked/offline with clear guidance and no misleading launch;
- first launch and company Google-key setup;
- valid workbook selection and activation;
- upgrade over the prior version with local key/workbook retained;
- rollback to the prior installer with local key/workbook retained;
- interactive uninstall preserving local data;
- reinstall recovery after preserved-data uninstall;
- interactive uninstall removing local data;
- Start menu shortcut, optional desktop shortcut, application icon, publisher, version, and update link; and
- signature and SHA-256 checksum verification.

Record machine versions, installer version/hash, WebView2 starting state, result, and evidence in the Package 7 acceptance record.

## Current external prerequisites

Package 6 cannot produce a generally distributable signed installer until the owner supplies or selects:

1. the final company SharePoint/OneDrive download-page URL; and
2. a valid Authenticode code-signing certificate and its approved RFC 3161 timestamp service.

These are release credentials/configuration, not application source, and must remain outside Git.
