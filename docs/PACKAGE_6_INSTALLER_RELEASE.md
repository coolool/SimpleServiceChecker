# Package 6 installer and release checklist

## Approved release policy

The owner approved a public, binary-only GitHub Releases channel and intentionally unsigned installers on September 14, 2026.

- Private source: `coolool/SimpleServiceChecker`.
- Public downloads: `coolool/StarlingServiceChecker-Releases`.
- Published assets: installer, SHA-256 checksum, and release manifest only.
- Excluded: source code, coverage workbooks, Google keys, customer information, databases, and local application data.
- Expected Windows behavior: **Unknown publisher** or Microsoft Defender SmartScreen may appear, and some managed computers may block installation.

The installer and manifest verification fail if an Authenticode signature is present, preventing accidental drift from the approved unsigned policy.

## Deliverables

- `installer/StarlingServiceChecker.iss` — stable per-user Inno Setup installer definition.
- `build/Build-Installer.ps1` — self-contained publish, Microsoft bootstrapper verification, installer compilation, SHA-256 checksum, source provenance, and release manifest.
- `build/Test-InstallerLifecycle.ps1` — unattended install, upgrade, rollback, and uninstall checks in an isolated program directory.
- `build/Test-ReleaseArtifacts.ps1` — fail-closed version, manifest, public update-location, checksum, provenance, and unsigned-policy verification.
- `.github/workflows/ci.yml` — installer and lifecycle proof on each pull request and `main` update.
- `build/Test-RepositorySafety.ps1` — fail-closed tracked-file scan for prohibited workbooks, databases, generated binaries, logs, private keys, and Google-key-shaped values without printing matched contents.
- `.github/workflows/release.yml` — manually triggered public draft-release workflow for existing annotated `vMAJOR.MINOR.PATCH` tags.
- Settings **Check for updates** action — opens the official public latest-release page through the restricted native bridge.

## Installer behavior

- Target: Windows 10/11 x64.
- Install context: current user, without an administrator prompt.
- Default location: `%LOCALAPPDATA%\Programs\Starling Service Checker`.
- Stable application ID: `{D2E88890-062E-4821-A5F5-FDF1D46EDD9C}`.
- Shortcuts: current-user Start menu entry and an optional, default-off desktop shortcut.
- Upgrade and rollback: the stable ID and previous-directory/task settings permit a newer or older package to replace the installed application in place.
- Uninstall: removes program files and shortcuts. Interactive uninstall asks whether to also delete the current user's encrypted Google key, coverage copies, WebView data, and other local application data; the default is to preserve them for reinstall.
- No Excel, .NET runtime, Git, developer tools, database, backend, or fibre-map data is installed.

## WebView2 prerequisite

The installer checks Microsoft's per-user and per-machine Evergreen Runtime registration. If missing, it runs the small Microsoft online bootstrapper using `/silent /install`. The build downloads the bootstrapper from Microsoft's stable link and refuses to package it unless Windows reports a valid Microsoft Authenticode signature.

The installer shows actionable guidance when the runtime remains unavailable. The application also catches the runtime-not-found condition before loading its local UI and directs the user to rerun setup while online or contact IT.

The version 1 installer is online-only. It deliberately does not bundle the much larger fixed/offline runtime.

## Artifact verification

Every installer path independently:

- recomputes the installer SHA-256 and verifies the exact checksum-file format;
- matches the manifest and executable version;
- verifies the exact public GitHub `releases/latest` URL;
- ties the artifact to the private source repository and a 40-character commit SHA; and
- confirms both the manifest and installer are unsigned.

Generated files remain ignored under `artifacts\installer`:

- `StarlingServiceChecker-Setup-<version>.exe`
- `StarlingServiceChecker-Setup-<version>.exe.sha256`
- `StarlingServiceChecker-<version>-release.json`

## One-time GitHub setup

Create a fine-grained personal access token restricted to `coolool/StarlingServiceChecker-Releases` with **Contents: Read and write** and no broader repository access. Add it to the private source repository as an Actions secret named `PUBLIC_RELEASE_TOKEN`.

The token is exposed only to the final draft-release step. Checkout, build, tests, lifecycle installation, and artifact verification run without it. Do not commit or paste this token into source, logs, manifests, issues, or release notes.

## Release procedure

1. Merge an owner-approved Package 6 branch with all checks green.
2. Confirm the public release repository contains no source or private data and `PUBLIC_RELEASE_TOKEN` is configured in the private source repository.
3. Create and push an annotated `vMAJOR.MINOR.PATCH` tag on the reviewed `main` commit.
4. Manually run **Public unsigned draft release** in the private source repository and enter that tag.
5. Confirm build, tests, lifecycle installation, checksum, provenance, public URL, and unsigned-policy verification pass.
6. Review the draft in the public release repository; do not publish it yet.
7. Run the clean-machine matrix below using the exact draft installer.
8. Confirm **Check for updates** opens the public latest-release page.
9. Publish the draft only after owner acceptance.

The workflow rejects lightweight tags, tags that are not on private-source `main`, mismatched checkouts, invalid manifests or checksums, signed installers, and attempts to overwrite an already-published release. The public draft contains a source-repository name and exact source commit for traceability, but no source code.

## Clean-machine matrix

Run on both clean Windows 10 x64 and Windows 11 x64 machines or snapshots with no Excel, separate .NET runtime, Git, or developer tools:

- download without a GitHub sign-in;
- unsigned installer and SmartScreen/Unknown publisher experience;
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
- Start menu shortcut, optional desktop shortcut, application icon, publisher text, version, update link, checksum, and manifest; and
- confirmation that company policy blocking is handled by contacting IT rather than disabling security controls.

Record machine versions, installer version/hash, WebView2 starting state, result, and evidence in the Package 7 acceptance record.

## Remaining Package 6 gates

- Configure `PUBLIC_RELEASE_TOKEN` in the private source repository.
- Complete clean Windows 10 and Windows 11 launch/lifecycle checks with the final public-channel build.
- Obtain explicit owner acceptance of Package 6 before Package 7 begins.
