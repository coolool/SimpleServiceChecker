# Package 1 proof checklist

## Purpose

Package 1 proves the Windows shell, secure local origin, Google integrations, spreadsheet contract, bundled chart, and self-contained publish before production sales workflow work begins.

This is an unsigned technical proof, not an installer or a production release.

## Google Cloud setup

Use a company-owned Google Maps Platform project with billing configured. Enable only:

- Maps JavaScript API
- Places API (New)
- Elevation API

Restrict the key to those three APIs. Set its application restriction to Websites and allow this exact application referrer:

```text
https://app.simpleservicechecker.local/*
```

Do not paste the key into GitHub, a task, a screenshot, or a log. Enter it only in the running application's masked `Company API key` field.

## Owner proof

1. Launch the self-contained `StarlingServiceChecker.exe` proof.
2. Confirm the warm sidebar, map area, and synthetic terrain/Fresnel chart render without a network request for Chart.js.
3. Enter the restricted company key and select `Save & test`.
4. Confirm Maps JavaScript, Places (New), and Elevation each show a green indicator.
5. Search a non-sensitive public address and confirm the map pans to it.
6. Choose the supplied coverage workbook from outside the repository.
7. Confirm the application reports 224 tower rows and 16 fibre rows.
8. Close and reopen the proof. Confirm the key remains configured for the same Windows user.
9. Remove the saved key and confirm it is no longer configured after another restart.

Google-connected features send map, place-search, and synthetic elevation-path requests to Google. Coverage workbook data remains local and the proof does not send workbook rows to Google.

## Acceptance evidence

Record results in `docs/ROADMAP.md`. Package 2 must not start until:

- the three restricted-key checks pass through `https://app.simpleservicechecker.local`;
- the supplied workbook passes outside Git;
- the shell, map, and chart are reviewed on representative Windows 10 and Windows 11 x64 computers;
- repository and output checks find no real workbook or key; and
- the owner explicitly accepts the proof.
