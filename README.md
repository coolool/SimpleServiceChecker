# Starling Service Checker

Starling Service Checker is a low-maintenance Windows desktop application for sales staff to check likely fibre and fixed-wireless availability during a customer call.

## Status

Packages 1 through 4 and the requested Package 4 visual and dropped-pin corrections are accepted. An owner-approved satellite-map, map-view, Street View, and Starling-branding follow-up is in review before Package 5 begins.

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

The application does not require Excel or a separately installed .NET runtime. It currently requires the Microsoft Edge WebView2 Evergreen Runtime; installer detection and bootstrap belong to Package 6.

See [Package 4 terrain checklist](docs/PACKAGE_4_TERRAIN.md) for the accepted terrain and Fresnel implementation.

## Repository safety

Do not commit coverage workbooks, API keys, credentials, customer information, generated installers, or local application data.
