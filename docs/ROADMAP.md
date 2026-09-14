# Roadmap

## Phase 0 - Decisions and acceptance criteria

- [x] Confirm spreadsheet-only coverage ownership.
- [x] Confirm fibre address lookup without a fibre map overlay.
- [x] Confirm the low-maintenance exclusion list.
- [x] Create the private `coolool/SimpleServiceChecker` GitHub repository and connect local `main`.
- [x] Draft the product specification and first-release acceptance tests.
- [x] Confirm Windows 10/11 x64 as the initial installer audience.
- [x] Confirm a self-contained application package.
- [x] Add Fresnel-zone plotting to the wireless scope.
- [x] Defer external spectrum and regulatory-data imports or map layers beyond version 1.
- [x] Owner approves `FIRST_RELEASE_SPEC.md`.
- [x] Owner accepts WPF/.NET and WebView2 as the technical direction.
- [x] Owner selects a company-controlled SharePoint or OneDrive installer link.
- [x] Owner accepts `BUILD_PLAN.md` and explicitly starts Phase 1 implementation (September 14, 2026).

## Phase 1 - Technical proof

- [x] Create a minimal Windows shell with the selected embedded web renderer (September 14, 2026).
- [x] Verify Google Maps, Places, and Elevation with a company-owned test key (September 14, 2026).
- [x] Verify the stable local application origin used by Google key restrictions (September 14, 2026).
- [x] Import and validate the supplied workbook without Excel installed (September 14, 2026).
- [ ] Confirm map and terrain rendering on representative Windows computers.

### Package 1 evidence

- Release build: succeeded with zero warnings and zero errors on Windows 10 x64.
- Automated tests: 13 passed, including bridge allow-listing, stable HTTPS origin, per-user key encryption, camel-case result serialization, malformed-workbook recovery, and synthetic workbook validation.
- Supplied workbook: validated read-only outside the repository with 224 tower rows and 16 fibre rows.
- Packaging: unsigned self-contained `win-x64` proof published successfully; .NET is included and WebView2 Evergreen remains an external runtime until Package 6.
- GitHub Actions: Windows restore, build, 12 tests, JavaScript syntax check, self-contained publish, and proof-artifact upload passed in pull request 1.
- Owner-feedback revision: white Places input, corrected workbook-result display, clearance-coloured synthetic map path with the established five thresholds, and a taller terrain/Fresnel profile.
- Owner screenshot: Maps JavaScript, Places (New), and Elevation all passed with the company key on the stable local HTTPS application origin.
- Owner accepted the revised proof on Windows 10 with the supplied workbook on September 14, 2026. The acceptance screenshot contains no key, real address, or tower name.
- Windows 11 clean-machine confirmation remains tracked for the Package 6/7 acceptance matrix; it is not claimed by this proof.

Package 1 is accepted. Real workbook towers are intentionally deferred to Packages 2 and 3; Package 1 only validates the workbook contract and demonstrates a synthetic path.

## Phase 2 - Sales workflow

- [ ] Recreate the simplified Service Checker visual design.
- [ ] Add address, coordinate, and map-pin location selection.
- [ ] Add fibre address-range matching.
- [ ] Add tower range filtering and candidate ordering.
- [ ] Add tower circles, paths, terrain profiles, and Fresnel-zone clearance.
- [ ] Add clear unavailable, incomplete-data, provider-error, and offline states.

## Phase 3 - Spreadsheet management

- [ ] Add Choose, Validate, Activate, Reload, Open, and Restore actions.
- [ ] Validate exact sheet and column contracts with row-specific warnings.
- [ ] Keep the current data active when a replacement is invalid or interrupted.
- [ ] Add a visible data filename, activation time, record count, and warning summary.

## Phase 4 - Installer and release

- [ ] Produce a per-user Windows installer with no administrator requirement where possible.
- [ ] Detect or bootstrap the required embedded browser runtime.
- [ ] Ensure API keys, workbooks, and local data are excluded from packages and Git.
- [ ] Test install, upgrade, rollback, and uninstall on clean Windows systems.
- [ ] Publish signed installer artifacts for manual download and add a `Check for updates` link to the chosen download page.
- [ ] Code-sign release binaries before general distribution.

## Release acceptance

- A salesperson can install the application without developer tools or Excel.
- A first-time user can configure a company Google key and select a valid workbook.
- Address results appear quickly and distinguish fibre from wireless candidates.
- The map, paths, legend, and plots remain visually familiar to Service Checker users.
- Invalid replacement data cannot displace the last known-good workbook.
- The application performs no database, backend, login, logging, telemetry, ticketing, external regulatory-data import or overlay, or fibre-overlay work.
