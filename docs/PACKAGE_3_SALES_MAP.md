# Package 3 sales map

Status: Owner accepted after a live functional review on September 14, 2026. No screenshot evidence is claimed.

Package 3 connects the accepted coverage domain to the salesperson-facing desktop interface. Package 4 terrain and Fresnel work must not begin until the owner accepts this package.

## Primary workflow

After first-time setup, the application opens directly to the sales screen. A location can be supplied through Google Places, decimal latitude and longitude, a map click, or the draggable customer pin.

The native application calculates local results from the active workbook and returns them in this order:

1. fibre status for a complete civic address; and
2. distance-ordered wireless candidates within each tower's published range and AP sector.

Coordinates, clicks, and pin drags intentionally return `Address required` for fibre rather than guessing a civic address. They still produce local wireless results.

## Map behavior

- Every workbook tower is represented by a labelled point.
- Valid candidate-producing towers show a low-opacity published-range circle.
- Towers disabled by zero height or range remain visible as grey points without an active range.
- Selecting a location draws neutral candidate paths. Package 4 replaces the selected path with the accepted clearance colours after elevation is calculated.
- The legend uses labels and shapes as well as colour.
- Google Data-layer points avoid requiring every company to create a Google Map ID solely for markers.

## Local data and privacy

Choosing a workbook makes a readable temporary snapshot, validates the complete snapshot, and only then replaces `%LOCALAPPDATA%\SimpleServiceChecker\coverage\current.xlsx`. The companion manifest contains the source filename, hash, activation time, counts, and warning count; it does not contain the source directory.

An invalid replacement leaves the existing active workbook and in-memory dataset untouched. Package 5 adds explicit previous-copy restore, reload, open-current, and recovery controls.

The active customer address and coordinates exist only in memory. Clear removes the active browser state, and closing the application discards it. No search history, customer record, log, database, backend, or telemetry is created.

## Provider and failure states

- Without a workbook, the app opens Settings and makes no availability claim.
- Without Google, coordinate entry can still run local fibre-address-required and wireless checks.
- A Google load, restriction, billing, or quota failure is labelled as a provider error without displaying the key.
- Offline status explicitly says local coordinate checks remain available.
- A replacement workbook with blocking errors reports the safe validation messages and retains the active copy.
- Each location carries an increasing revision. Native cancellation and browser revision checks prevent an older result from replacing a newer selection.

## Manual acceptance checklist

- [ ] Start the app with the saved key and workbook; the sales screen appears without opening Settings.
- [ ] Confirm all expected tower points and range circles appear on the map.
- [ ] Select a complete Google address and confirm fibre is shown before wireless candidates.
- [ ] Enter decimal coordinates and confirm fibre says `Address required` while wireless results still load.
- [ ] Click the map and drag the customer pin; confirm each action refreshes paths and cards.
- [ ] Confirm each candidate shows name, distance, tower height, published range, maximum download/upload speeds, notes when present, and the Package 4 profile state.
- [ ] Select a second location quickly and confirm the first result never reappears.
- [ ] Disconnect networking after obtaining coordinates and confirm the local result remains readable.
- [ ] Resize the sidebar with mouse and keyboard and check a representative laptop window size.
- [ ] Choose an invalid workbook and confirm the prior active workbook remains available.
- [ ] Capture an acceptance screenshot containing no key, customer address, coordinate, or real tower name unless specifically approved.
