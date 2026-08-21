# Release verification: v0.3.7

Date: 2026-08-21

This record covers the sanitized public v0.3.7 source, vendor-free release artifacts, visible classic plug-in windows, dual-host routing, and Citrix activation contract. It does not claim clinical approval of configured child applications.

## Automated gates

Authoritative command:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\build-release.ps1
```

- x64 .NET Framework 4.8 release build against Eclipse 18 metadata references
- 162 automated tests passed, 0 failed
- CMD launcher tests passed, 0 failed
- shell-free EXE launcher tests passed, 0 failed
- vendor-free package validation passed
- deterministic ZIP generation passed
- both Script Hosts, their UNC-loading configurations, and the context-debugging guide included

## Artifacts

- Citrix binary: `dist\versions\ESAPI-Runner-Hub.v0.3.7.exe`
- Citrix binary file version: `0.3.7.0`
- Citrix binary SHA-256: `8c5aed46c75b643f9299df81c95a53314cca6d3c4666215c5e739037ab8b8bb`
- Read-only host file version: `0.3.4.0`
- Read-only host SHA-256: `d5a8b9345194305bf7beaf959f5101f5fb6113abbee80689e1739fb5a4d46e0f`
- Write host file version: `0.3.5.0`
- Write host SHA-256: `0821b6e102545972bb5160913f588be0de387942e617a086046deef6076330c0`
- Stable Citrix launcher file version: `0.3.3.0`
- Stable Citrix launcher SHA-256: `cbfa57d02ae1b3d1e1592eae95c452e821c7f2c6940cde4b6e051615003a4756`
- Release ZIP: `dist\ESAPI-Runner-Hub-v0.3.7-win-x64.zip`
- Release ZIP SHA-256: `c17a4bc7ec400d138845ce43f75199cf2a267fa818e0727aadca5761ef3156d3`
- Active pointer: `citrix\current.txt` -> `ESAPI-Runner-Hub.v0.3.7.exe`

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
- A two-parameter plug-in receives one host-owned `Window`; when the child configures content but has not already displayed it, the host calls `ShowDialog()` and remains alive until the window closes.
- A synthetic two-parameter plug-in verifies the Window loaded event and closes itself, so the lifecycle is covered without manual interaction.
- The synthetic Eclipse API rejects a stack containing reflection when `AddExternalPlanSetup` or `SaveModifications` is reached. The released regression passes typed plan creation, single-save, discard, and failure paths.
- The loaded API assembly identity is checked against the host's compile-time reference before context resolution.

## Live configuration integrity

The ignored `dist/settings.ini` remains the sole live configuration. Deployment adds `WriteScriptHostExecutable=ESAPI-Write-Script-Host.exe` while preserving existing application sections, paths, arguments, and the stable Citrix Studio application entry.

## Configurable tool integration

- A write-enabled Eclipse plug-in can be configured with an explicit plan or PlanSum requirement and confirm-save handling.
- A separate read-only diagnostic can collect technical context evidence without a completion message box.
- The example catalogue uses placeholders only and contains no institutional or clinical paths.

## Clinical boundary

The v0.3.7 public release keeps the stable Citrix launcher at v0.3.3, changes the read host to v0.3.4, and changes the write host to v0.3.5 for the corrected Window lifecycle. The write host and every write-enabled child plug-in must be registered, evaluated, validated, and approved as their exact released binaries before live write use. Each child write script remains independently governed; host approval is not approval of dynamically loaded child code.
