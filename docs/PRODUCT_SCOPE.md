# Product scope

## Purpose

Give a salesperson a fast, understandable view of likely fibre and fixed-wireless availability at a customer location while on a call.

## Primary workflow

1. Search an address, enter latitude and longitude, or place a map pin.
2. Show the fibre address-range result and maximum listed speed.
3. Show fixed-wireless towers whose published range includes the location.
4. Draw the location, tower ranges, and candidate paths on Google Maps.
5. Show the familiar terrain, line-clearance, and first Fresnel-zone profile for a selected wireless candidate.
6. Optionally show user-imported official ISED spectrum records as a clearly separate reference layer.

## Coverage source

The source of truth is one user-selected Excel workbook with exactly two required sheets:

- `Tower Coverage`
- `Fibre Coverage`

The application reads coverage into memory. It does not edit the selected source workbook or persist coverage in a database.

The interface provides:

- Choose Spreadsheet
- Validate and Activate
- Reload Spreadsheet
- Open Current Spreadsheet
- Restore Previous Spreadsheet
- Current filename, activation time, record counts, and validation warnings

A new workbook becomes active only after complete validation. The application keeps the previous valid local copy for rollback.

## Google Maps ownership

Each company creates and manages its own Google Maps Platform account, key, enabled APIs, quotas, and billing. The application provides a first-run setup and diagnostics screen. No key is shipped with the application or committed to Git.

Google-connected features require internet access. Coverage data remains local, but searched locations and elevation paths are sent to Google services as required by the enabled map features.

## Optional ISED reference data

The user may open the official ISED download page, download supported spectrum-data ZIP files, and import them manually. The application validates and converts accepted records into ordinary versioned local cache files for responsive viewport display. It does not create a database, automatically download updates, or run a background synchronization service.

ISED records are regulatory reference data, not company tower inventory or proof of service availability. They use a visually distinct layer, can be hidden or removed, and never participate in fibre or wireless availability results.

## Explicitly excluded

- Fibre map overlay or route geometry
- Databases
- Hosted backend or web server
- User accounts and authentication
- BMS integration
- Automatic ISED downloading or synchronization
- Customer-record imports
- Sonar ticket creation
- Heatmaps
- Pin or usage logging
- Telemetry
- Automatic cloud synchronization
- Spreadsheet editing inside the application

## Product language

Fixed-wireless output must say `Wireless candidate` or `Within published coverage range`. It must not imply that a radius or terrain model guarantees installation.

## Supported platform target

Initial target: supported 64-bit Windows 10 and Windows 11 desktops. Other operating systems and legacy Windows releases are outside the first release.
