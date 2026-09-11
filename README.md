# SimpleServiceChecker

SimpleServiceChecker is a low-maintenance Windows desktop application for sales staff to check likely fibre and fixed-wireless availability during a customer call.

## Status

Planning and repository setup only. Application development has not started.

## Product boundaries

- Coverage comes from a user-selected Excel workbook with `Tower Coverage` and `Fibre Coverage` sheets.
- The application does not require Excel, a database, a web server, or a company backend.
- Each company supplies and manages its own Google Maps Platform key.
- Fibre is shown as an address availability result; a fibre map overlay is intentionally excluded.
- Wireless results are planning candidates, not a guarantee that service can be installed.

See [Product scope](docs/PRODUCT_SCOPE.md) and [Roadmap](docs/ROADMAP.md).

## Repository safety

Do not commit coverage workbooks, API keys, credentials, customer information, generated installers, or local application data.
