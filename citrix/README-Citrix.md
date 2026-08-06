# ESAPI Runner Hub in Citrix Studio

Citrix Studio should reference the stable launcher rather than a Hub release binary. New Hub versions then receive new filenames, while `current.txt` selects the version used for subsequent launches.

## Published application

### Recommended: direct EXE launcher

Executable:

```text
\\medizin.uni-leipzig.de\data\Archiv\STR\STR-Physik\11. Scripting\ESAPI-MG\ESAPI-Runner-Hub\citrix\ESAPI-Runner-Hub.CitrixLauncher.exe
```

Arguments:

Leave the Arguments field empty.

Working directory:

```text
\\medizin.uni-leipzig.de\data\Archiv\STR\STR-Physik\11. Scripting\ESAPI-MG\ESAPI-Runner-Hub\citrix
```

This creates one stable published entry point for the full configured catalogue. The published executable is not a shell: it resolves only the strictly validated filename from `current.txt` and starts that file directly through the Windows process API. The normal UI start and the automated debug workflow do not require client arguments to reach the VDA. Argument values are never written to launcher logs.

After changing application properties, fully log off the existing Citrix session before testing. Citrix application changes can otherwise remain invisible to launches routed into that session.

### Confirmed no-argument CMD fallback

The previously working no-argument configuration remains available if the direct UNC executable cannot be published.

Executable:

```text
C:\Windows\System32\cmd.exe
```

Arguments:

```text
/d /c call "\\medizin.uni-leipzig.de\data\Archiv\STR\STR-Physik\11. Scripting\ESAPI-MG\ESAPI-Runner-Hub\citrix\Start-ESAPI-Runner-Hub.cmd"
```

Do not replace `/c call` with `/call`; `cmd.exe` has no `/call` switch. Do not add a Studio argument-forwarding placeholder for the normal Hub or debugging workflow. Nested CMD quoting and Citrix Workspace forwarding were not reliable in the deployed environment.

For a deterministic patient/course/plan test from a workstation, create a shared request and wait for its result:

```powershell
& '..\tools\Invoke-CitrixContextDebug.ps1' -ApplicationId plugin-color-code -PatientId PATIENT-ID -CourseId C7 -PlanId PLAN-ID
```

The helper writes a user-scoped shared request and result JSON below the protected directory configured as `Hub.ContextRequestDirectory`. In live operation this is the `requests` child of the same central `LogDirectory`, so readable process logs and request history stay together. These files may contain clinical IDs by local policy. The helper records the Windows SID in the JSON, creates `<SID>.pending`, and opens the ordinary installed Citrix shortcut without parameters. The Runner on the assigned VDA atomically claims only that SID's marker and verifies ownership again before execution. A different user cannot accidentally start or consume it, including through `--run-request`. The pending marker is claimable for at most 30 seconds by default and normally disappears within seconds; request and result JSON remain as readable history. This workflow is independent of client parameter forwarding and VDA-local `latest` history. Request JSON may be UTF-8 with or without a byte-order mark.

The corresponding live settings are intentionally visible and editable in the Hub:

```ini
LogDirectory=\\medizin.uni-leipzig.de\data\Archiv\STR\STR-Physik\11. Scripting\Logs\ESAPI-Runner-Hub
ContextRequestDirectory=\\medizin.uni-leipzig.de\data\Archiv\STR\STR-Physik\11. Scripting\Logs\ESAPI-Runner-Hub\requests
```

A direct VDA shell does not depend on the Studio placeholder, for example:

```powershell
cmd.exe /d /s /c ""\\medizin.uni-leipzig.de\data\Archiv\STR\STR-Physik\11. Scripting\ESAPI-MG\ESAPI-Runner-Hub\citrix\Start-ESAPI-Runner-Hub.cmd" --replay-latest plugin-fb-info"
```

This reuses the latest DPAPI-protected context for the current Windows user on that VDA. It is only a local smoke test. Do not use `SelfService.exe -qlaunch ... <arguments>` as the automated workstation interface: client parameter forwarding was not reliable in the deployed Workspace configuration and could stop in `wfcrun32.exe` before any Runner process started.

`--replay-latest` is only the shortest smoke test. A specific patient and planning context is supplied privately from a PowerShell session on the VDA:

```powershell
$env:ESAPI_RUNNER_CONTEXT_PATIENT = 'PATIENT-ID'
$env:ESAPI_RUNNER_CONTEXT_COURSE = 'COURSE-ID'
$env:ESAPI_RUNNER_CONTEXT_PLAN = 'PLAN-ID'
cmd.exe /d /s /c ""\\medizin.uni-leipzig.de\data\Archiv\STR\STR-Physik\11. Scripting\ESAPI-MG\ESAPI-Runner-Hub\citrix\Start-ESAPI-Runner-Hub.cmd" --run-context plugin-fb-info"
Remove-Item Env:ESAPI_RUNNER_CONTEXT_PATIENT, Env:ESAPI_RUNNER_CONTEXT_COURSE, Env:ESAPI_RUNNER_CONTEXT_PLAN
```

For several selected contexts, use one private JSON environment value. The same read-only script is started sequentially and the series stops on the first error:

```powershell
$series = @{ Contexts = @(
    @{ PatientId = 'PATIENT-A'; CourseId = 'C1'; PlanId = 'P1' },
    @{ PatientId = 'PATIENT-B'; CourseId = 'C2'; PlanId = 'P2' }
) }
$env:ESAPI_RUNNER_CONTEXTS = $series | ConvertTo-Json -Depth 4 -Compress
cmd.exe /d /s /c ""\\medizin.uni-leipzig.de\data\Archiv\STR\STR-Physik\11. Scripting\ESAPI-MG\ESAPI-Runner-Hub\citrix\Start-ESAPI-Runner-Hub.cmd" --run-contexts plugin-fb-info"
Remove-Item Env:ESAPI_RUNNER_CONTEXTS
```

Direct CMD calls must execute inside the Citrix VDA session. Calling the shared CMD directly on a normal workstation starts the binary on that workstation; it does not move the process into Citrix and therefore cannot load ESAPI there. From a workstation, use `Invoke-CitrixContextDebug.ps1`, which writes the exact request before opening the ordinary Citrix shortcut. The Runner itself does not create a general remote shell.

## Runtime layout

```text
ESAPI-Runner-Hub\
  citrix\
    ESAPI-Runner-Hub.CitrixLauncher.exe
    Start-ESAPI-Runner-Hub.cmd
    current.txt
  dist\
    settings.ini
    versions\
      ESAPI-Runner-Hub.v0.3.2.exe
```

`dist\settings.ini` is the only live configuration. The launcher passes it with `--settings`; do not copy a second settings file into `dist\versions`.

## Activate a release

First verify that the new versioned EXE exists, has the expected file version, and has the release SHA-256. Then replace the pointer atomically:

```powershell
$citrix = '\\medizin.uni-leipzig.de\data\Archiv\STR\STR-Physik\11. Scripting\ESAPI-MG\ESAPI-Runner-Hub\citrix'
$next = Join-Path $citrix 'current.txt.new'
$current = Join-Path $citrix 'current.txt'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($next, "ESAPI-Runner-Hub.v0.3.2.exe`r`n", $utf8NoBom)
Move-Item -LiteralPath $next -Destination $current -Force
```

Already running sessions continue with their old version. New launches use the pointer's version.

## Roll back

Retain a previously verified binary in `dist\versions`, then repeat the pointer replacement with its filename. No Citrix Studio change is required.

## Exit codes

- `20`: `current.txt` is missing.
- `21`: the pointer is empty, contains multiple entries, or is not a permitted versioned EXE filename.
- `22`: the selected versioned EXE is missing.
- `23`: the shared `dist\settings.ini` is missing.
- `24`: `dist\versions` cannot be used as the working directory.
- `25`: the EXE launcher encountered an unexpected local start failure.
- `2`: a Hub context command was invalid, unavailable, or could not be started.
- `10`: the isolated Script Host ended after a handled script or context failure.
- Any other non-zero value is the Hub process exit code.

Launcher events are written without arguments or patient data to the `LogDirectory` configured in `dist\settings.ini`, using one file per VDA:

```text
<LogDirectory>\CitrixLauncher-<Computer>.log
```

If that location cannot be resolved, the launcher falls back to `%LOCALAPPDATA%\ESAPI-Runner-Hub\Logs\CitrixLauncher.log`. The Hub continues to use the configured technical log directory from `dist\settings.ini`. Both launchers treat logging as optional so an unavailable network log cannot prevent startup.

## Smoke test

Run only inside an existing VDA session:

```powershell
cmd.exe /d /s /c ""\\medizin.uni-leipzig.de\data\Archiv\STR\STR-Physik\11. Scripting\ESAPI-MG\ESAPI-Runner-Hub\citrix\Start-ESAPI-Runner-Hub.cmd" --offline-ui-smoke"
```

Close the synthetic Hub window normally and confirm exit code `0` plus matching `START` and `EXIT` entries in the local launcher log.
