[CmdletBinding()]
param(
    [string]$LauncherPath = '',
    [string]$FixturePath = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$launcherSource = if ([string]::IsNullOrWhiteSpace($LauncherPath)) {
    Join-Path $repoRoot 'citrix\ESAPI-Runner-Hub.CitrixLauncher.exe'
}
else {
    [System.IO.Path]::GetFullPath($LauncherPath)
}
$fixtureSource = if ([string]::IsNullOrWhiteSpace($FixturePath)) {
    Join-Path $repoRoot 'tests\RunnerFixture\bin\x64\Release\RunnerFixture.exe'
}
else {
    [System.IO.Path]::GetFullPath($FixturePath)
}

if (-not (Test-Path -LiteralPath $launcherSource -PathType Leaf)) {
    throw "Citrix EXE launcher not found: $launcherSource"
}
if (-not (Test-Path -LiteralPath $fixtureSource -PathType Leaf)) {
    throw "Runner fixture not found: $fixtureSource"
}

$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('esapi-runner-hub-citrix-exe-test-' + [Guid]::NewGuid().ToString('N'))
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$failures = 0

function Assert-Equal {
    param($Expected, $Actual, [string]$Message)
    if ($Expected -ne $Actual) {
        throw "$Message Expected '$Expected', got '$Actual'."
    }
}

function Assert-Contains {
    param([string]$Actual, [string]$Expected, [string]$Message)
    if ($null -eq $Actual -or -not $Actual.Contains($Expected)) {
        throw "$Message Missing '$Expected'."
    }
}

function Assert-NotContains {
    param([string]$Actual, [string]$Unexpected, [string]$Message)
    if ($null -ne $Actual -and $Actual.Contains($Unexpected)) {
        throw "$Message Found forbidden text '$Unexpected'."
    }
}

function Quote-WindowsArgument {
    param([AllowEmptyString()][string]$Value)
    if ($Value -notmatch '[\s"]') { return $Value }
    return '"' + ($Value -replace '(\\*)"', '$1$1\"' -replace '(\\+)$', '$1$1') + '"'
}

function Invoke-TestCase {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Pointer,
        [string[]]$Arguments = @(),
        [switch]$PackedArgument,
        [switch]$OmitSettings
    )

    $caseRoot = Join-Path $testRoot ([Guid]::NewGuid().ToString('N'))
    $citrix = Join-Path $caseRoot 'citrix'
    $dist = Join-Path $caseRoot 'dist'
    $versions = Join-Path $dist 'versions'
    $localAppData = Join-Path $caseRoot 'localappdata'
    $capture = Join-Path $caseRoot 'capture file.txt'
    New-Item -ItemType Directory -Path $citrix, $versions, $localAppData -Force | Out-Null

    $launcher = Join-Path $citrix 'ESAPI-Runner-Hub.CitrixLauncher.exe'
    Copy-Item -LiteralPath $launcherSource -Destination $launcher
    Copy-Item -LiteralPath $fixtureSource -Destination (Join-Path $versions 'ESAPI-Runner-Hub.v9.9.9.exe')
    [System.IO.File]::WriteAllText((Join-Path $citrix 'current.txt'), $Pointer + "`r`n", $utf8NoBom)
    if (-not $OmitSettings) {
        [System.IO.File]::WriteAllText((Join-Path $dist 'settings.ini'), "[Hub]`r`nLogDirectory=%LOCALAPPDATA%\ESAPI-Runner-Hub\Logs`r`n", $utf8NoBom)
    }

    $expandedArguments = @($Arguments | ForEach-Object {
        if ($_ -eq '{CAPTURE}') { $capture } else { $_ }
    })
    $processArguments = if ($PackedArgument) {
        @(($expandedArguments | ForEach-Object { Quote-WindowsArgument ([string]$_) }) -join ' ')
    }
    else {
        $expandedArguments
    }

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $launcher
    $startInfo.Arguments = ($processArguments | ForEach-Object { Quote-WindowsArgument ([string]$_) }) -join ' '
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.EnvironmentVariables['LOCALAPPDATA'] = $localAppData
    $startInfo.EnvironmentVariables['ESAPI_RUNNER_CITRIX_LAUNCHER_SUPPRESS_UI'] = '1'

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    [void]$process.Start()
    $process.WaitForExit()

    $logPath = Join-Path $localAppData 'ESAPI-Runner-Hub\Logs\CitrixLauncher.log'
    [pscustomobject]@{
        ExitCode = $process.ExitCode
        CapturePath = $capture
        LogPath = $logPath
        LogText = if (Test-Path -LiteralPath $logPath) { Get-Content -Raw -LiteralPath $logPath } else { '' }
    }
}

function Test-Case {
    param([string]$Name, [scriptblock]$Body)
    try {
        & $Body
        Write-Host "PASS $Name"
    }
    catch {
        $script:failures++
        Write-Host "FAIL $Name`: $($_.Exception.Message)"
    }
}

try {
    New-Item -ItemType Directory -Path $testRoot -Force | Out-Null

    Test-Case 'EXE launcher forwards separate arguments and redacts logs' {
        $result = Invoke-TestCase -Pointer 'ESAPI-Runner-Hub.v9.9.9.exe' -Arguments @(
            '--mode', 'capture', '--capture', '{CAPTURE}', '--secret', 'DO-NOT-LOG')
        Assert-Equal 0 $result.ExitCode 'A valid pointer must launch.'
        $captureText = Get-Content -Raw -LiteralPath $result.CapturePath
        Assert-Contains $captureText '--settings' 'The shared settings path must be passed.'
        Assert-Contains $captureText '--secret|DO-NOT-LOG' 'Arguments must reach the child unchanged.'
        Assert-NotContains $result.LogText 'DO-NOT-LOG' 'Arguments must not be logged.'
        Assert-Contains $result.LogText 'START release=ESAPI-Runner-Hub.v9.9.9.exe' 'Start must be logged.'
        Assert-Contains $result.LogText 'EXIT release=ESAPI-Runner-Hub.v9.9.9.exe code=0' 'Exit must be logged.'
    }

    Test-Case 'EXE launcher expands one Citrix-packed argument' {
        $result = Invoke-TestCase -Pointer 'ESAPI-Runner-Hub.v9.9.9.exe' -Arguments @(
            '--mode', 'capture', '--capture', '{CAPTURE}') -PackedArgument
        Assert-Equal 0 $result.ExitCode 'A packed Citrix argument must launch.'
        $captureText = Get-Content -Raw -LiteralPath $result.CapturePath
        Assert-Contains $captureText '--mode|capture|--capture' 'The packed argument must be expanded for the Runner.'
    }

    Test-Case 'EXE launcher rejects a path pointer' {
        $result = Invoke-TestCase -Pointer '..\evil.exe'
        Assert-Equal 21 $result.ExitCode 'A pointer with a path must be rejected.'
    }

    Test-Case 'EXE launcher reports missing settings' {
        $result = Invoke-TestCase -Pointer 'ESAPI-Runner-Hub.v9.9.9.exe' -OmitSettings
        Assert-Equal 23 $result.ExitCode 'Missing settings must be reported.'
    }

    Test-Case 'EXE launcher propagates the child exit code' {
        $result = Invoke-TestCase -Pointer 'ESAPI-Runner-Hub.v9.9.9.exe' -Arguments @('--mode', 'exit7')
        Assert-Equal 7 $result.ExitCode 'The child exit code must be preserved.'
    }
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "RESULT $failures failed"
exit $(if ($failures -eq 0) { 0 } else { 1 })
