# ESAPI Runner Hub in Citrix Studio

Citrix Studio should reference the stable launcher rather than a Hub release binary. New Hub versions then receive new filenames, while `current.txt` selects the version used for subsequent launches.

## Published application

Executable:

```text
C:\Windows\System32\cmd.exe
```

Arguments:

```text
/d /s /c ""\\medizin.uni-leipzig.de\data\Archiv\STR\STR-Physik\11. Scripting\ESAPI-MG\ESAPI-Runner-Hub\citrix\Start-ESAPI-Runner-Hub.cmd""
```

Working directory:

```text
\\medizin.uni-leipzig.de\data\Archiv\STR\STR-Physik\11. Scripting\ESAPI-MG\ESAPI-Runner-Hub\citrix
```

The Citrix entry remains unchanged for future releases.

The existing launcher already forwards additional arguments. No Citrix Studio change is required for command-line diagnosis. From a VDA shell, for example:

```powershell
cmd.exe /d /s /c ""\\medizin.uni-leipzig.de\data\Archiv\STR\STR-Physik\11. Scripting\ESAPI-MG\ESAPI-Runner-Hub\citrix\Start-ESAPI-Runner-Hub.cmd" --replay-latest plugin-fb-info"
```

This reuses the latest DPAPI-protected context for the current Windows user. A separately published debug application is optional, not required by the runtime contract.

## Runtime layout

```text
ESAPI-Runner-Hub\
  citrix\
    Start-ESAPI-Runner-Hub.cmd
    current.txt
  dist\
    settings.ini
    versions\
      ESAPI-Runner-Hub.v0.1.3.exe
```

`dist\settings.ini` is the only live configuration. The launcher passes it with `--settings`; do not copy a second settings file into `dist\versions`.

## Activate a release

First verify that the new versioned EXE exists, has the expected file version, and has the release SHA-256. Then replace the pointer atomically:

```powershell
$citrix = '\\medizin.uni-leipzig.de\data\Archiv\STR\STR-Physik\11. Scripting\ESAPI-MG\ESAPI-Runner-Hub\citrix'
$next = Join-Path $citrix 'current.txt.new'
$current = Join-Path $citrix 'current.txt'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($next, "ESAPI-Runner-Hub.v0.1.3.exe`r`n", $utf8NoBom)
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
- `2`: a Hub context command was invalid, unavailable, or could not be started.
- `10`: the isolated Script Host ended after a handled script or context failure.
- Any other non-zero value is the Hub process exit code.

Launcher events are written without arguments or patient data to:

```text
%LOCALAPPDATA%\ESAPI-Runner-Hub\Logs\CitrixLauncher.log
```

The Hub continues to use the configured technical log directory from `dist\settings.ini`. The batch launcher deliberately performs no optional network-log probe before startup.

## Smoke test

Run from a workstation or VDA:

```powershell
cmd.exe /d /s /c ""\\medizin.uni-leipzig.de\data\Archiv\STR\STR-Physik\11. Scripting\ESAPI-MG\ESAPI-Runner-Hub\citrix\Start-ESAPI-Runner-Hub.cmd" --offline-ui-smoke"
```

Close the synthetic Hub window normally and confirm exit code `0` plus matching `START` and `EXIT` entries in the local launcher log.
