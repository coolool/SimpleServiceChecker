[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0',
    [ValidatePattern('^https://')]
    [string]$UpdateLocationUrl = 'https://github.com/coolool/StarlingServiceChecker-Releases/releases/latest',
    [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')]
    [string]$SourceRepository = 'coolool/SimpleServiceChecker',
    [ValidatePattern('^[0-9A-Fa-f]{40}$')]
    [string]$SourceCommit,
    [string]$PublishDirectory,
    [string]$OutputDirectory,
    [string]$WebView2BootstrapperPath,
    [switch]$SkipRestore
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))

if ([string]::IsNullOrWhiteSpace($SourceCommit)) {
    $resolvedSourceCommit = & git -C $repoRoot rev-parse HEAD 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($resolvedSourceCommit)) {
        throw 'SourceCommit was not supplied and could not be resolved from the current Git checkout.'
    }
    $SourceCommit = ([string]$resolvedSourceCommit).Trim()
    if ($SourceCommit -notmatch '^[0-9A-Fa-f]{40}$') {
        throw 'The resolved source commit is not a 40-character Git commit SHA.'
    }
}

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

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Command
    $startInfo.UseShellExecute = $false
    foreach ($argument in $Arguments) {
        $startInfo.ArgumentList.Add($argument)
    }

    $process = [Diagnostics.Process]::Start($startInfo)
    try {
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) {
            throw "$Command failed with exit code $($process.ExitCode)."
        }
    }
    finally {
        $process.Dispose()
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

$publishPath = Resolve-ArtifactPath $PublishDirectory 'publish\win-x64'
$outputPath = Resolve-ArtifactPath $OutputDirectory 'installer'
$bootstrapperPath = Resolve-ArtifactPath $WebView2BootstrapperPath 'prerequisites\MicrosoftEdgeWebview2Setup.exe'
$bootstrapperDirectory = Split-Path -Parent $bootstrapperPath
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

    $innoCompiler = Find-InnoCompiler
    $innoArguments = @(
        "/DAppVersion=$Version",
        "/DAppVersionNumeric=$Version.0",
        "/DSourceDir=$publishPath",
        "/DOutputDir=$outputPath",
        "/DWebView2Bootstrapper=$bootstrapperPath",
        "/DUpdateLocationUrl=$UpdateLocationUrl")
    $innoArguments += 'installer\StarlingServiceChecker.iss'
    Invoke-Checked $innoCompiler $innoArguments

    $installerPath = Join-Path $outputPath "StarlingServiceChecker-Setup-$Version.exe"
    if (-not (Test-Path -LiteralPath $installerPath)) {
        throw "Installer output was not created: $installerPath"
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
        signed = $false
        timestamped = $false
        updateLocation = $UpdateLocationUrl
        sourceRepository = $SourceRepository
        sourceCommit = $SourceCommit.ToLowerInvariant()
        webView2 = 'Microsoft Evergreen bootstrapper, installed only when missing'
    } | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding UTF8

    [pscustomobject]@{
        Installer = $installerPath
        Checksum = $checksumPath
        Manifest = $manifestPath
        Signed = $false
    }
}
finally {
    Pop-Location
}
