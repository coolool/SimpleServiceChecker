# Map, Street View, and branding follow-up

## Owner-approved behaviour

- The user-facing product name is `Starling Service Checker` and the published executable is `StarlingServiceChecker.exe`.
- The map starts in satellite view.
- The map-type control allows roadmap, satellite, hybrid, and terrain views.
- The tower-load status appears directly below the Coverage map legend in the upper-right overlay.
- Selecting a customer location checks for official Google Street View imagery within 200 metres.
- The Street View action appears at the bottom of the Customer Location card only when imagery is available.
- Street View opens over the coverage map, initially faces the customer location, and can be closed to return to coverage.
- Opening a wireless profile closes Street View; opening Street View closes the wireless profile.
- Street View availability, panorama identifiers, addresses, and coordinates are not persisted.

## Compatibility boundary

Internal solution, project, namespace, local-data-folder, repository, and stable WebView2-origin identifiers retain `SimpleServiceChecker`. They are implementation identifiers rather than visible branding. Keeping them stable preserves existing encrypted settings, activated workbook data, and Google API key restrictions.

## Manual acceptance

1. Start with a saved Google key and workbook and confirm the main window and sidebar say `Starling Service Checker`.
2. Confirm the map initially uses satellite imagery.
3. Switch through roadmap, satellite, hybrid, and terrain views.
4. Choose a customer address with nearby Google imagery and confirm the Street View action appears at the bottom of the Customer Location card.
5. Open Street View, confirm it replaces the map, and use `Return to coverage map` or Google's close control to return.
6. Choose a remote customer location without imagery inside 200 metres and confirm the Street View action remains hidden.
7. Open a wireless profile and confirm the terrain plot overlays the coverage map and Street View is closed.
