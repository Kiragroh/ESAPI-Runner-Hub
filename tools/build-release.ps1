[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$solution = Join-Path $repoRoot 'ESAPI-Runner-Hub.sln'
$distRoot = Join-Path $repoRoot 'dist'
$versionInfo = Get-Content -Raw -LiteralPath (Join-Path $repoRoot 'versionInfo.json') | ConvertFrom-Json
$version = [string]$versionInfo.version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Invalid release version: $version" }
$versionedDist = Join-Path $repoRoot 'dist\versions'
$versionedExeName = "ESAPI-Runner-Hub.v$version.exe"
$versionedExePath = Join-Path $versionedDist $versionedExeName
$zipName = "ESAPI-Runner-Hub-v$version-win-x64.zip"
$zipPath = Join-Path $distRoot $zipName

function Resolve-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $candidate = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        if ($candidate -and (Test-Path -LiteralPath $candidate)) { return $candidate }
    }
    $command = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    throw 'MSBuild.exe with .NET Framework 4.8 support was not found.'
}

function Copy-FileWithRetry {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    for ($attempt = 1; $attempt -le 10; $attempt++) {
        try {
            Copy-Item -LiteralPath $Source -Destination $Destination -Force
            return
        }
        catch {
            if ($attempt -eq 10) { throw }
            Start-Sleep -Milliseconds 250
        }
    }
}

function Publish-ImmutableBinary {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    if (Test-Path -LiteralPath $Destination -PathType Leaf) {
        $sourceHash = (Get-FileHash -LiteralPath $Source -Algorithm SHA256).Hash
        $destinationHash = (Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash
        if ($sourceHash -ne $destinationHash) {
            throw "Existing versioned binary has a different SHA-256: $Destination"
        }
        return
    }

    Copy-FileWithRetry -Source $Source -Destination $Destination
}

$msbuild = Resolve-MSBuild
& $msbuild $solution /t:Rebuild /p:Configuration=Release /p:Platform=x64 /v:minimal
if ($LASTEXITCODE -ne 0) { throw "Release build failed with exit code $LASTEXITCODE." }

$tests = Join-Path $repoRoot 'tests\ESAPI.RunnerHub.Tests\bin\x64\Release\ESAPI.RunnerHub.Tests.exe'
& $tests
if ($LASTEXITCODE -ne 0) { throw "Automated tests failed with exit code $LASTEXITCODE." }

$expectedDist = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'dist'))
if ([System.IO.Path]::GetFullPath($distRoot) -ne $expectedDist -or -not $distRoot.StartsWith($repoRoot + [System.IO.Path]::DirectorySeparatorChar)) {
    throw 'Resolved dist path is outside the repository.'
}
$stagingRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("esapi-runner-hub-release-" + [Guid]::NewGuid().ToString('N'))
$packageRoot = Join-Path $stagingRoot 'package'
$stagedZip = Join-Path $stagingRoot $zipName
try {
    New-Item -ItemType Directory -Path (Join-Path $packageRoot 'assets'), (Join-Path $packageRoot 'docs'), $distRoot, $versionedDist -Force | Out-Null

    $copies = @{
        (Join-Path $repoRoot 'src\ESAPI.RunnerHub\bin\x64\Release\ESAPI-Runner-Hub.exe') = (Join-Path $packageRoot 'ESAPI-Runner-Hub.exe')
        (Join-Path $repoRoot 'settings.example.ini') = (Join-Path $packageRoot 'settings.example.ini')
        (Join-Path $repoRoot 'README.md') = (Join-Path $packageRoot 'README.md')
        (Join-Path $repoRoot 'LICENSE') = (Join-Path $packageRoot 'LICENSE')
        (Join-Path $repoRoot 'CHANGELOG.md') = (Join-Path $packageRoot 'CHANGELOG.md')
        (Join-Path $repoRoot 'versionInfo.json') = (Join-Path $packageRoot 'versionInfo.json')
        (Join-Path $repoRoot 'assets\ESAPI-Runner-Hub.png') = (Join-Path $packageRoot 'assets\ESAPI-Runner-Hub.png')
        (Join-Path $repoRoot 'docs\CLINICAL_VALIDATION.md') = (Join-Path $packageRoot 'docs\CLINICAL_VALIDATION.md')
    }
    foreach ($source in $copies.Keys) {
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Release source missing: $source" }
        Copy-Item -LiteralPath $source -Destination $copies[$source]
    }

    $manifestPath = Join-Path $packageRoot 'PACKAGE-SHA256SUMS.txt'
    $manifestLines = @(Get-ChildItem -LiteralPath $packageRoot -Recurse -File | Sort-Object FullName | ForEach-Object {
        $relative = $_.FullName.Substring($packageRoot.Length + 1).Replace('\', '/')
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $relative"
    })
    [System.IO.File]::WriteAllLines($manifestPath, $manifestLines, [System.Text.UTF8Encoding]::new($false))

    & (Join-Path $PSScriptRoot 'validate-vendor-free.ps1') -ReleaseDirectory $packageRoot
    & (Join-Path $PSScriptRoot 'create-deterministic-zip.ps1') -SourceDirectory $packageRoot -DestinationZip $stagedZip

    Publish-ImmutableBinary `
        -Source (Join-Path $packageRoot 'ESAPI-Runner-Hub.exe') `
        -Destination $versionedExePath
    Copy-FileWithRetry -Source $stagedZip -Destination $zipPath

    $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $externalManifest = Join-Path $distRoot 'SHA256SUMS.txt'
    [System.IO.File]::WriteAllText($externalManifest, "$zipHash  $zipName`r`n", [System.Text.UTF8Encoding]::new($false))
}
finally {
    if (Test-Path -LiteralPath $stagingRoot) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "Release ready: $zipPath"
Write-Host "Citrix binary: $versionedExePath"
Write-Host "SHA256: $zipHash"
