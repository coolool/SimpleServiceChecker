# Starling Service Checker installation

## What you need

- A Windows 10 or Windows 11 x64 computer.
- Internet access during installation and whenever using Google Maps features.
- The signed `StarlingServiceChecker-Setup-<version>.exe` supplied through the company download page.
- Your company's restricted Google Maps Platform browser API key.
- The approved two-tab coverage workbook.

You do not need administrator access, Microsoft Excel, .NET, Git, a database, or developer tools.

## Install

1. Download the installer from the company SharePoint or OneDrive location.
2. Open the installer and confirm Windows shows the company's expected verified publisher. Stop and contact IT if it says **Unknown publisher** or shows a different publisher.
3. Continue through setup. The Start menu shortcut is automatic; the desktop shortcut is optional.
4. If Microsoft Edge WebView2 is not already present, setup downloads and installs Microsoft's Evergreen Runtime for the current user. Keep the computer online until setup finishes.
5. Launch **Starling Service Checker**.

If WebView2 cannot be installed, reconnect to the internet and rerun setup. If a company policy blocks it, contact IT rather than downloading software from an unapproved third-party site.

## First use

1. Open **Settings**.
2. Enter the company Google API key and select **Save**. The key is encrypted for your Windows user and is never included in the installer or repository.
3. Choose the approved coverage spreadsheet.
4. Select **Validate and activate**.
5. Confirm the displayed filename, activation time, tower/fibre counts, warnings, and schema.

## Updates

Open **Settings** and select **Check for updates**. The company download page opens in your default browser. Download and run the newer signed installer; it upgrades the existing installation and retains the current user's local settings and validated coverage.

## Uninstall

Use **Settings → Apps → Installed apps → Starling Service Checker → Uninstall**.

The uninstaller asks whether to remove this Windows user's saved API key, coverage copies, and browser data. Choose **No** if the app may be reinstalled and should retain its setup. Choose **Yes** only when those local files should be permanently removed.
