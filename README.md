# Starling Service Checker

Starling Service Checker is a low-maintenance Windows desktop application for sales staff to check likely fibre and fixed-wireless availability during a customer call.

## Status

Packages 1 through 6 and the map, Street View, Geocoding, legend, icon, branding, and terrain-minimize follow-ups are accepted. The exact `v1.0.0` draft installer has passed automated Windows lifecycle checks and an owner laptop smoke test. It remains unpublished in the separate public binary-only `coolool/StarlingServiceChecker-Releases` repository pending the final publication gate.

## Product boundaries

- Coverage comes from a user-selected Excel workbook with `Tower Coverage` and `Fibre Coverage` sheets.
- Each public release includes a blank, data-free coverage workbook with those required sheets and headers.
- The application does not require Excel, a database, a web server, or a company backend.
- Each company supplies and manages its own Google Maps Platform key.
- Fibre is shown as an address availability result; a fibre map overlay is intentionally excluded.
- Wireless plots include terrain, line clearance, and first Fresnel-zone clearance.
- Wireless results are planning candidates, not a guarantee that service can be installed.

See [Product scope](docs/PRODUCT_SCOPE.md), [first-release specification](docs/FIRST_RELEASE_SPEC.md), [build plan](docs/BUILD_PLAN.md), and [roadmap](docs/ROADMAP.md).

## Development

The repository pins .NET SDK 10.0.401. On Windows:

```powershell
dotnet restore SimpleServiceChecker.sln --locked-mode
dotnet build SimpleServiceChecker.sln --configuration Release --no-restore
dotnet test SimpleServiceChecker.sln --configuration Release --no-build
```

The development-only visual smoke check uses Node.js 24 and the installed Microsoft Edge browser. It renders synthetic application data at both supported desktop baselines and writes ignored proof images under `output/playwright`:

```powershell
$env:PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD = '1'
npm ci
npm run test:visual
```

Create the unsigned, self-contained Windows application build with:

```powershell
dotnet publish src\SimpleServiceChecker.App\SimpleServiceChecker.App.csproj --configuration Release --runtime win-x64 --self-contained true --no-restore --output artifacts\app
```

Create an unsigned per-user installer with Inno Setup 6 or 7 installed and internet access for the Microsoft WebView2 bootstrapper:

```powershell
.\build\Build-Installer.ps1 -Version 1.0.0
```

The application does not require Excel or a separately installed .NET runtime. The installer detects Microsoft Edge WebView2 Evergreen Runtime and silently runs Microsoft's signed online bootstrapper only when the runtime is missing.

See [Package 4 terrain checklist](docs/PACKAGE_4_TERRAIN.md) for the accepted terrain and Fresnel implementation.
See [Package 5 spreadsheet-management checklist](docs/PACKAGE_5_SPREADSHEET_MANAGEMENT.md) for the current review build and recovery evidence.
See [Package 6 installer and release checklist](docs/PACKAGE_6_INSTALLER_RELEASE.md) and the [salesperson installation guide](docs/SALESPERSON_INSTALLATION.md).
See [Package 7 release-candidate acceptance](docs/PACKAGE_7_RELEASE_ACCEPTANCE.md) for final-candidate evidence and publication gates.

## Repository safety

Do not commit populated coverage workbooks, API keys, credentials, customer information, generated installers, or local application data. The only workbook permitted for public distribution is the reviewed blank release template, which is stored in the separate binary-only release repository and pinned by SHA-256 in release automation.
