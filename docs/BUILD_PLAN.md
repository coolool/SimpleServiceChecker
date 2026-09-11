# Build plan

Status: Ready for owner review. This document authorizes no application implementation by itself.

## 1. Build objective

Deliver a signed, self-contained Windows 10/11 x64 application that lets a salesperson check fibre and fixed-wireless availability from a locally selected workbook during a customer call.

The application must preserve the recognizable Service Checker map, warm sidebar, result cards, legend, path colours, and terrain-chart appearance. It removes databases, backends, customer and ticket functions, logging, telemetry, fibre map geometry, and automatic synchronization.

## 2. Fixed decisions

- WPF on .NET 10 LTS hosts the desktop application.
- Microsoft Edge WebView2 Evergreen renders the map and web-style sales interface.
- The application is published self-contained for `win-x64`.
- The main interface uses bundled HTML, CSS, plain JavaScript modules, and Chart.js. It has no runtime CDN or front-end framework.
- Coverage comes only from a validated workbook containing `Tower Coverage` and `Fibre Coverage`.
- Google Maps, Places, and Elevation use a company-owned restricted key encrypted for the current Windows user.
- Wireless plots include first Fresnel-zone clearance with defaults of 5.8 GHz, 60%, and effective-earth-radius factor 4/3.
- ISED data is optional, manually downloaded, and imported into disposable local files. It never affects availability results.
- A signed per-user installer is distributed through a company-controlled SharePoint or OneDrive link.
- Updates are manual. `Check for Updates` opens the fixed company download location.

## 3. Source baselines

### Existing Service Checker

Use the existing `serviceChecker` files as a visual and behavioural reference, not as code to copy wholesale. Retain only the address and coordinate workflow, draggable map pin, tower ranges, candidate paths, sidebar resizing, fibre summary, result cards, clearance legend, and terrain-chart appearance.

Do not port PHP endpoints, SQL access, customer loading, Sonar ticket creation, pin logging, heatmaps, server configuration, or credentials.

Before changing the interface, capture synthetic-data screenshots at 1366 by 768 and 1920 by 1080. These become the visual comparison baseline. Repository screenshots must not contain real addresses, tower names, API keys, or customer information.

### Supplied workbook

The planning workbook currently contains:

- `Tower Coverage`: 224 data rows and 10 columns;
- `Fibre Coverage`: 16 data rows and 8 columns; and
- no formulas on either sheet.

The application must not ship or commit this workbook. Automated tests create synthetic workbooks with the same contract.

Tower units follow the existing Service Checker behaviour:

- `Tower Height`: feet;
- `Tower Range`: kilometres;
- coordinates: decimal degrees;
- speeds: megabits per second; and
- AP direction and coverage: degrees.

The current data has blank AP direction values with 360-degree AP coverage and blank notes. Both patterns are valid.

## 4. Architecture

### Repository layout

```text
SimpleServiceChecker.sln
src/
  SimpleServiceChecker.Core/
  SimpleServiceChecker.App/
    Web/
tests/
  SimpleServiceChecker.Core.Tests/
  SimpleServiceChecker.App.Tests/
installer/
docs/
```

`SimpleServiceChecker.Core` owns immutable domain models, workbook validation, fibre matching, distance and sector calculations, terrain and Fresnel calculations, and ISED normalization. It has no WPF or WebView2 dependency.

`SimpleServiceChecker.App` owns the WPF shell, native file dialogs, encrypted configuration, local-file activation, Google adapters, elevation requests, WebView2 bridge, and bundled web interface.

The web interface renders data supplied by the native application. Coverage, matching, and propagation calculations remain in C# so the same tested rules drive every screen.

### Native-to-web boundary

Expose a small, versioned message contract:

- web to native: app ready, select location, choose workbook, activate workbook, restore workbook, save Google setup, request terrain plot, import or remove ISED data, and open download pages;
- native to web: setup state, active-data summary, location result, candidate list, terrain datasets, ISED viewport records, progress, warnings, and errors.

Validate every incoming message. Do not expose arbitrary filesystem, shell, or process execution to JavaScript. Disable WebView2 developer tools in release builds and restrict navigation to the local application origin plus explicit external links opened in the system browser.

### Local storage

Use `%LOCALAPPDATA%\SimpleServiceChecker`:

```text
config/
  preferences.json
  google-key.dat
coverage/
  current.xlsx
  current-manifest.json
  previous.xlsx
  previous-manifest.json
ised/
  active.json
  versions/<import-id>/manifest.json
  versions/<import-id>/tiles/*.json.gz
temp/
```

Encrypt `google-key.dat` with Windows Data Protection API for the current user. Manifests contain hashes, schema versions, source filenames, activation dates, and record counts. They contain no searched addresses or coordinates.

All replacement operations write to a temporary location, validate completely, and then atomically switch the active manifest. Startup removes abandoned temporary files. There is no database and no persistent application log.

## 5. Work packages

Each work package is a separate pull request unless a very small documentation-only correction belongs with its implementation. Every code-changing pull request updates `docs/ROADMAP.md` with tests and acceptance evidence.

### Package 1: Foundation and technical proof

Build:

- create the solution, Core, App, and test projects;
- add Windows-only CI for restore, build, and tests;
- host bundled local HTML through a WebView2 HTTPS virtual host;
- establish the typed native-to-web message bridge;
- load a synthetic map screen and bundled Chart.js chart;
- read the supplied workbook outside the repository without Excel installed;
- prove Maps JavaScript, Places, and Elevation using a temporary company test key;
- test HTTP referrer and API restrictions using the exact WebView2 origin; and
- create an unsigned, non-production self-contained proof build.

Gate:

- the restricted Google key works for all three required services without becoming unrestricted;
- workbook headers and the 224/16 row counts are recognized;
- no real workbook or key appears in Git, build output, test output, or screenshots;
- the shell, map, and chart run on representative Windows 10 and Windows 11 machines; and
- the owner accepts the proof before production UI work starts.

If Google does not consistently receive an authorizable HTTPS origin from WebView2, stop. Record the failing request evidence and revise the map architecture before continuing.

### Package 2: Coverage domain and workbook validation

Build:

- typed tower, fibre-range, workbook-manifest, warning, and error models;
- read-only `.xlsx` parsing without Excel;
- exact sheet and normalized-header validation;
- row-level parsing with original row numbers;
- coordinate, height, range, speed, AP direction, and AP coverage validation;
- fibre civic-range parsing, including inclusive end values and `*`;
- address normalization for case, punctuation, spacing, and common street types;
- Haversine distance, range filtering, sorting, and future-safe sector logic; and
- deterministic error and warning messages suitable for sales users.

Test:

- valid workbook and reordered columns;
- missing or duplicate sheet and header cases;
- degree symbol, whitespace, number stored as text, blank, zero, and invalid number cases;
- coordinates at all boundaries;
- blank AP direction with 360-degree coverage;
- fibre range boundaries, open-ended ranges, postal-code discrimination, and conflicting matches;
- distance boundary exactly equal to tower range; and
- cancellation and file-read failures.

Gate: all coverage results can be produced from synthetic test data without WebView2 or Google.

### Package 3: Sales interface and map workflow

Build:

- first-run setup for Google configuration and workbook selection;
- resizable warm-coloured sidebar and full-window Google map;
- Places search, decimal coordinates, map click, and draggable pin;
- structured address transfer for fibre matching;
- fibre result shown before wireless candidates;
- tower circles, candidate paths, result cards, distances, speeds, notes, and legend;
- clear loading, empty, invalid-data, provider-error, quota, and offline states;
- keyboard focus, labels, non-colour status cues, and laptop-size responsive layout; and
- cancellation so a newly selected location cannot display results from an older request.

Local fibre and tower-range results render before elevation requests complete. No searched location persists after the active view is cleared or the application closes.

Gate: a salesperson can complete the primary check without opening Settings after first-time setup, and the synthetic-data screenshots match the approved visual baseline.

### Package 4: Terrain, line clearance, and Fresnel plots

Build:

- request elevation only when a candidate plot is opened or selected;
- cancel stale requests when the location or selected tower changes;
- use bounded sampling and explicit provider-error states;
- calculate line of sight from customer and tower endpoint heights;
- apply clutter height as a labelled planning assumption;
- calculate earth curvature and the full first Fresnel-zone envelope in Core;
- offer frequency, Fresnel percentage, and earth-model controls with approved defaults;
- render terrain, clutter, line of sight, full Fresnel envelope, 60% reference, and clearance status in Chart.js; and
- preserve the established chart colours and map-path clearance legend.

Test:

- known flat and curved-earth paths;
- zero-length and invalid paths;
- first Fresnel radius at endpoints and midpoint;
- known 10-kilometre, 5.8-GHz midpoint result;
- 0%, 60%, and 100% clearance settings;
- endpoint-height changes;
- obstruction and colour thresholds; and
- out-of-order, failed, and quota-limited elevation responses.

Gate: calculations match independent fixtures, and the plot remains explicitly labelled as a planning estimate rather than an installation guarantee.

### Package 5: Spreadsheet management and recovery

Build:

- Choose Spreadsheet, Validate and Activate, Reload, Open Current, and Restore Previous;
- progress and row-specific validation results;
- SHA-256 source hash and manifest creation;
- current and previous local workbook copies;
- atomic activation and rollback;
- visible source filename, activation time, counts, warnings, and application schema version; and
- safe handling of locked, moved, deleted, OneDrive-hosted, and network-path source files.

The application reads the selected source only during validation or an explicit reload. It never watches or edits the source workbook.

Gate: invalid or interrupted replacement attempts cannot displace the active workbook, and restoration works after an application restart.

### Package 6: Optional ISED reference layer

Build:

- Open Official ISED Download Page and Import ISED Files;
- support the approved All Services and Terrestrial Spectrum Licence site-data ZIPs;
- validate archive names, members, field counts, headers where supplied, coordinates, and source dates;
- default the first release to Ontario while retaining a province-aware format that can extend across Canada;
- stream input records into bounded geographic temporary partitions;
- group records sharing a physical coordinate within one partition;
- generate compressed JSON tile files and a versioned manifest;
- atomically activate the new tile set only after every partition succeeds;
- load and cluster only tiles intersecting the visible map viewport;
- provide distinct blue ISED markers, source details, layer toggle, replacement, and removal; and
- state throughout the UI that ISED records are regulatory references, not coverage or necessarily unique physical towers.

No automatic download, scheduled update, background service, SQLite file, or other database is permitted.

Test:

- both supported archive formats;
- missing, renamed, truncated, malformed, duplicate, and future-schema inputs;
- coordinates on tile boundaries;
- multiple records at one coordinate;
- cancellation and disk-full simulation;
- failed import retaining the previous active version;
- viewport loading and marker clustering; and
- proof that adding or removing ISED data cannot change fibre or wireless results.

Gate: a large representative import completes with bounded memory and the sales workflow stays responsive with the layer enabled.

### Package 7: Installer, signing, and release automation

Build:

- self-contained `win-x64` release publishing;
- a per-user Inno Setup installer with a stable application ID;
- Start menu entry, optional desktop shortcut, upgrade-over-existing, and clean uninstall;
- WebView2 Evergreen detection and silent per-user bootstrap when missing;
- clear failure guidance if the WebView2 bootstrap cannot complete;
- Authenticode signing for application and installer binaries;
- GitHub Actions build, test, publish, sign, checksum, and draft-release workflow;
- protected approval before signing and publishing;
- `Check for Updates` opening the company SharePoint or OneDrive location; and
- release checklist and administrator-free salesperson instructions.

The normal online installer uses the small WebView2 bootstrapper. Keep an offline installer variant out of version 1 unless a real deployment environment requires it, because bundling the fixed runtime would substantially increase package size and servicing work.

Gate: install, launch, upgrade, rollback, and uninstall succeed on clean Windows 10 and 11 x64 systems without Excel, .NET, Git, or developer tools installed.

### Package 8: Release candidate and acceptance

Run the complete acceptance matrix using synthetic data plus the supplied workbook outside Git:

- first-run and returning-user workflows;
- valid, invalid, replaced, restored, locked, and missing workbooks;
- Places, coordinate, map-click, and drag-pin input;
- fibre match, no match, address required, and data error;
- zero, one, and many wireless candidates;
- terrain and Fresnel plots at representative path lengths;
- Google key restriction, quota, timeout, and offline cases;
- no ISED data, valid import, failed replacement, enabled layer, and removed layer;
- 1366 by 768, 1920 by 1080, and Windows display scaling;
- keyboard workflow and visible focus;
- clean install, upgrade, uninstall, and remaining local-data disclosure; and
- repository and release-artifact scan for keys, real workbooks, customer data, logs, and searched locations.

Gate: owner signs off on the release candidate. Publish the signed installer to the company SharePoint or OneDrive location only after this gate.

## 6. Continuous integration

Pull requests run:

1. dependency restore from locked files;
2. Release build with warnings treated as errors for owned code;
3. Core and application integration tests;
4. synthetic workbook tests;
5. web asset and native-message contract tests;
6. synthetic screenshot smoke tests at the two baseline viewport sizes; and
7. secret and prohibited-artifact checks.

CI never receives a real Google key or coverage workbook. Google integration is a separately approved manual proof because it incurs external requests and billing.

Version tags create draft GitHub releases. General-release publication requires a protected manual approval and successful clean-machine evidence.

## 7. Low-maintenance rules

- Prefer stable LTS and Evergreen dependencies over pinned browser runtimes.
- Pin NuGet, GitHub Action, installer, and vendored JavaScript versions in source control.
- Use automated dependency alerts, but merge updates only after tests pass.
- Do not add a service, database, auto-updater, crash reporter, analytics SDK, or background task.
- Keep business rules in Core and provider-specific Google code behind a narrow adapter.
- Keep no runtime CDN dependency.
- Rebuild the self-contained installer when a relevant .NET security patch requires it.
- Test upcoming WebView2 behaviour periodically with synthetic data; do not collect user activity.

## 8. Main risks and controls

| Risk | Control | Blocking gate |
| --- | --- | --- |
| Google referrer restrictions fail in local WebView2 | Use an HTTPS virtual host and test the actual restricted key before full development | Package 1 |
| Google cost or quota interrupts calls | Company-owned restricted key, bounded elevation requests, cancellation, and clear quota errors | Packages 1 and 4 |
| Replacement workbook is malformed | Complete validation, temporary copy, hash, manifest, and atomic activation | Packages 2 and 5 |
| Fibre address wording produces false assurance | Structured address matching and explicit `Not listed` wording | Packages 2 and 3 |
| Radius or terrain is mistaken for guaranteed coverage | Candidate language and visible planning disclaimer | Packages 3 and 4 |
| ISED extracts overwhelm memory or the map | Streaming partitions, compressed tiles, viewport loading, and clustering | Package 6 |
| ISED is mistaken for company service | Separate colour, legend, controls, and no connection to availability logic | Package 6 |
| Unsigned installer creates user distrust | Sign binaries and verify on clean machines before distribution | Package 7 |
| Real data or keys enter Git | Synthetic fixtures, ignore rules, CI scans, and release checklist | Every package |

## 9. Completion definition

Version 1 is complete only when:

- every package gate has recorded evidence;
- all automated and manual acceptance tests pass;
- the owner approves the visual comparison and release candidate;
- the installer is signed and tested on clean Windows 10 and 11 x64 systems;
- the installer is available through the agreed company link;
- a non-developer can install, configure, check a location, replace the workbook, and uninstall using the supplied instructions; and
- the repository and installed application contain no database, backend, real coverage workbook, shared Google key, customer data, logs, telemetry, fibre overlay, or automatic ISED synchronization.

## 10. Start condition

No application code has been created. Implementation begins only after the owner explicitly accepts this build plan and asks to start Package 1.

## 11. Technical references

- [.NET application publishing overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/)
- [WebView2 local content and virtual host mapping](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/working-with-local-content)
- [WebView2 Runtime distribution](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution)
- [Google Maps Platform API security guidance](https://developers.google.com/maps/api-security-best-practices)
- [ISED Spectrum Management System data downloads](https://ised-isde.canada.ca/site/spectrum-management-system/en/spectrum-management-system-data/download-sms-data)
