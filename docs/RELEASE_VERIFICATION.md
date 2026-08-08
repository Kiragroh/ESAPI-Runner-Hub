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
- Citrix binary SHA-256: `d56449cb22e48670a3ddcdd9b0d599c198b231ed7a900c7929180cbd439493be`
- Read-only host file version: `0.3.3.0`
- Read-only host SHA-256: `53ec8f491f3de06a57867a66e54e60242a8743b3eaa500920218450e721f6153`
- Write host file version: `0.3.3.0`
- Write host SHA-256: `d5f24e2723d4eba3ee2a282d127ad921aeb04f63290e6a9100b21791937eccb0`
- Release ZIP: `dist\ESAPI-Runner-Hub-v0.3.3-win-x64.zip`
- Release ZIP SHA-256: `cc26cb0810b639d87182ce9499005dd35837d0d6498bda9f183686d852931530`
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
