# Package 2 coverage domain

Package 2 turns the two-sheet workbook into validated, typed in-memory coverage data. It has no WebView2, Google, WPF, database, backend, logging, or local persistence dependency.

## Workbook result

`CoverageWorkbookReader` opens the selected `.xlsx` file read-only with file sharing enabled. It returns either:

- a `CoverageDataset` containing typed tower and fibre records plus non-blocking warnings; or
- blocking `WorkbookValidationIssue` records with stable codes, severity, source sheet, original Excel row, column, and sales-readable message.

The reader never edits the source file. It reports only the source filename, not its directory. A cancelled read throws `OperationCanceledException`; an unreadable file returns a safe `FileReadFailed` issue without exposing a local path.

## Tower rules

- Latitude includes `-90` through `90`; longitude includes `-180` through `180`.
- Height and published range use feet and kilometres as supplied. Negative values are errors.
- Zero height or range is retained with a warning and cannot produce a normal wireless candidate.
- Download and upload speeds must be positive numbers.
- AP coverage must be greater than `0` and no more than `360` degrees.
- Blank AP direction is valid for 360-degree coverage. Narrower coverage requires a direction.
- Direction `360` normalizes to north (`0`). Sector boundaries are inclusive and handle the north/zero wrap.

`WirelessCandidateFinder` uses Haversine distance. A location exactly equal to the published tower range remains a candidate. Results sort by distance, then tower name and source row for deterministic ties.

## Fibre rules

- Civic start and numeric end values are inclusive whole numbers of zero or greater.
- `*` is stored as an open-ended range.
- Street name, street type, and city are required. Postal code remains optional.
- Matching ignores case, harmless punctuation, and spacing and normalizes common street-type abbreviations.
- A populated address and workbook postal code must agree; a missing postal code does not create a guessed mismatch.
- Overlapping rows choose the narrowest civic range. Conflicting service details produce a visible match warning.
- `Max_Speeds` preserves the supplied display text after format validation.

## Package boundary

Package 3 now consumes the accepted `CoverageDataset`, while Package 2 itself deliberately retains no map or interface dependency.
