# Package 5 spreadsheet-management checklist

## Implemented workflow

- **Choose spreadsheet** remembers only the source file for the current application session.
- **Validate and activate** reads a local snapshot, validates the complete workbook, and changes coverage only after validation succeeds.
- **Reload** rereads the selected source. After restart, it asks the user to choose the source again rather than persisting its path.
- **Open current spreadsheet** opens a temporary read-only inspection copy so Excel cannot alter active coverage.
- **Restore previous** validates and restores the last known-good local copy. The displaced current copy becomes the new previous version, allowing the action to be reversed.

## Visible information

Settings show the active source filename, activation time, tower count, fibre-range count, warning count and details, stored schema version, and application schema version. Spreadsheet operations show progress and validation failures without hiding the active-copy status.

## Safety model

- The app never edits or watches the selected source workbook.
- The source path is not written to settings, manifests, logs, or the repository.
- Each accepted workbook has a SHA-256 integrity manifest containing only its filename, activation time, counts, schema, warning details, and content hash.
- Both current and previous local copies are retained under the existing per-user application-data directory.
- Replacement uses staged files and raw rollback copies. A rejected, locked, missing, or interrupted source cannot intentionally replace valid active coverage.
- Startup verifies workbook structure, counts, schema, and SHA-256. If the active pair is damaged and the previous pair is valid, the previous copy is recovered automatically and the user is notified.
- Abandoned staging and inspection files are cleaned on startup when Windows permits; an inspection copy still open in Excel is left for a later cleanup attempt.

## Automated evidence

Run on Windows 10 x64 on September 14, 2026:

```text
dotnet test SimpleServiceChecker.sln --no-restore -v:minimal
Core: 64 passed
App: 30 passed
Total: 94 passed

dotnet build SimpleServiceChecker.sln -c Release --no-restore -v:minimal
Build succeeded. 0 warnings, 0 errors.

node --check src/SimpleServiceChecker.App/Web/app.js
Passed with no syntax errors.

dotnet publish src/SimpleServiceChecker.App/SimpleServiceChecker.App.csproj --configuration Release --runtime win-x64 --self-contained true --no-restore --output artifacts/app
Publish succeeded.
```

## Owner acceptance gate

Before Package 6 begins, confirm in the review build that:

1. Choosing a spreadsheet does not immediately change active coverage.
2. **Validate and activate** shows progress and then the new filename, time, counts, warnings, and schema.
3. A deliberately invalid workbook reports its validation errors and leaves the current towers and fibre results active.
4. **Reload** refreshes an edited source during the same session and asks for the source again after application restart.
5. **Open current spreadsheet** opens an inspection copy that cannot modify active coverage.
6. After two valid activations, **Restore previous** returns to the prior data and still works after an application restart.
