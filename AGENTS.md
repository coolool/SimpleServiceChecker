# SimpleServiceChecker project rules

- Read `README.md`, `docs/PRODUCT_SCOPE.md`, `docs/FIRST_RELEASE_SPEC.md`, `docs/BUILD_PLAN.md`, and `docs/ROADMAP.md` before changing code.
- Do not begin implementation without explicit owner authorization.
- Keep coverage spreadsheet driven. Do not add a database, hosted backend, authentication service, or telemetry.
- Do not commit coverage workbooks, API keys, credentials, customer data, logs, or local application data.
- Preserve the familiar Service Checker map, sidebar, result-card, legend, and terrain-profile appearance while simplifying the workflow for sales calls.
- Treat wireless results as planning candidates rather than guaranteed availability.
- Validate a replacement workbook completely before activating it, and retain the last known-good local copy for rollback.
- Keep Google integration isolated behind a provider adapter and require each company to configure its own restricted key.
- Fibre address availability is in scope. Fibre map geometry and overlays are out of scope.
- Include a first Fresnel-zone envelope in wireless plots, defaulting to 5.8 GHz, 60% clearance, and effective-earth-radius factor 4/3; label the assumptions and keep them adjustable.
- Keep optional, user-imported ISED records in a separate reference layer. Do not use them to infer company coverage, and do not add an automatic downloader, database, scheduler, or backend for them.
- Follow the package order and blocking gates in `docs/BUILD_PLAN.md`; do not continue past a failed gate without owner approval of a revised approach.
- Keep changes small, tested, and documented in `docs/ROADMAP.md`.
