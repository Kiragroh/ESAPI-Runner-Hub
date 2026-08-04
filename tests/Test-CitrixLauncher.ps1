[CmdletBinding()]
param(
    [string]$FixturePath = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$launcherSource = Join-Path $repoRoot 'citrix\Start-ESAPI-Runner-Hub.cmd'
$fixtureSource = if ([string]::IsNullOrWhiteSpace($FixturePath)) {
    Join-Path $repoRoot 'tests\RunnerFixture\bin\x64\Release\RunnerFixture.exe'
}
else {
    [System.IO.Path]::GetFullPath($FixturePath)
}
if (-not (Test-Path -LiteralPath $fixtureSource -PathType Leaf)) {
    throw "Runner fixture not found: $fixtureSource"
}
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('esapi-runner-hub-citrix-test-' + [Guid]::NewGuid().ToString('N'))
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

function Invoke-TestCase {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Pointer,
        [string[]]$Arguments = @(),
        [string]$ConfiguredLogDirectory = '',
        [string]$PointerLineEnding = "`r`n",
        [switch]$OmitSettings
    )

    $caseRoot = Join-Path $testRoot ([Guid]::NewGuid().ToString('N'))
    $citrix = Join-Path $caseRoot 'citrix'
    $dist = Join-Path $caseRoot 'dist'
    $versions = Join-Path $dist 'versions'
    $localAppData = Join-Path $caseRoot 'localappdata'
    $capture = Join-Path $caseRoot 'capture.txt'
    New-Item -ItemType Directory -Path $citrix, $versions, $localAppData -Force | Out-Null

    Copy-Item -LiteralPath $launcherSource -Destination (Join-Path $citrix 'Start-ESAPI-Runner-Hub.cmd')
    Copy-Item -LiteralPath $fixtureSource -Destination (Join-Path $versions 'ESAPI-Runner-Hub.v9.9.9.exe')
    [System.IO.File]::WriteAllText((Join-Path $citrix 'current.txt'), $Pointer + $PointerLineEnding, $utf8NoBom)
    if (-not $OmitSettings) {
        $settingsText = "[Hub]`r`n"
        if (-not [string]::IsNullOrWhiteSpace($ConfiguredLogDirectory)) {
            $settingsText += "LogDirectory=$ConfiguredLogDirectory`r`n"
        }
        [System.IO.File]::WriteAllText((Join-Path $dist 'settings.ini'), $settingsText, $utf8NoBom)
    }

    $expandedArguments = @($Arguments | ForEach-Object {
        if ($_ -eq '{CAPTURE}') { $capture } else { $_ }
    })
    $quotedArguments = @($expandedArguments | ForEach-Object {
        '"' + ([string]$_).Replace('"', '""') + '"'
    })
    $batch = Join-Path $citrix 'Start-ESAPI-Runner-Hub.cmd'
    $argumentText = if ($quotedArguments.Count -gt 0) { ' ' + ($quotedArguments -join ' ') } else { '' }

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $env:ComSpec
    $startInfo.Arguments = '/d /s /c ""{0}"{1}"' -f $batch, $argumentText
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.EnvironmentVariables['LOCALAPPDATA'] = $localAppData

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    [void]$process.Start()
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    $logPath = if ([string]::IsNullOrWhiteSpace($ConfiguredLogDirectory)) {
        Join-Path $localAppData 'ESAPI-Runner-Hub\Logs\CitrixLauncher.log'
    }
    else {
        Join-Path $ConfiguredLogDirectory ("CitrixLauncher-{0}.log" -f $env:COMPUTERNAME)
    }
    [pscustomobject]@{
        ExitCode = $process.ExitCode
        StdOut = $stdout
        StdErr = $stderr
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
    if (-not (Test-Path -LiteralPath $launcherSource -PathType Leaf)) {
        throw "Expected launcher source is missing: $launcherSource"
    }
    if (-not (Test-Path -LiteralPath $fixtureSource -PathType Leaf)) {
        throw "RunnerFixture must be built before this test: $fixtureSource"
    }

    New-Item -ItemType Directory -Path $testRoot -Force | Out-Null

    Test-Case 'launcher keeps the Hub in the Citrix process tree' {
        $launcherText = Get-Content -Raw -LiteralPath $launcherSource
        Assert-Contains $launcherText '"%TARGET%" --settings "%SETTINGS%" %*' 'The Hub must be invoked directly by the published launcher.'
        Assert-NotContains $launcherText 'start "" /wait' 'START detaches the Hub from reliable Citrix process tracking.'
    }

    Test-Case 'valid pointer launches and keeps arguments out of logs' {
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

    Test-Case 'launcher writes diagnostics to the settings log directory' {
        $sharedLogDirectory = Join-Path $testRoot 'shared launcher logs'
        $result = Invoke-TestCase -Pointer 'ESAPI-Runner-Hub.v9.9.9.exe' -ConfiguredLogDirectory $sharedLogDirectory
        Assert-Equal 0 $result.ExitCode 'A configured shared log directory must not prevent launch.'
        Assert-Equal $sharedLogDirectory (Split-Path -Parent $result.LogPath) 'The launcher log must use LogDirectory from settings.ini.'
        Assert-Contains $result.LogText 'START release=ESAPI-Runner-Hub.v9.9.9.exe' 'The shared launcher log must contain the start boundary.'
    }

    Test-Case 'LF-only release pointer launches on Windows' {
        $result = Invoke-TestCase -Pointer 'ESAPI-Runner-Hub.v9.9.9.exe' -PointerLineEnding "`n"
        Assert-Equal 0 $result.ExitCode 'The tracked current.txt may use Git LF line endings and must still launch.'
    }

    Test-Case 'path pointer is rejected' {
        $result = Invoke-TestCase -Pointer '..\evil.exe'
        Assert-Equal 21 $result.ExitCode 'A pointer with a path must be rejected.'
    }

    Test-Case 'missing target has a stable exit code' {
        $result = Invoke-TestCase -Pointer 'ESAPI-Runner-Hub.v8.8.8.exe'
        Assert-Equal 22 $result.ExitCode 'A missing target must be reported.'
    }

    Test-Case 'missing settings has a stable exit code' {
        $result = Invoke-TestCase -Pointer 'ESAPI-Runner-Hub.v9.9.9.exe' -OmitSettings
        Assert-Equal 23 $result.ExitCode 'Missing settings must be reported.'
    }

    Test-Case 'child exit code is propagated' {
        $result = Invoke-TestCase -Pointer 'ESAPI-Runner-Hub.v9.9.9.exe' -Arguments @('--mode', 'exit7')
        Assert-Equal 7 $result.ExitCode 'The child exit code must be preserved.'
    }
}
catch {
    $failures++
    Write-Host "FAIL launcher test setup: $($_.Exception.Message)"
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "RESULT $failures failed"
exit $(if ($failures -eq 0) { 0 } else { 1 })
