[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$solution = Join-Path $repoRoot 'ESAPI-Runner-Hub.sln'
$distRoot = Join-Path $repoRoot 'dist'
$versionInfo = Get-Content -Raw -LiteralPath (Join-Path $repoRoot 'versionInfo.json') | ConvertFrom-Json
$version = [string]$versionInfo.version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Invalid release version: $version" }
$scriptHostVersion = [string]$versionInfo.scriptHostVersion
if ($scriptHostVersion -notmatch '^\d+\.\d+\.\d+$') { throw "Invalid script host version: $scriptHostVersion" }
$expectedScriptHostFileVersion = $scriptHostVersion + '.0'
$citrixLauncherVersion = [string]$versionInfo.citrixLauncherVersion
if ($citrixLauncherVersion -notmatch '^\d+\.\d+\.\d+$') { throw "Invalid Citrix launcher version: $citrixLauncherVersion" }
$expectedCitrixLauncherFileVersion = $citrixLauncherVersion + '.0'
$versionedDist = Join-Path $repoRoot 'dist\versions'
$versionedExeName = "ESAPI-Runner-Hub.v$version.exe"
$versionedExePath = Join-Path $versionedDist $versionedExeName
$zipName = "ESAPI-Runner-Hub-v$version-win-x64.zip"
$zipPath = Join-Path $distRoot $zipName
$stagingParent = Join-Path $repoRoot 'obj\release-staging'
$stagingRoot = Join-Path $stagingParent ([Guid]::NewGuid().ToString('N'))
$buildOutput = Join-Path $stagingRoot 'build'
$packageRoot = Join-Path $stagingRoot 'package'
$stagedZip = Join-Path $stagingRoot $zipName
$esapiReferenceDirectory = [System.IO.Path]::GetFullPath((Join-Path $repoRoot '..\_Assets'))
if (-not (Test-Path -LiteralPath (Join-Path $esapiReferenceDirectory 'VMS.TPS.Common.Model.API.dll') -PathType Leaf)) {
    $ancestor = [System.IO.DirectoryInfo]::new($repoRoot)
    while ($ancestor -and -not (Test-Path -LiteralPath (Join-Path $ancestor.FullName '_Assets\VMS.TPS.Common.Model.API.dll') -PathType Leaf)) {
        $ancestor = $ancestor.Parent
    }
    if ($ancestor) { $esapiReferenceDirectory = Join-Path $ancestor.FullName '_Assets' }
}
$esapiApiReference = Join-Path $esapiReferenceDirectory 'VMS.TPS.Common.Model.API.dll'
$esapiTypesReference = Join-Path $esapiReferenceDirectory 'VMS.TPS.Common.Model.Types.dll'
foreach ($reference in $esapiApiReference, $esapiTypesReference) {
    if (-not (Test-Path -LiteralPath $reference -PathType Leaf)) {
        throw "Eclipse 18 build reference missing: $reference"
    }
    if (-not ([System.Diagnostics.FileVersionInfo]::GetVersionInfo($reference).FileVersion).StartsWith('18.')) {
        throw "Eclipse 18 build reference expected, found another version: $reference"
    }
}

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

    if (Test-Path -LiteralPath $Destination -PathType Leaf) {
        $sourceHash = (Get-FileHash -LiteralPath $Source -Algorithm SHA256).Hash
        $destinationHash = (Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash
        if ($sourceHash -eq $destinationHash) { return }
    }

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

function Resolve-StableComponentBinary {
    param(
        [Parameter(Mandatory = $true)][string]$BuiltPath,
        [Parameter(Mandatory = $true)][string]$LivePath,
        [Parameter(Mandatory = $true)][string]$ExpectedFileVersion
    )

    if (Test-Path -LiteralPath $LivePath -PathType Leaf) {
        $liveVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($LivePath).FileVersion
        if ([string]::Equals($liveVersion, $ExpectedFileVersion, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $LivePath
        }
    }

    $builtVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($BuiltPath).FileVersion
    if (-not [string]::Equals($builtVersion, $ExpectedFileVersion, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Stable component version mismatch: expected $ExpectedFileVersion, found $builtVersion at $BuiltPath"
    }
    return $BuiltPath
}

$expectedDist = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'dist'))
if ([System.IO.Path]::GetFullPath($distRoot) -ne $expectedDist -or -not $distRoot.StartsWith($repoRoot + [System.IO.Path]::DirectorySeparatorChar)) {
    throw 'Resolved dist path is outside the repository.'
}
try {
    New-Item -ItemType Directory -Path $buildOutput, (Join-Path $packageRoot 'assets'), (Join-Path $packageRoot 'docs'), $distRoot, $versionedDist -Force | Out-Null

    $msbuild = Resolve-MSBuild
    & $msbuild $solution /t:Rebuild /p:Configuration=Release /p:Platform=x64 ("/p:OutputPath={0}" -f $buildOutput) ("/p:EsapiReferenceDirectory={0}" -f $esapiReferenceDirectory) /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "Release build failed with exit code $LASTEXITCODE." }

    $tests = Join-Path $buildOutput 'ESAPI.RunnerHub.Tests.exe'
    & $tests
    if ($LASTEXITCODE -ne 0) { throw "Automated tests failed with exit code $LASTEXITCODE." }

    $launcherTests = Join-Path $repoRoot 'tests\Test-CitrixLauncher.ps1'
    $fixture = Join-Path $buildOutput 'RunnerFixture.exe'
    $windowsPowerShell = (Get-Command powershell.exe -ErrorAction Stop).Source
    & $windowsPowerShell -NoProfile -ExecutionPolicy Bypass -File $launcherTests -FixturePath $fixture
    if ($LASTEXITCODE -ne 0) { throw "Citrix launcher tests failed with exit code $LASTEXITCODE." }

    $exeLauncherTests = Join-Path $repoRoot 'tests\Test-CitrixExeLauncher.ps1'
    $exeLauncher = Join-Path $buildOutput 'ESAPI-Runner-Hub.CitrixLauncher.exe'
    & $windowsPowerShell -NoProfile -ExecutionPolicy Bypass -File $exeLauncherTests -LauncherPath $exeLauncher -FixturePath $fixture
    if ($LASTEXITCODE -ne 0) { throw "Citrix EXE launcher tests failed with exit code $LASTEXITCODE." }

    $stableReadHost = Resolve-StableComponentBinary `
        -BuiltPath (Join-Path $buildOutput 'ESAPI-Script-Host.exe') `
        -LivePath (Join-Path $distRoot 'ESAPI-Script-Host.exe') `
        -ExpectedFileVersion $expectedScriptHostFileVersion
    $stableWriteHost = Resolve-StableComponentBinary `
        -BuiltPath (Join-Path $buildOutput 'ESAPI-Write-Script-Host.exe') `
        -LivePath (Join-Path $distRoot 'ESAPI-Write-Script-Host.exe') `
        -ExpectedFileVersion $expectedScriptHostFileVersion
    $stableCitrixLauncher = Resolve-StableComponentBinary `
        -BuiltPath $exeLauncher `
        -LivePath (Join-Path $repoRoot 'citrix\ESAPI-Runner-Hub.CitrixLauncher.exe') `
        -ExpectedFileVersion $expectedCitrixLauncherFileVersion

    $copies = @{
        (Join-Path $buildOutput 'ESAPI-Runner-Hub.exe') = (Join-Path $packageRoot 'ESAPI-Runner-Hub.exe')
        $stableReadHost = (Join-Path $packageRoot 'ESAPI-Script-Host.exe')
        (Join-Path $buildOutput 'ESAPI-Script-Host.exe.config') = (Join-Path $packageRoot 'ESAPI-Script-Host.exe.config')
        $stableWriteHost = (Join-Path $packageRoot 'ESAPI-Write-Script-Host.exe')
        (Join-Path $buildOutput 'ESAPI-Write-Script-Host.exe.config') = (Join-Path $packageRoot 'ESAPI-Write-Script-Host.exe.config')
        $stableCitrixLauncher = (Join-Path $packageRoot 'ESAPI-Runner-Hub.CitrixLauncher.exe')
        (Join-Path $repoRoot 'settings.example.ini') = (Join-Path $packageRoot 'settings.example.ini')
        (Join-Path $repoRoot 'README.md') = (Join-Path $packageRoot 'README.md')
        (Join-Path $repoRoot 'LICENSE') = (Join-Path $packageRoot 'LICENSE')
        (Join-Path $repoRoot 'CHANGELOG.md') = (Join-Path $packageRoot 'CHANGELOG.md')
        (Join-Path $repoRoot 'versionInfo.json') = (Join-Path $packageRoot 'versionInfo.json')
        (Join-Path $repoRoot 'assets\ESAPI-Runner-Hub.png') = (Join-Path $packageRoot 'assets\ESAPI-Runner-Hub.png')
        (Join-Path $repoRoot 'docs\CLINICAL_VALIDATION.md') = (Join-Path $packageRoot 'docs\CLINICAL_VALIDATION.md')
        (Join-Path $repoRoot 'docs\CONTEXT_DEBUGGING.md') = (Join-Path $packageRoot 'docs\CONTEXT_DEBUGGING.md')
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
    Copy-FileWithRetry `
        -Source $stableReadHost `
        -Destination (Join-Path $distRoot 'ESAPI-Script-Host.exe')
    Copy-FileWithRetry `
        -Source (Join-Path $buildOutput 'ESAPI-Script-Host.exe.config') `
        -Destination (Join-Path $distRoot 'ESAPI-Script-Host.exe.config')
    Copy-FileWithRetry `
        -Source $stableWriteHost `
        -Destination (Join-Path $distRoot 'ESAPI-Write-Script-Host.exe')
    Copy-FileWithRetry `
        -Source (Join-Path $buildOutput 'ESAPI-Write-Script-Host.exe.config') `
        -Destination (Join-Path $distRoot 'ESAPI-Write-Script-Host.exe.config')
    Copy-FileWithRetry `
        -Source $stableCitrixLauncher `
        -Destination (Join-Path $repoRoot 'citrix\ESAPI-Runner-Hub.CitrixLauncher.exe')
    Copy-FileWithRetry -Source $stagedZip -Destination $zipPath

    $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $externalManifest = Join-Path $distRoot 'SHA256SUMS.txt'
    [System.IO.File]::WriteAllText($externalManifest, "$zipHash  $zipName`r`n", [System.Text.UTF8Encoding]::new($false))
}
finally {
    if (Test-Path -LiteralPath $stagingRoot) {
        $resolvedStaging = [System.IO.Path]::GetFullPath($stagingRoot)
        $resolvedParent = [System.IO.Path]::GetFullPath($stagingParent) + [System.IO.Path]::DirectorySeparatorChar
        if (-not $resolvedStaging.StartsWith($resolvedParent, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove release staging outside the expected directory: $resolvedStaging"
        }
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "Release ready: $zipPath"
Write-Host "Citrix binary: $versionedExePath"
Write-Host "SHA256: $zipHash"
