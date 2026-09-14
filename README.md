# Starling Service Checker

Starling Service Checker is a low-maintenance Windows desktop application for sales staff to check likely fibre and fixed-wireless availability during a customer call.

## Status

Packages 1 through 5 and the map, Street View, Geocoding, legend, icon, branding, and terrain-minimize follow-ups are accepted. Package 6's unsigned installer proof is passing automated and local lifecycle checks; production signing, clean-machine testing, and owner acceptance remain open.

## Product boundaries

- Coverage comes from a user-selected Excel workbook with `Tower Coverage` and `Fibre Coverage` sheets.
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

## Repository safety

Do not commit coverage workbooks, API keys, credentials, customer information, generated installers, or local application data.
