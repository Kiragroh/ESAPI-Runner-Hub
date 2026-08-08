# Release verification: v0.3.4

Date: 2026-08-08

This record covers the v0.3.4 source, vendor-free release artifacts, resilient history actions, dual-host routing, and Citrix activation contract. It does not claim clinical approval of configured child applications.

## Automated gates

Authoritative command:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\build-release.ps1
```

- x64 .NET Framework 4.8 release build against Eclipse 18 metadata references
- 158 automated tests passed, 0 failed
- CMD launcher tests passed, 0 failed
- shell-free EXE launcher tests passed, 0 failed
- vendor-free package validation passed
- deterministic ZIP generation passed
- both Script Hosts, their UNC-loading configurations, and the context-debugging guide included

## Artifacts

- Citrix binary: `dist\versions\ESAPI-Runner-Hub.v0.3.4.exe`
- Citrix binary file version: `0.3.4.0`
- Citrix binary SHA-256: `c9bbe0329145c0864d4f7f1c8b5de79563a1bb00820a06d61b361751f08e6f9f`
- Read-only host file version: `0.3.3.0`
- Read-only host SHA-256: `15238c1aab0748bda241051d1edbd7f3cd31a492e1fa91ee0b27d245bdbd5c13`
- Write host file version: `0.3.3.0`
- Write host SHA-256: `1cd14951828ff5c73ae38e91c98d80f5241945add47aa8dfd9f5fb6b3bb12234`
- Stable Citrix launcher file version: `0.3.3.0`
- Stable Citrix launcher SHA-256: `cbfa57d02ae1b3d1e1592eae95c452e821c7f2c6940cde4b6e051615003a4756`
- Release ZIP: `dist\ESAPI-Runner-Hub-v0.3.4-win-x64.zip`
- Release ZIP SHA-256: `41b8c2d8441946ff6a96e11c5880d0abbbb11af3fd8b0fbe4abd01251419d22b`
- Active pointer: `citrix\current.txt` -> `ESAPI-Runner-Hub.v0.3.4.exe`

## History and UI verification

- Child process exit handling is posted to the captured WPF synchronization context before terminal state, persistence, and command availability are updated.
- Persisted nonterminal history is reconciled as `Interrupted` on startup and becomes replayable when the current catalogue and protected context permit it.
- **Select patient** resolves the DPAPI-protected patient ID against the current ESAPI directory and updates the Hub context without starting a child process.
- The productive offline UI smoke opened a real window, stayed alive for five seconds, and closed with exit code 0.

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

The v0.3.4 Hub release does not rebuild the stable helper identities: the read host, write host, and stable Citrix launcher are byte-identical to v0.3.3. A future helper rebuild creates a new approval candidate. Each child write script remains independently governed; host approval is not approval of dynamically loaded child code.
