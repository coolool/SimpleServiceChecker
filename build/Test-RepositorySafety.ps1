[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$repoPrefix = $repoRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$prohibitedExtensions = [Collections.Generic.HashSet[string]]::new(
    [string[]]@('.csv', '.db', '.exe', '.geojson', '.key', '.kml', '.kmz', '.log', '.msi', '.msix', '.p12', '.pem', '.pfx', '.sqlite', '.xls', '.xlsx'),
    [StringComparer]::OrdinalIgnoreCase)
$prohibitedNames = [Collections.Generic.HashSet[string]]::new(
    [string[]]@('.env', 'appsettings.local.json', 'google-key.dat', 'secrets.json'),
    [StringComparer]::OrdinalIgnoreCase)
$sensitivePatterns = [ordered]@{
    'Google API key-shaped value' = ('AI' + 'za[0-9A-Za-z_-]{35}')
    'private-key material' = ('-----BEGIN (?:[A-Z0-9]+ )?' + 'PRIVATE KEY-----')
}
$issues = [Collections.Generic.List[object]]::new()

Push-Location $repoRoot
try {
    $safeDirectory = $repoRoot.Replace([IO.Path]::DirectorySeparatorChar, '/')
    $trackedFiles = @(& git -c "safe.directory=$safeDirectory" ls-files)
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not enumerate tracked repository files.'
    }

    foreach ($relativePath in $trackedFiles) {
        $fullPath = [IO.Path]::GetFullPath((Join-Path $repoRoot $relativePath))
        if (-not $fullPath.StartsWith($repoPrefix, [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            throw "Tracked path could not be safely inspected: $relativePath"
        }

        $fileName = [IO.Path]::GetFileName($relativePath)
        $extension = [IO.Path]::GetExtension($relativePath)
        if ($prohibitedExtensions.Contains($extension) -or $prohibitedNames.Contains($fileName)) {
            $issues.Add([pscustomobject]@{ Path = $relativePath; Category = 'prohibited tracked artifact' })
            continue
        }

        $bytes = [IO.File]::ReadAllBytes($fullPath)
        if ($bytes.Length -eq 0 -or $bytes.Length -gt 5MB -or $bytes.Contains([byte]0)) {
            continue
        }
        $content = [Text.Encoding]::UTF8.GetString($bytes)
        foreach ($pattern in $sensitivePatterns.GetEnumerator()) {
            if ([Text.RegularExpressions.Regex]::IsMatch($content, $pattern.Value)) {
                $issues.Add([pscustomobject]@{ Path = $relativePath; Category = $pattern.Key })
            }
        }
    }
}
finally {
    Pop-Location
}

if ($issues.Count -gt 0) {
    Write-Error 'Repository safety check failed. Matched values are intentionally not displayed.'
    $issues |
        Sort-Object Path, Category -Unique |
        ForEach-Object { Write-Error "$($_.Path): $($_.Category)" }
    exit 1
}

Write-Output "Repository safety check passed for $($trackedFiles.Count) tracked files."
