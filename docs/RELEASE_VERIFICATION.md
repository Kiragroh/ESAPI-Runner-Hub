# Release verification: v0.3.5

Date: 2026-08-08

This record covers the v0.3.5 source, vendor-free release artifacts, reflection-free ESAPI write invocation, dual-host routing, and Citrix activation contract. It does not claim clinical approval of configured child applications.

## Automated gates

Authoritative command:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\build-release.ps1
```

- x64 .NET Framework 4.8 release build against Eclipse 18 metadata references
- 159 automated tests passed, 0 failed
- CMD launcher tests passed, 0 failed
- shell-free EXE launcher tests passed, 0 failed
- vendor-free package validation passed
- deterministic ZIP generation passed
- both Script Hosts, their UNC-loading configurations, and the context-debugging guide included

## Artifacts

- Citrix binary: `dist\versions\ESAPI-Runner-Hub.v0.3.5.exe`
- Citrix binary file version: `0.3.5.0`
- Citrix binary SHA-256: `a45ad05e3499ee57ce0467556e88eda0fea3c26aa5189006af4ca89ca9d4ae2c`
- Read-only host file version: `0.3.3.0`
- Read-only host SHA-256: `15238c1aab0748bda241051d1edbd7f3cd31a492e1fa91ee0b27d245bdbd5c13`
- Write host file version: `0.3.4.0`
- Write host SHA-256: `e8b0cfa4053d851c147eb83bf5413cf443feb3f121ffbdc1340a3abfb078d973`
- Stable Citrix launcher file version: `0.3.3.0`
- Stable Citrix launcher SHA-256: `cbfa57d02ae1b3d1e1592eae95c452e821c7f2c6940cde4b6e051615003a4756`
- Release ZIP: `dist\ESAPI-Runner-Hub-v0.3.5-win-x64.zip`
- Release ZIP SHA-256: `9658c0edee0bf0321c9426e39d43497dc2a1d03a851d77bfcd6864cba62d0cca`
- Active pointer: `citrix\current.txt` -> `ESAPI-Runner-Hub.v0.3.5.exe`

## History and UI verification

- Child process exit handling is posted to the captured WPF synchronization context before terminal state, persistence, and command availability are updated.
- Persisted nonterminal history is reconciled as `Interrupted` on startup and becomes replayable when the current catalogue and protected context permit it.
- **Select patient** resolves the DPAPI-protected patient ID against the current ESAPI directory and updates the Hub context without starting a child process.
- The productive offline UI smoke opened a real window, stayed alive and responsive for five seconds, and was then closed by the verification harness.

## Dual-host verification

- `ReadOnly` requests select `ESAPI-Script-Host.exe`; `ConfirmSave` and `ExecuteAndDiscard` select `ESAPI-Write-Script-Host.exe` from validated settings.
- The read host contains no `ESAPIScriptAttribute`; the write host contains `ESAPIScript(IsWriteable=true)`.
- Each executable refuses a mode outside its capability before ESAPI session creation.
- `ExecuteAndDiscard` runs a synthetic write-enabled child without presenting the save callback and without calling `SaveModifications()`.
- `ConfirmSave` retains a single explicit save decision; failure and discard never save.
- Read-only and write-enabled context series remain prohibited by configuration and command-line tests as specified: only read-only series are accepted.

## Typed ESAPI boundary verification

- The host calls `Application.CreateApplication`, `OpenPatientById`, `SaveModifications`, `ClosePatient`, and `Dispose` through its Eclipse 18 compile-time API reference.
- Classic `Execute(ScriptContext)` and `Execute(ScriptContext, Window)` methods are converted to typed delegates and invoked directly; public ESAPI operations are not called through `MethodInfo.Invoke`.
- The synthetic Eclipse API rejects a stack containing reflection when `AddExternalPlanSetup` or `SaveModifications` is reached. The released regression passes typed plan creation, single-save, discard, and failure paths.
- The loaded API assembly identity is checked against the host's compile-time reference before context resolution.

## Live configuration integrity

The ignored `dist/settings.ini` remains the sole live configuration. Deployment adds `WriteScriptHostExecutable=ESAPI-Write-Script-Host.exe` while preserving existing application sections, paths, arguments, and the stable Citrix Studio application entry.

## Clinical boundary

The v0.3.5 Hub release preserves the read host and stable Citrix launcher byte-for-byte at v0.3.3. The fixed write host is a new v0.3.4 approval candidate and must be registered, evaluated, validated, and approved as this exact SHA-256 before live write use. Each child write script remains independently governed; host approval is not approval of dynamically loaded child code.
