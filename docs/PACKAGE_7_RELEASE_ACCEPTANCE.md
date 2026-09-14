# Package 7 release-candidate acceptance

## Release policy

- Product: Starling Service Checker 1.0.0.
- Source repository: private `coolool/SimpleServiceChecker`.
- Download repository: public, binary-only `coolool/StarlingServiceChecker-Releases`.
- Installer: self-contained Windows x64, per-user, intentionally unsigned.
- Update behavior: manual **Check for updates** link to the public latest-release page; no background updater.
- Publication gate: the release stays draft until this matrix is complete and the owner explicitly approves publication.

## Evidence already accepted

| Area | Evidence | Status |
| --- | --- | --- |
| Product workflow and visual design | Owner accepted Packages 1–5 plus map, Street View, Geocoding, branding, legend, tower, profile-layout, and minimize follow-ups | Passed |
| Automated domain and application behavior | 96 Core and App tests pass on the final Package 6 branch | Passed |
| Repository safety | CI scanned all tracked files without exposing matches; no workbook, key, database, installer, log, or customer data is tracked | Passed |
| Windows 10 installer lifecycle | Exact CI installer installed, upgraded over itself, and uninstalled under a normal Windows 10 Home 22H2 user token | Passed |
| Release artifact integrity | SHA-256, executable version, manifest, public update URL, private source provenance, and unsigned state independently matched | Passed |
| Public repository boundary | Repository is public, contains only its safety/download README, and has Issues and Wiki disabled | Passed |

The final Package 6 proof installer SHA-256 is `6a9322ab31e4640ff3dcb38531122a6edfaf8bbd6117be8f92527d17dd0130a2`. It is evidence for the build path, not the final tagged release candidate.

## Final tagged candidate

These steps must use the exact assets produced from the annotated `v1.0.0` tag on reviewed `main`:

- [ ] Configure `PUBLIC_RELEASE_TOKEN` in the private source repository. It must be a fine-grained token restricted to the public release repository with **Contents: Read and write**.
- [ ] Merge the owner-approved Package 7 documentation branch into `main`.
- [ ] Create and push annotated tag `v1.0.0` on the reviewed merge commit.
- [ ] Manually run **Public unsigned draft release** for `v1.0.0`.
- [ ] Confirm the workflow creates a draft—not a published release—in the public repository.
- [ ] Record the draft installer SHA-256 and source commit.
- [ ] Confirm an unauthenticated browser can reach the public repository and draft assets become public only after publication.

## Acceptance matrix

Use synthetic data for destructive/error cases and the supplied workbook only outside Git.

| Test | Required evidence | Status |
| --- | --- | --- |
| First run | Configure a company key and activate a valid workbook | Previously accepted; repeat on final candidate |
| Returning user | Key and active workbook reload without setup | Previously accepted; repeat on final candidate |
| Workbook lifecycle | Valid, invalid, replacement, restore, locked, and missing-source cases retain the last known-good data | Automated and Package 5 accepted; final smoke check pending |
| Location input | Places, coordinates, map click, and draggable pin update the same active customer location | Package 3 accepted; final smoke check pending |
| Fibre result | Match, no match, address required, and data-error states are distinct | Automated and Package 3 accepted; final smoke check pending |
| Wireless result | Zero, one, and many candidates are correctly ordered and labelled | Automated and Package 3 accepted; final smoke check pending |
| Terrain/Fresnel | Representative short and long paths render clearance, legends, details, minimize, and restore | Automated and Package 4 accepted; final smoke check pending |
| Google provider states | Restricted key, quota/provider error, timeout, and offline states remain bounded and do not expose provider diagnostics | Automated/provider proof accepted; final smoke check pending |
| Desktop layout | 1366×768, 1920×1080, Windows display scaling, keyboard navigation, and visible focus | Prior visual review accepted; final candidate checks pending |
| Windows 10 clean install | No Excel, separate .NET, Git, or developer tools; observe unsigned warning, install, launch, upgrade, uninstall, and local-data choice | Lifecycle passed; final tagged-candidate check pending |
| Windows 11 clean install | Same checks on Windows 11 x64 | Pending external machine or approved revised evidence plan |
| Public download | Non-developer can download from the official public link without GitHub sign-in and follow safe SmartScreen instructions | Pending published-candidate rehearsal |
| Privacy boundary | Release assets and installed application contain no key, real workbook, customer data, database, backend, logs, telemetry, fibre overlay, or regulatory layer | Automated scan passed; repeat on final assets |

## Owner publication gate

Do not publish the draft until:

- all remaining rows above have evidence or the owner explicitly approves a documented revised evidence plan;
- the final installer, checksum, and manifest match each other;
- the release notes clearly state that the installer is unsigned;
- the owner confirms the release candidate is approved for public download.
