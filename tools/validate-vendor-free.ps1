[CmdletBinding()]
param(
    [string]$ReleaseDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'dist\package')
)

$ErrorActionPreference = 'Stop'
$releaseRoot = [System.IO.Path]::GetFullPath($ReleaseDirectory)
if (-not (Test-Path -LiteralPath $releaseRoot -PathType Container)) {
    throw "Release directory missing: $releaseRoot"
}

$files = @(Get-ChildItem -LiteralPath $releaseRoot -Recurse -File)
if ($files.Count -eq 0) {
    throw 'Release directory is empty.'
}

$forbiddenExtensions = @('.dll', '.pdb', '.config', '.xml', '.user', '.suo')
$allowedConfigurationFiles = @('ESAPI-Script-Host.exe.config')
$forbiddenFiles = @($files | Where-Object {
    ($forbiddenExtensions -contains $_.Extension.ToLowerInvariant() -and
        $allowedConfigurationFiles -notcontains $_.Name) -or
    $_.Name -match '(?i)VMS\.TPS|EsapiEssentials' -or
    $_.Name -eq 'settings.ini'
})
if ($forbiddenFiles.Count -gt 0) {
    throw ('Forbidden release files: ' + (($forbiddenFiles | ForEach-Object FullName) -join ', '))
}

$launcher = Join-Path $releaseRoot 'ESAPI-Runner-Hub.exe'
if (-not (Test-Path -LiteralPath $launcher -PathType Leaf)) {
    throw 'ESAPI-Runner-Hub.exe is missing from the release directory.'
}

# A metadata-only VMS API reference is required by ESAPI 16.1 to authorize a
# standalone entry assembly. Vendor DLL files remain forbidden in the package.

$required = @(
    'ESAPI-Runner-Hub.exe',
    'ESAPI-Script-Host.exe',
    'ESAPI-Script-Host.exe.config',
    'settings.example.ini',
    'README.md',
    'LICENSE',
    'CHANGELOG.md',
    'versionInfo.json',
    'PACKAGE-SHA256SUMS.txt',
    'assets\ESAPI-Runner-Hub.png',
    'docs\CLINICAL_VALIDATION.md'
)
foreach ($relativePath in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $releaseRoot $relativePath) -PathType Leaf)) {
        throw "Required release file missing: $relativePath"
    }
}

Write-Host "Vendor-free release validation passed: $releaseRoot"
