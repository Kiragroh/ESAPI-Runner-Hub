# Release verification: v0.3.3

Date: 2026-08-08

This record covers the v0.3.3 source, vendor-free release artifacts, dual-host routing, and Citrix activation contract. It does not claim clinical approval of the write host or configured child applications.

## Automated gates

Authoritative command:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\build-release.ps1
```

- x64 .NET Framework 4.8 release build against Eclipse 18 metadata references
- 152 automated tests passed, 0 failed
- CMD launcher tests passed, 0 failed
- shell-free EXE launcher tests passed, 0 failed
- vendor-free package validation passed
- deterministic ZIP generation passed
- both Script Hosts, their UNC-loading configurations, and the context-debugging guide included

## Artifacts

- Citrix binary: `dist\versions\ESAPI-Runner-Hub.v0.3.3.exe`
- Citrix binary file version: `0.3.3.0`
- Citrix binary SHA-256: `b87407631fd13fc61615e225a5f18ba43746499e22abefd77f3b4a7e9d8e1cf9`
- Read-only host file version: `0.3.3.0`
- Read-only host SHA-256: `15238c1aab0748bda241051d1edbd7f3cd31a492e1fa91ee0b27d245bdbd5c13`
- Write host file version: `0.3.3.0`
- Write host SHA-256: `1cd14951828ff5c73ae38e91c98d80f5241945add47aa8dfd9f5fb6b3bb12234`
- Release ZIP: `dist\ESAPI-Runner-Hub-v0.3.3-win-x64.zip`
- Release ZIP SHA-256: `3fb03e3f160d771dd969c879d16e306222f508b781c4ee9d33003e1bdd7bd748`
- Active pointer: `citrix\current.txt` -> `ESAPI-Runner-Hub.v0.3.3.exe`

## Dual-host verification

- `ReadOnly` requests select `ESAPI-Script-Host.exe`; `ConfirmSave` and `ExecuteAndDiscard` select `ESAPI-Write-Script-Host.exe` from validated settings.
- The read host contains no `ESAPIScriptAttribute`; the write host contains `ESAPIScript(IsWriteable=true)`.
- Each executable refuses a mode outside its capability before ESAPI session creation.
- `ExecuteAndDiscard` runs a synthetic write-enabled child without presenting the save callback and without calling `SaveModifications()`.
- `ConfirmSave` retains a single explicit save decision; failure and discard never save.
- Read-only and write-enabled context series remain prohibited by configuration and command-line tests as specified: only read-only series are accepted.

## Live configuration integrity

The ignored `dist/settings.ini` remains the sole live configuration. Deployment adds `WriteScriptHostExecutable=ESAPI-Write-Script-Host.exe` while preserving existing application sections, paths, arguments, and the stable Citrix Studio application entry.

## Clinical boundary

The released write host is a new standalone write-enabled ESAPI executable. Its exact SHA-256 must be registered, evaluated, validated, and approved in Eclipse Script Administration before write-mode clinical testing. Rebuilding it creates a new approval candidate. Each child write script remains independently governed; host approval is not approval of dynamically loaded child code.
