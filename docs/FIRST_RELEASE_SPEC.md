# First-release specification

Status: Approved on September 11, 2026. Implementation has not started.

## 1. Outcome

SimpleServiceChecker lets a salesperson enter a customer location and quickly see:

- whether the workbook lists fibre at that civic address;
- which fixed-wireless towers include the location within their published range;
- the listed maximum speeds and distance for each candidate; and
- a familiar terrain, line-clearance, and first Fresnel-zone plot for a selected wireless candidate.

The user may also import supported official ISED spectrum files for an optional map reference layer. ISED records are not company coverage and never affect an availability result.

The application is a planning aid. It does not guarantee installation feasibility or service availability.

## 2. Release boundaries

Version 1 is a Windows desktop application. It has no database, hosted backend, authentication, telemetry, pin logging, customer records, ticketing, fibre overlay, automatic ISED downloader, or automatic cloud synchronization.

Coverage comes only from a locally selected Excel workbook. The application never edits the selected source workbook.

## 3. Proposed technology

- Windows Presentation Foundation on .NET 10 LTS.
- Microsoft Edge WebView2 Evergreen for the map and existing web-style interface.
- A self-contained x64 application publish so the user does not install .NET separately.
- A maintained .NET Excel library for read-only `.xlsx` parsing; Excel is not required.
- Plain HTML, CSS, and JavaScript modules in the embedded view. Avoid a large front-end framework unless the proof demonstrates a concrete need.
- Chart.js packaged with the application for terrain profiles; no runtime CDN dependency.
- A simple per-user Windows installer. Version 1 uses a signed installer downloaded manually rather than an automatic updater.

The Google proof must validate an HTTPS virtual-host origin in WebView2 before this architecture is accepted. If Google does not consistently receive an authorizable referrer, stop and revise the hosting approach rather than requiring an unrestricted key.

## 4. Main window

Preserve the familiar Service Checker visual character:

- resizable warm-coloured sidebar on the left;
- Google map filling the remaining window;
- address and coordinate search at the top;
- fibre summary before wireless candidates;
- tower result cards with distance, height, range, maximum speeds, and plot;
- coloured line-clearance paths and the existing clearance legend;
- an optional, visually distinct ISED reference-layer toggle when data has been imported; and
- responsive behaviour for smaller laptop screens.

Remove ticket, customer, heatmap, BMS, logging, and administrative controls.

## 5. Location workflow

The user can select a location by:

- Google Places address search;
- pasting decimal latitude and longitude; or
- clicking or dragging a pin on the map.

An address selection retains structured Google address components for fibre matching. A raw map pin or coordinate without a reliable civic address returns `Fibre: Address required` rather than guessing.

After location selection, local distance and coverage calculations appear immediately. Google elevation work runs asynchronously and cannot block the initial availability summary.

## 6. Fibre result

The workbook is authoritative for fibre availability.

Matching uses normalized civic number, street name, street type, city, and postal code where available. Matching is case-insensitive and ignores harmless punctuation and spacing differences. Common street-type abbreviations are normalized through a small tested mapping.

- `Start_Civic_Num` is inclusive.
- A numeric `End_Civic_Num` is inclusive.
- `*` means no upper civic-number limit.
- A row must match street and city; postal code is an additional discriminator when populated.
- If more than one row matches, select the most specific civic range and display a validation warning if the matches conflict.
- `Max_Speeds` is displayed exactly as supplied after format validation.

Results are `Available`, `Not listed`, `Address required`, or `Data error`. The wording must make clear that `Not listed` means the active workbook contains no match.

## 7. Wireless result

For every valid tower row, calculate great-circle distance from the selected location. A tower is a candidate when the distance is less than or equal to `Tower Range`.

Candidate cards are ordered by distance. Each card shows:

- tower name;
- distance;
- tower height and published range;
- maximum download and upload speeds;
- plot state; and
- workbook notes when present.

Coverage circles and candidate lines appear on the map. The application treats blank `AP Direction` with 360-degree `AP Coverage` as omnidirectional. If a future row has coverage below 360 degrees, direction becomes required and the application may apply sector filtering only after that behaviour has dedicated acceptance tests.

Terrain and line-clearance calculations should reproduce the existing Service Checker inputs, colours, labels, and chart appearance. Add the complete first Fresnel-zone envelope and a clearance reference with these defaults:

- frequency: 5.8 GHz;
- Fresnel clearance reference: 60%; and
- effective-earth-radius factor: 4/3.

Frequency and clearance percentage are adjustable for the selected plot. The earth model is visible and adjustable between standard 4/3, geometric earth, and flat-earth comparison. The profile must label all assumptions and remain a planning aid; it does not add a link budget, diffraction model, antenna pattern, or installation guarantee.

For a path of total length `D`, point distances `d1` and `d2`, wavelength `lambda`, and effective Earth radius `R`, use:

- first Fresnel radius: `sqrt(lambda * d1 * d2 / D)`; and
- curvature bulge: `d1 * d2 / (2 * R)`.

Reuse the tested TXfinder calculation behaviour where practical, with independent unit tests in this repository.

## 8. Workbook contract

Required sheet names:

- `Tower Coverage`
- `Fibre Coverage`

Required tower columns:

- `Tower Name`
- `Tower Height`
- `Tower Range`
- `Max Download (Mbps)`
- `Max Upload (Mbps)`
- `AP Direction (°)`
- `AP Coverage (°)`
- `Lat`
- `lon`
- `Notes`

Required fibre columns:

- `Start_Civic_Num`
- `End_Civic_Num`
- `Street_Name`
- `Street_Type`
- `City`
- `Postal_Code`
- `Max_Speeds`
- `Notes`

Column order may vary, but names must match after trimming surrounding whitespace. Unknown extra columns are retained in the source file but ignored by version 1.

Validation failures include missing sheets or columns, unreadable files, duplicate headers, invalid coordinates, and non-numeric required coverage values. Zero tower height or range is a warning and that row cannot produce a normal candidate result. Blank notes and omnidirectional AP direction are permitted.

## 9. Spreadsheet management

The Data screen provides:

- `Choose Spreadsheet`;
- `Validate and Activate`;
- `Reload`;
- `Open Current Spreadsheet`;
- `Restore Previous`; and
- current filename, activation time, counts, and warnings.

Activation sequence:

1. Read the chosen workbook without modifying it.
2. Validate all required structure and rows.
3. Present record counts, errors, and warnings.
4. Require zero blocking errors before activation.
5. Copy the validated workbook to the user's local application-data directory using a temporary name.
6. Atomically replace the active local copy and retain one previous valid copy.
7. Load the new dataset into memory and show the active filename and timestamp.

An invalid or interrupted replacement leaves the current dataset active. The application does not watch OneDrive or network paths automatically in version 1.

## 10. Google configuration

Each company supplies its own Google Maps Platform project and credentials. First-run setup collects:

- browser API key;
- Map ID if required by the selected marker implementation; and
- default map centre.

The required Google services are Maps JavaScript API, Places API (New), and Elevation API. The application tests map loading, place search, and one bounded elevation request before reporting setup complete.

The key is never committed, logged, added to crash output, or included in an installer. Store it encrypted for the current Windows user using Windows Data Protection API. The key is necessarily delivered to the embedded map at runtime and is not a server secret. Setup must instruct the company to apply API restrictions and the tested application/referrer restriction.

## 11. Local storage

Use the current user's local application-data directory for:

- active and previous validated workbook copies;
- encrypted Google configuration;
- non-sensitive UI preferences; and
- application version metadata.

Do not create a database. Do not store searched addresses, coordinates, map history, or usage analytics after the active view is cleared or the application closes.

## 12. Error and offline behaviour

- Workbook unavailable: continue using the last validated local copy.
- Invalid replacement: show row-specific errors and keep the active copy.
- Google unavailable: retain local fibre and tower-range results when a location is already known; clearly mark map and elevation as unavailable.
- Places unavailable: keep coordinate entry and map-pin selection when the map remains usable.
- Elevation unavailable: show candidate towers without presenting a clearance result.
- No workbook: open the Data setup screen and disable availability claims.
- No Google key: allow workbook validation but disable Google-dependent location and plot functions.
- No ISED import: omit or disable the ISED layer without affecting normal service checks.
- Invalid or interrupted ISED import: keep the previous validated ISED cache active and show a clear import error.

## 13. Optional ISED reference layer

ISED support is an optional file-import feature and is not a coverage source.

The Data screen provides an `ISED Reference Data` section with:

- `Open official ISED download page`;
- `Import ISED Files`;
- current source names, data dates, record counts, and validation warnings;
- `Replace ISED Data`; and
- `Remove ISED Data`.

The first supported inputs are the official All Services radio-authorization extract (`TAFL_LTAF.zip`) and Terrestrial Spectrum Licence site-data extract (`Site_Data_Extract_FX.zip`). The importer validates the expected archive contents and schemas before replacing active data.

Because these source files can contain very large numbers of regulatory records, import processing must stream records and produce disposable, versioned local cache files with a small manifest. The application must not create SQLite, another database, or a continuously running index service. Map display must use clustering or viewport-bounded loading so the full dataset is never rendered at once.

ISED markers use a distinct colour and legend. Marker details identify the source and available authorization fields. Every ISED screen states that the records are regulatory references, may be duplicated at one physical coordinate, and do not indicate company service availability.

The application does not automatically download, refresh, or schedule ISED updates. The user controls when files are downloaded from ISED and imported. Removing ISED data deletes only the derived local cache and does not touch the user's downloaded ZIP files.

## 14. Performance targets

On a representative supported business laptop:

- open the main window within five seconds after first installation setup;
- load the supplied workbook within two seconds;
- show local fibre and tower-range results within one second of receiving coordinates;
- remain responsive while elevation plots load;
- support at least 5,000 tower rows and 25,000 fibre rows without a database; and
- keep the map responsive while optional ISED records are loaded only for the visible viewport.

These are acceptance targets, not claims until measured.

## 15. Accessibility and usability

- Complete the primary workflow with keyboard and mouse.
- Provide visible focus and labelled controls.
- Do not communicate availability by colour alone.
- Keep primary result wording understandable without engineering knowledge.
- Put setup and data-management controls behind a simple settings button so the normal sales screen remains uncluttered.

## 16. Verification

Automated tests cover workbook parsing, header normalization, validation, fibre matching, open-ended civic ranges, distance calculations, candidate sorting, persisted-setting boundaries, Fresnel and earth-curvature calculations, and JavaScript plot calculations.

Manual acceptance covers:

- clean Windows 10 and Windows 11 x64 installations;
- installation without Excel or a development runtime;
- first-run Google and workbook setup;
- valid, invalid, locked, moved, and replacement workbooks;
- allowed and rejected Google key restrictions;
- address, coordinate, click, and drag workflows;
- representative fibre matches and non-matches;
- multiple, single, and zero wireless candidates;
- Fresnel plots at known distances and frequencies, including default 60% clearance;
- valid, malformed, interrupted, and large ISED imports, plus cache replacement and removal;
- confirmation that ISED markers never change fibre or wireless availability results;
- Google timeout, offline, and quota/provider errors;
- map, line, legend, and terrain-chart visual comparison with Service Checker;
- upgrade, rollback, and uninstall; and
- confirmation that no workbook, key, searched address, or generated installer entered Git history.

## 17. Distribution and updates

GitHub Actions produces a normal signed Windows installer. A salesperson does not need Git, GitHub Desktop, .NET, Node.js, Excel, or any developer tools: they download the installer, run it, and follow a standard setup wizard.

Manual releases mean the installed application does not silently update itself. A `Check for updates` command opens the fixed latest-release page, and the user downloads and runs a newer installer when one is available. The existing installation continues to work until then.

Because the source repository is private, its GitHub Releases page is convenient only for recipients who have been granted repository access. General distribution uses a company-controlled SharePoint or OneDrive download link while keeping source code private.

Code signing is required before general distribution. Without it, Windows can show an `Unknown publisher` warning even when the installer is legitimate.

## 18. GitHub and releases

- `main` remains releasable and protected through reviewed pull requests once implementation starts.
- Every code-changing pull request updates `docs/ROADMAP.md` and its acceptance evidence.
- GitHub Actions builds and tests on Windows without real API keys or coverage data.
- Tests use synthetic workbook fixtures only.
- Version tags create draft release artifacts for manual review.
- General distribution waits for code signing and clean-machine acceptance.

## 19. Approved decisions

1. Windows 10/11 x64 is the version 1 support target.
2. The application package is self-contained.
3. .NET 10 WPF and WebView2 are the technical direction.
4. Wireless plots include Fresnel-zone clearance.
5. Optional ISED data is manually imported and remains separate from coverage.
6. Workbook validation and availability wording follow this specification.
7. Signed installers are distributed through a company-controlled SharePoint or OneDrive link.
