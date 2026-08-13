# Final verification checklist

Verified for public release v0.3.5 and the synchronized internal Citrix deployment contract on 2026-08-08.

- [x] Existing Hub UI, patient/context selection, privacy mode, activity replay, and catalogue filtering remain intact
- [x] Child exits return to the captured WPF UI context and no longer close the Hub
- [x] Incomplete `Starting` or `Running` history is recovered as replayable `Interrupted` history
- [x] A history patient can be selected without launching the recorded application
- [x] One stable Citrix Published Application remains the only Runner entry point
- [x] Read-only direct scripts route only to `ESAPI-Script-Host.exe`
- [x] `ConfirmSave` and `ExecuteAndDiscard` route only to `ESAPI-Write-Script-Host.exe`
- [x] Host capability is validated before an ESAPI session or patient is opened
- [x] Host application, patient, save, close, and disposal operations use compile-time Eclipse 18 API calls
- [x] Classic Eclipse child scripts are invoked through typed delegates rather than `MethodInfo.Invoke`
- [x] Synthetic `AddExternalPlanSetup` fails through the former reflection boundary and succeeds through the released typed boundary
- [x] Only the write host carries `ESAPIScript(IsWriteable=true)`
- [x] Child assembly write metadata must match the configured mode
- [x] `ConfirmSave` saves at most once and only after explicit confirmation
- [x] `ExecuteAndDiscard` never asks to save and never calls `SaveModifications()`
- [x] Failure, crash, discard, and abnormal exit never save
- [x] Context series remain restricted to `WriteMode=ReadOnly`
- [x] Both host paths are editable in `settings.ini` and the English Settings GUI
- [x] Missing write-host configuration is an error only when an enabled direct write entry exists
- [x] Public release package contains both hosts and no vendor assemblies
- [x] 159 automated tests pass
- [x] CMD launcher contract passes with 0 failures
- [x] EXE launcher contract passes with 0 failures
- [x] Deterministic v0.3.5 ZIP and immutable Citrix binary were created
- [x] `citrix/current.txt` selects `ESAPI-Runner-Hub.v0.3.5.exe`
- [x] Read host and stable Citrix launcher remain byte-identical to v0.3.3
- [x] Write host has the separately versioned v0.3.4 binary identity
- [x] Offline UI smoke remained running and responsive for five seconds and was then closed by the verification harness
- [ ] Exact write-host binary registered, evaluated, validated, and approved in Eclipse Script Administration
- [ ] Live write-script matrix completed with approved synthetic test data

The unchecked items are intentionally clinical workstation gates. The v0.3.4 write host is a new exact binary and must be approved before its live write-script matrix can pass. Automated and synthetic evidence does not replace local approval or validation of each configured clinical application. The protocol is defined in [CLINICAL_VALIDATION.md](CLINICAL_VALIDATION.md).
