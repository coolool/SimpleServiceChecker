[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$CurrentInstaller,
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$CurrentVersion,
    [string]$PreviousInstaller,
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$PreviousVersion,
    [switch]$VerifySignatures,
    [string]$SignToolPath,
    [ValidatePattern('^[0-9A-Fa-f]{40}$')]
    [string]$ExpectedSignerThumbprint,
    [switch]$AllowUntrustedTestCertificate
)

$ErrorActionPreference = 'Stop'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) "StarlingServiceChecker-InstallerTest-$([Guid]::NewGuid().ToString('N'))"
$expectedExecutable = Join-Path $testRoot 'StarlingServiceChecker.exe'

function Invoke-Installer {
    param([string]$Path)

    $logPath = Join-Path ([IO.Path]::GetTempPath()) "StarlingServiceChecker-Installer-$([Guid]::NewGuid().ToString('N')).log"
    try {
        $arguments = @(
            '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/NOICONS', '/CLOSEAPPLICATIONS',
            "/DIR=$testRoot", "/LOG=$logPath")
        $process = Start-Process -FilePath $Path -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
        if ($process.ExitCode -ne 0) {
            $diagnostics = if (Test-Path -LiteralPath $logPath) {
                @(Select-String -LiteralPath $logPath -Pattern 'Fatal exception', 'Exception message:', 'Error ', 'failed; code' -SimpleMatch |
                    Select-Object -Last 8 |
                    ForEach-Object { $_.Line.Trim() })
            }
            else {
                @()
            }
            $suffix = if ($diagnostics.Count -gt 0) { " Diagnostics: $($diagnostics -join ' | ')" } else { '' }
            throw "Installer $Path failed with exit code $($process.ExitCode).$suffix"
        }
    }
    finally {
        Remove-Item -LiteralPath $logPath -Force -ErrorAction SilentlyContinue
    }
}

function Assert-InstalledVersion {
    param([string]$ExpectedVersion)

    if (-not (Test-Path -LiteralPath $expectedExecutable)) {
        throw "Installed executable was not found at $expectedExecutable"
    }
    $actualVersion = (Get-Item -LiteralPath $expectedExecutable).VersionInfo.ProductVersion
    if (-not $actualVersion.StartsWith($ExpectedVersion, [StringComparison]::Ordinal)) {
        throw "Expected installed version $ExpectedVersion but found $actualVersion."
    }
}

function Assert-Signed {
    param([string]$Path)

    if ($AllowUntrustedTestCertificate) {
        if ([string]::IsNullOrWhiteSpace($ExpectedSignerThumbprint)) {
            throw 'ExpectedSignerThumbprint is required for untrusted test-certificate verification.'
        }
        $signature = Get-AuthenticodeSignature -LiteralPath $Path
        if ($null -eq $signature.SignerCertificate -or
            $signature.SignerCertificate.Thumbprint -ne $ExpectedSignerThumbprint -or
            $signature.Status -in @(
                [Management.Automation.SignatureStatus]::NotSigned,
                [Management.Automation.SignatureStatus]::HashMismatch,
                [Management.Automation.SignatureStatus]::NotSupportedFileFormat,
                [Management.Automation.SignatureStatus]::Incompatible)) {
            throw "Ephemeral Authenticode verification failed for $Path with status $($signature.Status)."
        }
        return
    }

    if ([string]::IsNullOrWhiteSpace($SignToolPath)) {
        throw 'SignToolPath is required when VerifySignatures is set.'
    }
    & $SignToolPath verify /pa /all $Path
    if ($LASTEXITCODE -ne 0) {
        throw "Authenticode verification failed for $Path"
    }
}

try {
    if (-not (Test-Path -LiteralPath $CurrentInstaller)) {
        throw "Current installer not found: $CurrentInstaller"
    }
    if (-not [string]::IsNullOrWhiteSpace($PreviousInstaller) -and -not (Test-Path -LiteralPath $PreviousInstaller)) {
        throw "Previous installer not found: $PreviousInstaller"
    }

    if ($VerifySignatures) {
        Assert-Signed $CurrentInstaller
    }

    if (-not [string]::IsNullOrWhiteSpace($PreviousInstaller)) {
        if ([string]::IsNullOrWhiteSpace($PreviousVersion)) {
            throw 'PreviousVersion is required with PreviousInstaller.'
        }
        Invoke-Installer $PreviousInstaller
        Assert-InstalledVersion $PreviousVersion
    }

    Invoke-Installer $CurrentInstaller
    Assert-InstalledVersion $CurrentVersion

    Invoke-Installer $CurrentInstaller
    Assert-InstalledVersion $CurrentVersion

    if (-not [string]::IsNullOrWhiteSpace($PreviousInstaller)) {
        Invoke-Installer $PreviousInstaller
        Assert-InstalledVersion $PreviousVersion
    }

    $uninstaller = Get-ChildItem -LiteralPath $testRoot -Filter 'unins*.exe' -File | Select-Object -First 1
    if ($null -eq $uninstaller) {
        throw 'The installer did not create an uninstaller.'
    }
    if ($VerifySignatures) {
        Assert-Signed $expectedExecutable
        Assert-Signed $uninstaller.FullName
    }

    $uninstall = Start-Process -FilePath $uninstaller.FullName -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') -Wait -PassThru -WindowStyle Hidden
    if ($uninstall.ExitCode -ne 0) {
        throw "Uninstaller failed with exit code $($uninstall.ExitCode)."
    }
    if (Test-Path -LiteralPath $expectedExecutable) {
        throw 'The application executable remained after uninstall.'
    }

    Write-Output 'Installer lifecycle passed: install, same-version upgrade, version upgrade/rollback when supplied, and uninstall.'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
