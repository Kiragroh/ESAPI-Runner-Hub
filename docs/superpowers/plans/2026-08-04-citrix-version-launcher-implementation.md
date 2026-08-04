# Citrix Version Launcher Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish ESAPI Runner Hub through one stable Citrix command while activating immutable versioned Hub binaries through a one-line pointer.

**Architecture:** `cmd.exe` runs a stable repository-relative batch file. The batch validates `citrix/current.txt`, launches only the selected `dist/versions/ESAPI-Runner-Hub.vX.Y.Z.exe`, passes the existing `dist/settings.ini`, waits for the child, and logs only technical lifecycle data locally. The release build publishes a new immutable versioned binary instead of overwriting the currently blocked live executable.

**Tech Stack:** Windows CMD, PowerShell 5.1, C#/.NET Framework 4.8 test harness, MSBuild x64, Git.

---

## File map

- Create `citrix/Start-ESAPI-Runner-Hub.cmd`: stable Citrix bootstrap and validation.
- Create `citrix/current.txt`: active immutable release filename.
- Create `citrix/README-Citrix.md`: exact Studio setup, switching, rollback, and diagnosis.
- Create `tests/Test-CitrixLauncher.ps1`: executable behavior test around the real batch file.
- Modify `tools/build-release.ps1`: publish `dist/versions/ESAPI-Runner-Hub.vX.Y.Z.exe`; never overwrite the blocked legacy path.
- Modify `tests/ESAPI.RunnerHub.Tests/ReleaseMetadataTests.cs`: enforce versioned publication and v0.1.3 metadata.
- Modify `src/ESAPI.RunnerHub/Properties/AssemblyInfo.cs`, `versionInfo.json`, and `CHANGELOG.md`: release v0.1.3 build 4.
- Modify `README.md`: document the stable Citrix deployment option.

### Task 1: Add failing launcher behavior tests

**Files:**
- Create: `tests/Test-CitrixLauncher.ps1`

- [ ] **Step 1: Write the failing test harness**

The script creates an isolated `citrix`, `dist/versions`, `dist/settings.ini`, and `%LOCALAPPDATA%` tree. It copies `tests/RunnerFixture/bin/x64/Release/RunnerFixture.exe` as `ESAPI-Runner-Hub.v9.9.9.exe`, invokes the repository batch through `%ComSpec%`, and asserts:

```powershell
$success = Invoke-Launcher -Pointer 'ESAPI-Runner-Hub.v9.9.9.exe' `
    -Arguments @('--mode','capture','--capture',$capture,'--secret','DO-NOT-LOG')
Assert-Equal 0 $success.ExitCode 'valid pointer must launch'
Assert-Contains (Get-Content -Raw $capture) '--settings' 'shared settings must be passed'
Assert-NotContains (Get-Content -Raw $success.Log) 'DO-NOT-LOG' 'arguments must not be logged'

Assert-Equal 21 (Invoke-Launcher -Pointer '..\evil.exe').ExitCode 'paths must be rejected'
Assert-Equal 22 (Invoke-Launcher -Pointer 'ESAPI-Runner-Hub.v8.8.8.exe').ExitCode 'missing target'
Assert-Equal 23 (Invoke-Launcher -Pointer 'ESAPI-Runner-Hub.v9.9.9.exe' -OmitSettings).ExitCode 'missing settings'
Assert-Equal 7 (Invoke-Launcher -Pointer 'ESAPI-Runner-Hub.v9.9.9.exe' -Arguments @('--mode','exit7')).ExitCode 'child exit code'
```

The helper must restore `%LOCALAPPDATA%` and remove its exact temporary root in `finally`.

- [ ] **Step 2: Run the test and verify RED**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Test-CitrixLauncher.ps1
```

Expected: FAIL because `citrix/Start-ESAPI-Runner-Hub.cmd` does not exist.

- [ ] **Step 3: Commit the failing test**

```powershell
git add tests/Test-CitrixLauncher.ps1
git commit -m "test: define Citrix launcher contract"
```

### Task 2: Implement the stable batch launcher

**Files:**
- Create: `citrix/Start-ESAPI-Runner-Hub.cmd`
- Create: `citrix/current.txt`

- [ ] **Step 1: Implement the minimal launcher**

Use this behavior and exit-code contract:

```bat
@echo off
setlocal EnableExtensions DisableDelayedExpansion
set "LAUNCHER_DIR=%~dp0"
for %%D in ("%LAUNCHER_DIR%..") do set "ROOT=%%~fD"
set "POINTER=%LAUNCHER_DIR%current.txt"
set "VERSIONS=%ROOT%\dist\versions"
set "SETTINGS=%ROOT%\dist\settings.ini"
set "LOGDIR=%LOCALAPPDATA%\ESAPI-Runner-Hub\Logs"
if not exist "%LOGDIR%" md "%LOGDIR%" >nul 2>&1
set "LOGFILE=%LOGDIR%\CitrixLauncher.log"

if not exist "%POINTER%" goto PointerMissing
set "TARGET_FILE="
set "POINTER_EXTRA="
for /f "usebackq delims=" %%I in ("%POINTER%") do (
  if defined TARGET_FILE (set "POINTER_EXTRA=1") else set "TARGET_FILE=%%I"
)
if not defined TARGET_FILE goto PointerInvalid
if defined POINTER_EXTRA goto PointerInvalid
%SystemRoot%\System32\findstr.exe /R /X /I /C:"ESAPI-Runner-Hub\.v[0-9][0-9]*\.[0-9][0-9]*\.[0-9][0-9]*\.exe" "%POINTER%" >nul
if errorlevel 1 goto PointerInvalid
set "TARGET=%VERSIONS%\%TARGET_FILE%"
if not exist "%TARGET%" goto TargetMissing
if not exist "%SETTINGS%" goto SettingsMissing
call :Log START release=%TARGET_FILE%
pushd "%VERSIONS%" || goto WorkingDirectoryFailed
start "" /wait "%TARGET%" --settings "%SETTINGS%" %*
set "CHILD_EXIT=%ERRORLEVEL%"
popd
call :Log EXIT release=%TARGET_FILE% code=%CHILD_EXIT%
exit /b %CHILD_EXIT%

:PointerMissing
call :Log ERROR code=20 reason=pointer_missing
>&2 echo ESAPI Runner Hub: Die Versionsauswahl current.txt fehlt.
exit /b 20
:PointerInvalid
call :Log ERROR code=21 reason=pointer_invalid
>&2 echo ESAPI Runner Hub: current.txt enthaelt keinen gueltigen Versions-Dateinamen.
exit /b 21
:TargetMissing
call :Log ERROR code=22 reason=target_missing
>&2 echo ESAPI Runner Hub: Die ausgewaehlte Programmversion wurde nicht gefunden.
exit /b 22
:SettingsMissing
call :Log ERROR code=23 reason=settings_missing
>&2 echo ESAPI Runner Hub: Die gemeinsame settings.ini wurde nicht gefunden.
exit /b 23
:WorkingDirectoryFailed
call :Log ERROR code=24 reason=working_directory
>&2 echo ESAPI Runner Hub: Das Versionsverzeichnis ist nicht erreichbar.
exit /b 24
:Log
>>"%LOGFILE%" echo [%date% %time%] %*
exit /b 0
```

Write `citrix/current.txt` as UTF-8 without BOM with exactly:

```text
ESAPI-Runner-Hub.v0.1.3.exe
```

- [ ] **Step 2: Run the launcher test and verify GREEN**

Run the PowerShell test from Task 1. Expected: all named cases print `PASS`; process exit code 0.

- [ ] **Step 3: Commit**

```powershell
git add citrix tests/Test-CitrixLauncher.ps1
git commit -m "feat: add stable Citrix version launcher"
```

### Task 3: Make releases immutable and version-addressed

**Files:**
- Modify: `tools/build-release.ps1`
- Modify: `tests/ESAPI.RunnerHub.Tests/ReleaseMetadataTests.cs`

- [ ] **Step 1: Add a failing release-shape test**

Register and implement this test in `ReleaseMetadataTests.cs`:

```csharp
TestHarness.Test("release build publishes immutable versioned Citrix binaries", PublishesImmutableCitrixBinary);

private static void PublishesImmutableCitrixBinary()
{
    var script = File.ReadAllText(TestHarness.PathFromRoot("tools/build-release.ps1"));
    TestHarness.AssertContains(script, "'dist\\versions'");
    TestHarness.AssertContains(script, "ESAPI-Runner-Hub.v$version.exe");
    TestHarness.AssertContains(script, "Existing versioned binary has a different SHA-256");
    TestHarness.AssertFalse(
        script.Contains("-Destination (Join-Path $distRoot 'ESAPI-Runner-Hub.exe')"),
        "The release build must not overwrite the Citrix-published legacy path.");
}
```

- [ ] **Step 2: Run the C# tests and verify RED**

Run the existing Release x64 build and test executable. Expected: the new immutable-release test fails against the current overwrite behavior.

- [ ] **Step 3: Implement immutable publication**

Add this setup near the existing ZIP variables:

```powershell
$versionInfo = Get-Content -Raw -LiteralPath (Join-Path $repoRoot 'versionInfo.json') | ConvertFrom-Json
$version = [string]$versionInfo.version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Invalid release version: $version" }
$versionedDist = Join-Path $repoRoot 'dist\versions'
$versionedExeName = "ESAPI-Runner-Hub.v$version.exe"
$versionedExePath = Join-Path $versionedDist $versionedExeName
```

Add this function next to `Copy-FileWithRetry`:

```powershell
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
```

Create `$versionedDist` with `New-Item -ItemType Directory -Force`, replace the legacy EXE copy with:

```powershell
Publish-ImmutableBinary `
    -Source (Join-Path $packageRoot 'ESAPI-Runner-Hub.exe') `
    -Destination $versionedExePath
```

Keep ZIP and manifest generation unchanged and do not touch `dist/settings.ini`.

- [ ] **Step 4: Run C# and launcher tests and verify GREEN**

Expected: both suites exit 0.

- [ ] **Step 5: Commit**

```powershell
git add tools/build-release.ps1 tests/ESAPI.RunnerHub.Tests/ReleaseMetadataTests.cs
git commit -m "fix: publish immutable Citrix runner versions"
```

### Task 4: Document and version v0.1.3

**Files:**
- Create: `citrix/README-Citrix.md`
- Modify: `README.md`
- Modify: `src/ESAPI.RunnerHub/Properties/AssemblyInfo.cs`
- Modify: `versionInfo.json`
- Modify: `CHANGELOG.md`
- Modify: `tests/ESAPI.RunnerHub.Tests/ReleaseMetadataTests.cs`

- [ ] **Step 1: Change metadata expectations first and verify RED**

Change the existing metadata test name to `release metadata identifies version 0.1.3 build 4` and its assertions to:

```csharp
TestHarness.AssertContains(version, "\"version\": \"0.1.3\"");
TestHarness.AssertContains(version, "\"build\": 4");
TestHarness.AssertContains(changelog, "## [0.1.3] - 2026-08-04");
```

Add an assembly metadata assertion:

```csharp
var assemblyInfo = File.ReadAllText(TestHarness.PathFromRoot("src/ESAPI.RunnerHub/Properties/AssemblyInfo.cs"));
TestHarness.AssertContains(assemblyInfo, "AssemblyVersion(\"0.1.3.0\")");
TestHarness.AssertContains(assemblyInfo, "AssemblyFileVersion(\"0.1.3.0\")");
```

- [ ] **Step 2: Update product metadata and documentation**

Set both assembly versions to `0.1.3.0`. Set `versionInfo.json` to version `0.1.3`, build `4`, date `2026-08-04`, and prepend a build-4 entry with these changes:

```json
[
  "Citrix Studio verwendet einen stabilen CMD-Einstieg und eine austauschbare current.txt.",
  "Runner-Binaries werden versionsbezogen und unveraenderlich unter dist/versions bereitgestellt.",
  "Die bestehende dist/settings.ini bleibt die einzige Live-Konfiguration und wird explizit uebergeben.",
  "Start-, Fehler- und Exit-Ereignisse werden ohne Argumente oder Patientendaten lokal protokolliert."
]
```

Prepend this changelog section:

```markdown
## [0.1.3] - 2026-08-04

### Added

- Stable Citrix batch entry point with an editable version pointer and immutable versioned Hub binaries.
- Documented Studio command, release switch, rollback, local technical log, and launcher exit codes.

### Fixed

- Release builds no longer overwrite the legacy live executable path or risk replacing `dist/settings.ini`.
```

Create `citrix/README-Citrix.md` with the exact executable `C:\Windows\System32\cmd.exe`, the approved `/d /s /c` UNC argument, the UNC working directory, exit codes 20-24, `%LOCALAPPDATA%\ESAPI-Runner-Hub\Logs\CitrixLauncher.log`, pointer switch and rollback commands, and the preserved settings rule. Add a short `Stable Citrix launcher` section to the public README that describes the relative layout without clinic-specific UNC paths.

- [ ] **Step 3: Run all tests and verify GREEN**

Run the Release build, C# suite, and PowerShell launcher suite. Expected: 0 failures.

- [ ] **Step 4: Commit**

```powershell
git add README.md CHANGELOG.md versionInfo.json src/ESAPI.RunnerHub/Properties/AssemblyInfo.cs citrix/README-Citrix.md tests/ESAPI.RunnerHub.Tests/ReleaseMetadataTests.cs
git commit -m "release: prepare ESAPI Runner Hub v0.1.3"
```

### Task 5: Build, deploy, and verify the live Citrix path

**Files:**
- Runtime output: `dist/versions/ESAPI-Runner-Hub.v0.1.3.exe`
- Runtime pointer: `citrix/current.txt`
- Preserve: `dist/settings.ini`

- [ ] **Step 1: Capture the live settings hash**

```powershell
Get-FileHash .\dist\settings.ini -Algorithm SHA256
```

- [ ] **Step 2: Build the release**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\build-release.ps1
```

Expected: MSBuild succeeds, all C# tests pass, vendor validation passes, ZIP is created, and the versioned binary is published without accessing the blocked legacy EXE.

- [ ] **Step 3: Verify final UNC artifacts**

Check v0.1.3 file version, non-zero length, SHA-256, pointer text, and unchanged `settings.ini` hash.

- [ ] **Step 4: Smoke-launch through the stable Citrix command**

```powershell
cmd.exe /d /s /c "".\citrix\Start-ESAPI-Runner-Hub.cmd" --offline-ui-smoke"
```

Confirm that the window opens from the selected versioned binary, close it normally, and verify exit code 0 plus privacy-safe launcher log.

- [ ] **Step 5: Merge, push, publish, and verify Hub visibility**

Merge the focused branch to `main`, push configured remotes and GitHub, tag/release `v0.1.3` with ZIP and SHA-256 assets, then verify the live Hub/InHouse version resolver reports `0.1.3` build 4.
