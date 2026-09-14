# Package 4 terrain and Fresnel profile

Package 4 adds an on-demand wireless path profile to the accepted sales map. Package 5 must not begin until the owner accepts this package.

## Request boundary

Local fibre and wireless candidates still appear before any elevation work. Google Elevation is called only after `Plot terrain & Fresnel` or `Refresh elevation` is selected for one candidate.

The sample plan targets no more than 30 metres between samples, keeps an exact midpoint with an odd count, and stays between 33 and 511 samples. This is within Google's documented 2–512 range for `getElevationAlongPath`: <https://developers.google.com/maps/documentation/javascript/reference/elevation>.

Only one physical request can be in flight. A 15-second timeout suppresses delivery but does not pretend the Google request was cancelled or allow another request to overlap it. Changed locations, towers, settings, and closed profiles invalidate older display revisions.

## Validated calculation

Google supplies only locations, elevations, and optional interpolation resolution. Core validates the exact count, finite values, coordinate ranges, elevation ranges, and each returned location against the requested great-circle path before producing an assessment.

For total horizontal path length `D`, distance `d1` from the customer, distance `d2 = D - d1` from the tower, wavelength `lambda`, and effective Earth radius `R`:

- full first Fresnel radius is `sqrt(lambda * d1 * d2 / D)`;
- Earth-curvature bulge is `d1 * d2 / (2 * R)`;
- the endpoint ray joins the entered customer and tower antenna elevations;
- terrain is the Google elevation plus the selected curvature bulge;
- clutter top is adjusted terrain plus the global clutter-height assumption;
- line clearance is ray minus clutter top; and
- selected Fresnel margin is line clearance minus the selected percentage of the full radius.

The default model is 5.8 GHz, 60% Fresnel reference, 4/3 effective Earth radius, 25 ft customer height, workbook tower height, and 50 ft global clutter. The user can adjust both endpoint heights, clutter, frequency, percentage, and Earth model. Adjustments reuse current elevation samples and make no new provider call; `Refresh elevation` is explicit.

## Display

The tall Chart.js profile appears as a closeable overlay over the map and shows:

- curvature-adjusted terrain;
- the labelled global clutter assumption;
- endpoint line of sight;
- the complete first Fresnel envelope; and
- the selected percentage reference.

The selected map path is split into the established clearance bands:

- black: obstruction or contact;
- red: positive clearance through 3.048 m / 10 ft;
- orange: through 9.144 m / 30 ft;
- yellow: through 15.24 m / 50 ft; and
- green: more than 15.24 m / 50 ft.

Text states whether sampled line of sight and the selected Fresnel reference are clear, so colour is never the only result. The summary labels provider, assumptions, sampling, interpolation resolution, and the planning-only limitations.

## Manual acceptance checklist

- [ ] Select a location with at least one wireless candidate and confirm local results appear before elevation is requested.
- [ ] Open a candidate profile and confirm the tall chart overlays the map without reflowing it or hiding Google attribution.
- [ ] Confirm terrain, clutter, line of sight, full Fresnel envelope, selected reference, labels, units, and summary are readable.
- [ ] Confirm the selected map path uses the five approved clearance colours and the visible clearance legend.
- [ ] Change frequency and Fresnel percentage; confirm the plot recalculates without showing another elevation request.
- [ ] Change customer height, tower height, clutter, and Earth model; confirm the profile and non-colour status refresh.
- [ ] Select another location while a profile is loading; confirm the old result never appears.
- [ ] Confirm quota/provider/offline failure wording leaves candidate cards readable and does not show raw diagnostics.
- [ ] Capture an acceptance screenshot containing no key, customer address, coordinate, or real tower name unless specifically approved.
