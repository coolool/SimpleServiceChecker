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

- [x] Recreate the simplified Service Checker visual design (Package 3 implementation).
- [x] Add address, coordinate, and map-pin location selection (Package 3 implementation).
- [x] Add fibre address-range matching in Core (September 14, 2026).
- [x] Add tower range filtering and candidate ordering in Core (September 14, 2026).
- [ ] Add tower circles, paths, terrain profiles, and Fresnel-zone clearance. Tower circles and candidate paths are implemented; terrain and Fresnel profiles remain Package 4.
- [x] Add clear unavailable, incomplete-data, provider-error, and offline states for the sales-map workflow (Package 3 implementation).

### Package 3 - Sales interface and map workflow

- [x] Persist and automatically reload a fully validated active workbook for first-run setup.
- [x] Add the resizable warm sidebar, full-window map, Settings screen, address search, decimal coordinates, map click, and draggable customer pin.
- [x] Transfer structured Google address components to native fibre matching without retaining searched locations.
- [x] Render fibre status before distance-ordered wireless candidate cards.
- [x] Draw real workbook tower points, published-range circles, and candidate paths with a non-colour-only legend.
- [x] Keep local fibre and tower calculations usable from known coordinates when Google is unavailable.
- [x] Cancel superseded location calculations and reject stale browser results by revision.
- [x] Add keyboard labels, visible focus, keyboard sidebar resizing, and compact laptop-height behavior.
- [ ] Owner accepts Package 3 before Package 4 terrain and Fresnel work begins.

#### Package 3 implementation evidence

- Release build succeeds with zero warnings and zero errors on Windows 10 x64.
- Automated tests: 58 pass across Core and App, including validated workbook activation/reload, invalid-replacement retention, structured route transfer, invalid coordinate rejection, fibre matching, and wireless candidate calculations.
- Browser JavaScript passes the syntax check. The normal workflow does not request Elevation; Package 4 will request it only when a candidate profile is opened.
- The source workbook path, Google API key, selected address, and coordinates are neither logged nor written to application preferences. Only the encrypted key and validated local workbook copy persist.
- Manual Package 3 screenshot and owner acceptance remain the blocking gate.

### Package 2 - Coverage domain and workbook validation

- [x] Add typed tower, fibre-range, dataset, manifest, validation-warning, and validation-error models.
- [x] Parse `.xlsx` files read-only with exact sheets, trimmed/reordered headers, original row numbers, and no Excel dependency.
- [x] Validate tower coordinates, height, range, speeds, AP direction, and AP coverage with future-safe sector rules.
- [x] Parse finite and `*` fibre civic ranges and normalize address case, punctuation, spacing, street types, city, and postal code.
- [x] Add Haversine distance, inclusive range filtering, deterministic candidate sorting, and sector filtering.
- [x] Return deterministic, sales-readable row and column errors while retaining valid zero-height/range rows as non-candidate warnings.
- [x] Owner accepts Package 2 before Package 3 interface work begins (September 14, 2026).

#### Package 2 evidence

- Release build: succeeded with zero warnings and zero errors on Windows 10 x64.
- Automated tests: 48 passed across Core and App. Package 2 tests cover reordered columns, missing and duplicate sheets/headers, whitespace, degree symbols, numeric text, blanks, zero and invalid values, coordinate boundaries, omnidirectional and sector behavior, fibre boundaries and conflicts, exact tower-range boundaries, cancellation, and file-read failures.
- Supplied workbook: the full Package 2 parser loaded 224 tower rows and 16 fibre rows read-only outside Git. It reported only the two expected non-blocking data warnings: zero tower height at workbook row 103 and zero tower range at workbook row 142.
- Isolation gate: every coverage result and validation rule is exercised in `SimpleServiceChecker.Core.Tests`, which references Core only and does not use WebView2 or Google.
- The source workbook, company Google key, searched locations, and real tower values remain outside the repository and test output.
- GitHub Actions independently restored, built, ran all 48 tests, checked JavaScript, and published the self-contained Windows proof successfully in pull request 2.
- Owner approved Package 2 on September 14, 2026.

Package 2 remains the accepted map-independent domain layer. Package 3 now connects it to the sales interface and Google map.

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
