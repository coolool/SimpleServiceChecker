[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,
    [string]$ArtifactDirectory = 'artifacts\installer',
    [ValidatePattern('^https://')]
    [string]$ExpectedUpdateLocationUrl,
    [string]$ExpectedSourceRepository,
    [ValidatePattern('^[0-9A-Fa-f]{40}$')]
    [string]$ExpectedSourceCommit,
    [ValidatePattern('^[0-9A-Fa-f]{64}$')]
    [string]$ExpectedCoverageTemplateSha256,
    [switch]$RequireGitHubReleasesUpdateLocation
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactPath = if ([IO.Path]::IsPathRooted($ArtifactDirectory)) {
    [IO.Path]::GetFullPath($ArtifactDirectory)
}
else {
    [IO.Path]::GetFullPath((Join-Path $repoRoot $ArtifactDirectory))
}

function Assert-Equal {
    param($Actual, $Expected, [string]$Label)

    if (-not [object]::Equals($Actual, $Expected)) {
        throw "$Label mismatch. Expected '$Expected' but found '$Actual'."
    }
}

if (-not (Test-Path -LiteralPath $artifactPath -PathType Container)) {
    throw "Release artifact directory was not found: $artifactPath"
}

$installerName = "StarlingServiceChecker-Setup-$Version.exe"
$checksumName = "$installerName.sha256"
$manifestName = "StarlingServiceChecker-$Version-release.json"
$installerPath = Join-Path $artifactPath $installerName
$checksumPath = Join-Path $artifactPath $checksumName
$manifestPath = Join-Path $artifactPath $manifestName
$coverageTemplateName = 'StarlingServiceChecker-Coverage-Template.xlsx'
$coverageTemplatePath = Join-Path $artifactPath $coverageTemplateName

foreach ($requiredPath in @($installerPath, $checksumPath, $manifestPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required release artifact is missing: $requiredPath"
    }
}

$coverageTemplateHash = $null
if (-not [string]::IsNullOrWhiteSpace($ExpectedCoverageTemplateSha256)) {
    if (-not (Test-Path -LiteralPath $coverageTemplatePath -PathType Leaf)) {
        throw "Required blank coverage template is missing: $coverageTemplatePath"
    }

    $coverageTemplateHash = (Get-FileHash -LiteralPath $coverageTemplatePath -Algorithm SHA256).Hash.ToLowerInvariant()
    Assert-Equal $coverageTemplateHash $ExpectedCoverageTemplateSha256.ToLowerInvariant() 'Coverage template SHA-256'
}

try {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
}
catch {
    throw "Release manifest is not valid JSON: $manifestPath"
}

Assert-Equal $manifest.product 'Starling Service Checker' 'Manifest product'
Assert-Equal $manifest.version $Version 'Manifest version'
Assert-Equal $manifest.runtime 'win-x64 self-contained' 'Manifest runtime'
Assert-Equal $manifest.installer $installerName 'Manifest installer name'
Assert-Equal $manifest.webView2 'Microsoft Evergreen bootstrapper, installed only when missing' 'Manifest WebView2 policy'

if ($manifest.sha256 -notmatch '^[0-9a-f]{64}$') {
    throw 'Manifest SHA-256 must contain exactly 64 lowercase hexadecimal characters.'
}
if ($manifest.signed -isnot [bool] -or $manifest.timestamped -isnot [bool]) {
    throw 'Manifest signed and timestamped fields must be JSON booleans.'
}
if ($manifest.timestamped -and -not $manifest.signed) {
    throw 'Manifest cannot claim a timestamped installer when signed is false.'
}
Assert-Equal $manifest.signed $false 'Manifest signed policy'
Assert-Equal $manifest.timestamped $false 'Manifest timestamp policy'
if ([string]::IsNullOrWhiteSpace([string]$manifest.sourceRepository)) {
    throw 'Manifest sourceRepository is required.'
}
if ([string]$manifest.sourceCommit -notmatch '^[0-9a-f]{40}$') {
    throw 'Manifest sourceCommit must contain exactly 40 lowercase hexadecimal characters.'
}
if (-not [string]::IsNullOrWhiteSpace($ExpectedSourceRepository)) {
    Assert-Equal ([string]$manifest.sourceRepository) $ExpectedSourceRepository 'Manifest source repository'
}
if (-not [string]::IsNullOrWhiteSpace($ExpectedSourceCommit)) {
    Assert-Equal ([string]$manifest.sourceCommit) $ExpectedSourceCommit.ToLowerInvariant() 'Manifest source commit'
}

$updateUri = $null
if (-not [Uri]::TryCreate([string]$manifest.updateLocation, [UriKind]::Absolute, [ref]$updateUri) -or
    $updateUri.Scheme -ne [Uri]::UriSchemeHttps) {
    throw 'Manifest updateLocation must be an absolute HTTPS URL.'
}
if (-not [string]::IsNullOrWhiteSpace($ExpectedUpdateLocationUrl)) {
    Assert-Equal ([string]$manifest.updateLocation) $ExpectedUpdateLocationUrl 'Manifest update location'
}
if ($RequireGitHubReleasesUpdateLocation -and
    (-not $updateUri.Host.Equals('github.com', [StringComparison]::OrdinalIgnoreCase) -or
        $updateUri.AbsolutePath -notmatch '^/[^/]+/[^/]+/releases(?:/latest)?/?$')) {
    throw 'Production updateLocation must be a GitHub repository Releases or Releases/latest URL.'
}

$actualHash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
Assert-Equal $actualHash ([string]$manifest.sha256) 'Installer SHA-256 in manifest'

$checksumText = Get-Content -LiteralPath $checksumPath -Raw
$checksumPattern = '^' + [regex]::Escape("$actualHash *$installerName") + '(?:\r?\n)?$'
if ($checksumText -cnotmatch $checksumPattern) {
    throw 'Checksum file mismatch. Expected one SHA-256 entry for the release installer.'
}

$installerVersion = (Get-Item -LiteralPath $installerPath).VersionInfo.FileVersion
if ([string]::IsNullOrWhiteSpace($installerVersion) -or
    -not $installerVersion.StartsWith("$Version.", [StringComparison]::Ordinal)) {
    throw "Installer file version '$installerVersion' does not match release version '$Version'."
}

$signature = Get-AuthenticodeSignature -LiteralPath $installerPath
$hasSignature = $null -ne $signature.SignerCertificate
if ($hasSignature -or $signature.Status -ne [Management.Automation.SignatureStatus]::NotSigned) {
    throw "Approved release policy requires an unsigned installer, but Authenticode status is '$($signature.Status)'."
}

[pscustomobject]@{
    Version = $Version
    Installer = $installerPath
    Sha256 = $actualHash
    Signed = $false
    Timestamped = $false
    UpdateLocation = [string]$manifest.updateLocation
    SourceRepository = [string]$manifest.sourceRepository
    SourceCommit = [string]$manifest.sourceCommit
    CoverageTemplate = if ($null -ne $coverageTemplateHash) { $coverageTemplatePath } else { $null }
    CoverageTemplateSha256 = $coverageTemplateHash
}
