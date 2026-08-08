# Final verification checklist

Verified for public release v0.3.4 and the synchronized internal Citrix deployment contract on 2026-08-08.

- [x] Existing Hub UI, patient/context selection, privacy mode, activity replay, and catalogue filtering remain intact
- [x] Child exits return to the captured WPF UI context and no longer close the Hub
- [x] Incomplete `Starting` or `Running` history is recovered as replayable `Interrupted` history
- [x] A history patient can be selected without launching the recorded application
- [x] One stable Citrix Published Application remains the only Runner entry point
- [x] Read-only direct scripts route only to `ESAPI-Script-Host.exe`
- [x] `ConfirmSave` and `ExecuteAndDiscard` route only to `ESAPI-Write-Script-Host.exe`
- [x] Host capability is validated before an ESAPI session or patient is opened
- [x] Only the write host carries `ESAPIScript(IsWriteable=true)`
- [x] Child assembly write metadata must match the configured mode
- [x] `ConfirmSave` saves at most once and only after explicit confirmation
- [x] `ExecuteAndDiscard` never asks to save and never calls `SaveModifications()`
- [x] Failure, crash, discard, and abnormal exit never save
- [x] Context series remain restricted to `WriteMode=ReadOnly`
- [x] Both host paths are editable in `settings.ini` and the English Settings GUI
- [x] Missing write-host configuration is an error only when an enabled direct write entry exists
- [x] Public release package contains both hosts and no vendor assemblies
- [x] 158 automated tests pass
- [x] CMD launcher contract passes with 0 failures
- [x] EXE launcher contract passes with 0 failures
- [x] Deterministic v0.3.4 ZIP and immutable Citrix binary were created
- [x] `citrix/current.txt` selects `ESAPI-Runner-Hub.v0.3.4.exe`
- [x] Read host, write host, and stable Citrix launcher remain byte-identical to v0.3.3
- [x] Offline UI smoke remained responsive for five seconds and closed with exit code 0
- [ ] Exact write-host binary registered, evaluated, validated, and approved in Eclipse Script Administration
- [ ] Live write-script matrix completed with approved synthetic test data

The unchecked items are intentionally clinical workstation gates. Until the released write host is approved, an authorization failure at `BeginModifications()` is expected. Automated and synthetic evidence does not replace local approval or validation of each configured clinical application. The protocol is defined in [CLINICAL_VALIDATION.md](CLINICAL_VALIDATION.md).
