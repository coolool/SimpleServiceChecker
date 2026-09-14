[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0',
    [ValidatePattern('^https://')]
    [string]$UpdateLocationUrl = 'https://github.com/coolool/SimpleServiceChecker/releases',
    [string]$PublishDirectory,
    [string]$OutputDirectory,
    [string]$WebView2BootstrapperPath,
    [switch]$SkipRestore,
    [string]$SignToolPath,
    [string]$SigningCertificateThumbprint,
    [ValidatePattern('^https://')]
    [string]$TimestampUrl
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))

function Resolve-ArtifactPath {
    param([string]$Path, [string]$DefaultRelativePath)

    $candidate = if ([string]::IsNullOrWhiteSpace($Path)) {
        Join-Path $artifactRoot $DefaultRelativePath
    }
    elseif ([IO.Path]::IsPathRooted($Path)) {
        $Path
    }
    else {
        Join-Path $repoRoot $Path
    }

    $resolved = [IO.Path]::GetFullPath($candidate)
    $artifactPrefix = $artifactRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Generated output must remain inside $artifactRoot"
    }

    return $resolved
}

function Reset-GeneratedDirectory {
    param([string]$Path)

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
    $null = New-Item -ItemType Directory -Path $Path -Force
}

function Invoke-Checked {
    param([string]$Command, [string[]]$Arguments)

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Command failed with exit code $LASTEXITCODE."
    }
}

function Find-InnoCompiler {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $candidates = @(
        (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    throw 'Inno Setup command-line compiler ISCC.exe was not found. Install Inno Setup 6 or 7, then retry.'
}

function Assert-MicrosoftBootstrapper {
    param([string]$Path)

    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne [Management.Automation.SignatureStatus]::Valid -or
        $null -eq $signature.SignerCertificate -or
        $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
        throw 'The WebView2 bootstrapper does not have a valid Microsoft Authenticode signature.'
    }
}

function Invoke-Sign {
    param([string]$Path)

    Invoke-Checked $SignToolPath @(
        'sign', '/sha1', $SigningCertificateThumbprint,
        '/fd', 'SHA256', '/td', 'SHA256', '/tr', $TimestampUrl,
        '/d', 'Starling Service Checker', $Path)
    Invoke-Checked $SignToolPath @('verify', '/pa', '/all', $Path)
}

$publishPath = Resolve-ArtifactPath $PublishDirectory 'publish\win-x64'
$outputPath = Resolve-ArtifactPath $OutputDirectory 'installer'
$bootstrapperPath = Resolve-ArtifactPath $WebView2BootstrapperPath 'prerequisites\MicrosoftEdgeWebview2Setup.exe'
$bootstrapperDirectory = Split-Path -Parent $bootstrapperPath
$signingRequested = -not [string]::IsNullOrWhiteSpace($SignToolPath) -or
    -not [string]::IsNullOrWhiteSpace($SigningCertificateThumbprint) -or
    -not [string]::IsNullOrWhiteSpace($TimestampUrl)
if ($signingRequested -and (
    [string]::IsNullOrWhiteSpace($SignToolPath) -or
    [string]::IsNullOrWhiteSpace($SigningCertificateThumbprint) -or
    [string]::IsNullOrWhiteSpace($TimestampUrl))) {
    throw 'SignToolPath, SigningCertificateThumbprint, and TimestampUrl must all be supplied for a signed build.'
}

Reset-GeneratedDirectory $publishPath
Reset-GeneratedDirectory $outputPath
$null = New-Item -ItemType Directory -Path $bootstrapperDirectory -Force

Push-Location $repoRoot
try {
    if (-not $SkipRestore) {
        Invoke-Checked 'dotnet' @('restore', 'SimpleServiceChecker.sln', '--locked-mode')
    }

    Invoke-Checked 'dotnet' @(
        'publish', 'src\SimpleServiceChecker.App\SimpleServiceChecker.App.csproj',
        '--configuration', 'Release', '--runtime', 'win-x64', '--self-contained', 'true',
        '--no-restore', '--output', $publishPath,
        "/p:Version=$Version", "/p:FileVersion=$Version.0", "/p:InformationalVersion=$Version",
        "/p:UpdateLocationUrl=$UpdateLocationUrl")

    $applicationExecutable = Join-Path $publishPath 'StarlingServiceChecker.exe'
    $applicationDll = Join-Path $publishPath 'StarlingServiceChecker.dll'
    $coreDll = Join-Path $publishPath 'SimpleServiceChecker.Core.dll'
    foreach ($requiredFile in @($applicationExecutable, $applicationDll, $coreDll)) {
        if (-not (Test-Path -LiteralPath $requiredFile)) {
            throw "Required published file is missing: $requiredFile"
        }
    }

    $forbiddenFiles = Get-ChildItem -LiteralPath $publishPath -Recurse -File | Where-Object {
        $_.Extension -in @('.xlsx', '.xls', '.csv', '.key', '.pfx') -or
        $_.Name -in @('google-key.dat', 'current-manifest.json', 'previous-manifest.json')
    }
    if ($forbiddenFiles.Count -gt 0) {
        throw "Sensitive or local-data files were found in the publish output: $($forbiddenFiles.Name -join ', ')"
    }

    if (-not (Test-Path -LiteralPath $bootstrapperPath)) {
        Invoke-WebRequest -Uri 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' -OutFile $bootstrapperPath
    }
    Assert-MicrosoftBootstrapper $bootstrapperPath

    if ($signingRequested) {
        foreach ($fileToSign in @($applicationExecutable, $applicationDll, $coreDll)) {
            Invoke-Sign $fileToSign
        }
    }

    $innoCompiler = Find-InnoCompiler
    $innoArguments = @(
        "/DAppVersion=$Version",
        "/DAppVersionNumeric=$Version.0",
        "/DSourceDir=$publishPath",
        "/DOutputDir=$outputPath",
        "/DWebView2Bootstrapper=$bootstrapperPath",
        "/DUpdateLocationUrl=$UpdateLocationUrl")
    if ($signingRequested) {
        $signCommand = '"{0}" sign /sha1 {1} /fd SHA256 /td SHA256 /tr "{2}" /d $qStarling Service Checker$q $f' -f $SignToolPath, $SigningCertificateThumbprint, $TimestampUrl
        $innoArguments += "--signtool=starling=$signCommand"
        $innoArguments += '/DSignToolName=starling'
    }
    $innoArguments += 'installer\StarlingServiceChecker.iss'
    Invoke-Checked $innoCompiler $innoArguments

    $installerPath = Join-Path $outputPath "StarlingServiceChecker-Setup-$Version.exe"
    if (-not (Test-Path -LiteralPath $installerPath)) {
        throw "Installer output was not created: $installerPath"
    }
    if ($signingRequested) {
        Invoke-Checked $SignToolPath @('verify', '/pa', '/all', $installerPath)
    }

    $installerHash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $checksumPath = "$installerPath.sha256"
    [IO.File]::WriteAllText(
        $checksumPath,
        "$installerHash *$([IO.Path]::GetFileName($installerPath))`n",
        [Text.UTF8Encoding]::new($false))
    $manifestPath = Join-Path $outputPath "StarlingServiceChecker-$Version-release.json"
    [ordered]@{
        product = 'Starling Service Checker'
        version = $Version
        runtime = 'win-x64 self-contained'
        installer = [IO.Path]::GetFileName($installerPath)
        sha256 = $installerHash
        signed = $signingRequested
        updateLocation = $UpdateLocationUrl
        webView2 = 'Microsoft Evergreen bootstrapper, installed only when missing'
    } | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding UTF8

    [pscustomobject]@{
        Installer = $installerPath
        Checksum = $checksumPath
        Manifest = $manifestPath
        Signed = $signingRequested
    }
}
finally {
    Pop-Location
}
