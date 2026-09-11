# Roadmap

## Phase 0 - Decisions and acceptance criteria

- [x] Confirm spreadsheet-only coverage ownership.
- [x] Confirm fibre address lookup without a fibre map overlay.
- [x] Confirm the low-maintenance exclusion list.
- [x] Create the private `coolool/SimpleServiceChecker` GitHub repository and connect local `main`.
- [x] Draft the product specification and first-release acceptance tests.
- [ ] Owner approves `FIRST_RELEASE_SPEC.md`.
- [ ] Owner confirms the proposed Windows 10/11 x64 installer audience.

## Phase 1 - Technical proof

- [ ] Create a minimal Windows shell with the selected embedded web renderer.
- [ ] Verify Google Maps, Places, and Elevation with a company-owned restricted test key.
- [ ] Verify the stable local application origin used by Google key restrictions.
- [ ] Import and validate the supplied workbook without Excel installed.
- [ ] Confirm map and terrain rendering on representative Windows computers.

No production installer work should begin until this proof is accepted.

## Phase 2 - Sales workflow

- [ ] Recreate the simplified Service Checker visual design.
- [ ] Add address, coordinate, and map-pin location selection.
- [ ] Add fibre address-range matching.
- [ ] Add tower range filtering and candidate ordering.
- [ ] Add tower circles, paths, and terrain profiles.
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
- [ ] Decide whether releases are manually downloaded or use a minimal signed updater.
- [ ] Code-sign release binaries before general distribution.

## Release acceptance

- A salesperson can install the application without developer tools or Excel.
- A first-time user can configure a company Google key and select a valid workbook.
- Address results appear quickly and distinguish fibre from wireless candidates.
- The map, paths, legend, and plots remain visually familiar to Service Checker users.
- Invalid replacement data cannot displace the last known-good workbook.
- The application performs no database, backend, login, logging, telemetry, ticketing, ISED, or fibre-overlay work.
