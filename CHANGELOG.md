# Changelog

All notable changes to ESAPI Runner Hub are documented here.

## [0.3.5] - 2026-08-08

### Fixed

- Classic Eclipse scripts are invoked through typed delegates rather than `MethodInfo.Invoke`, so write operations such as `AddExternalPlanSetup` execute without a reflection frame.
- ESAPI application creation, patient opening, saving, closing, and disposal now use compile-time ESAPI calls in the host.
- The host verifies that the loaded Eclipse 18 API matches its compile-time reference before resolving context or running a child script.

### Approval identity

- Only `ESAPI-Write-Script-Host.exe` changes to version 0.3.4 and therefore requires a new Eclipse Script Administration approval.
- `ESAPI-Script-Host.exe` and the Citrix launcher remain at version 0.3.3 and retain their existing identities.

### Validation

- A synthetic Eclipse 18 regression rejects reflective write calls and verifies typed `AddExternalPlanSetup`, single-save behavior, failure handling, and execute-without-save behavior.
- Release metadata now versions the read and write hosts independently.

## [0.3.4] - 2026-08-08

### Fixed

- Child process exits are marshalled to the captured WPF UI context, preventing the exit callback from closing the Hub.
- Terminal state, exit code, replay availability, and protected history are now persisted in one ordered UI-thread transition.
- History left as `Starting` or `Running` by an interrupted Hub session is recovered as `Interrupted` and can be replayed when its application and protected context remain available.

### Added

- Recent activity has a separate **Select patient** action that restores the current patient selection without launching the recorded application.
- Unavailable patient actions explain whether the run had no patient, its protected context is unreadable, or the patient is absent from the current ESAPI directory.

### Validation

- Regression coverage exercises background child exits, interrupted history recovery, exact patient lookup, no-launch patient restoration, unavailable-state explanations, and the compact two-action history layout.
- Hub-only releases declare separate script-host and Citrix-launcher versions and preserve existing version-matched helper binaries instead of changing their approval identities.

## [0.3.3] - 2026-08-08

### Added

- A separate `ESAPI-Write-Script-Host.exe` carries the ESAPI write-enabled assembly marker and provides a stable executable approval identity for reviewed write workflows.
- `WriteMode=ExecuteAndDiscard` executes inside the write-authorized host but always closes without saving and never presents a save question.
- `WriteScriptHostExecutable` is editable beside the existing read-only host path in `settings.ini` and the Settings window.

### Safety

- `ReadOnly` routes only to `ESAPI-Script-Host.exe`; `ConfirmSave` and `ExecuteAndDiscard` route only to the write host.
- Both executables reject a mismatched mode before opening an ESAPI session. Child script write metadata must agree with the selected mode, and context series remain read-only.
- The write host and each child write script retain their own institutional review, versioning, validation, and approval requirements.

### Validation

- Automated coverage verifies both path contracts, automatic routing, fail-closed host capabilities, write metadata, discard behavior, assembly markers, Settings integration, and release packaging.

## [0.3.2] - 2026-08-06

### Added

- The Plan editing catalogue includes the write-enabled **Export Plan Sum Dose** binary as a direct patient-context tool.
- The application links to its InHouse README and starts with the established confirm-save boundary.

### Validation

- The example configuration regression verifies patient context, Eclipse binary execution, write-enabled metadata, and confirm-save behavior.

## [0.3.1] - 2026-08-06

### Changed

- Application documentation actions copy the configured STR Hub URL to the clipboard instead of starting a browser or another Citrix session.
- The productive settings use an IP-reachable Hub base URL rather than `localhost`.
- Direct patient-context catalogue entries cover the single-file `GetDicomCollectionUKL.cs` exporter and compiled `ExportPlansQuicker.esapi.dll` exporter.

### Fixed

- The isolated source compiler includes `System.Windows.Forms`, allowing mixed WPF and Forms single-file ESAPI tools to compile.

### Validation

- Regression coverage verifies exact clipboard URLs, both new export-tool defaults, and Windows Forms source compilation.

## [0.3.0] - 2026-08-04

### Changed

- The catalogue uses a centered search/category/format filter bar, responsive compact cards, and no horizontal scrolling.
- Recent activity uses proportional columns and exposes a reusable `Run again` action whenever the configured application and protected context remain available.
- Visible Hub, Script Host, Citrix launcher, example settings, and live catalogue copy is English.

### Privacy

- A header action obscures patient identifiers, treatment context, application paths, and activity context while preserving enough workflow detail for documentation screenshots.
- Patient-specific action labels become generic while privacy mode is enabled; hidden paths are also removed from tooltips.

### Citrix workflow

- Public documentation explains why one published Runner entry is preferable to separate ARIA toolbar links or interactive remote desktop access for every standalone utility.
- Exact automated context tests use the user-scoped shared-request channel and do not depend on client argument forwarding.

### Validation

- Synthetic UI smoke data exercises a selected plan, four application modes, privacy redaction, and a replayable terminal activity without clinical data.
- Regression coverage includes replay availability reasons, filter reset state, privacy state, centered layout, English product copy, and Citrix documentation.

## [0.2.9] - 2026-08-04

### Security

- Pending markers use the Windows SID, and each request records the same mandatory owner SID. Startup and direct `--run-request` execution both reject another identity.
- An ownership mismatch does not create a result file, leaving the request available to its rightful owner.
- The workstation helper limits the claim window to 30 seconds by default and removes its unclaimed marker on timeout.

### Operations

- Request and result JSON remain readable history in the protected `requests` child of the configured log tree; pending and claimed markers remain short-lived.
- The Settings GUI continues to expose both paths explicitly.
- The release build skips byte-identical live artifacts, avoiding needless replacement failures while an identical Script Host is still running.

## [0.2.8] - 2026-08-04

### Fixed

- Shared context requests written by Windows PowerShell 5.1 are read correctly when the JSON file starts with a UTF-8 byte-order mark.

### Validation

- A regression writes the request in the same BOM-bearing encoding as the production helper and executes it through the real request reader.
- `docs/CONTEXT_DEBUGGING.md` documents the exact request/result contract, workstation helper, direct VDA modes, automation rules, and safe failure interpretation.

## [0.2.7] - 2026-08-04

### Fixed

- Automated Citrix debugging now uses a per-user pending marker and the normal installed published-app shortcut; it no longer depends on unreliable client-side `qlaunch`, `wfcrun32`, or `%**` parameter forwarding.
- Startup atomically claims at most one pending request for the current Windows user, runs it before opening the catalogue, and exits with the script result.

### Validation

- A regression covers pending-marker claiming, exact request execution, result creation, and marker cleanup.

## [0.2.6] - 2026-08-04

### Added

- `--run-request <request-id>` runs one or more explicit contexts from the configured protected request directory and writes a matching result JSON.
- `tools/Invoke-CitrixContextDebug.ps1` creates an explicit request and waits for completion through the published Runner.
- `ContextRequestDirectory` is editable in `settings.ini` and the Settings GUI.

### Validation

- Synthetic regressions cover exact request execution, result creation, retained request evidence, and rejection of unsafe request IDs.
- Live validation covers the published shell-free launcher and explicit cross-VDA request routing without relying on client command-line forwarding or `latest` history.

## [0.2.5] - 2026-08-04

### Added

- `--run-contexts` applies an explicit ordered context series to one configured read-only context script.
- The series is transferred as JSON through `ESAPI_RUNNER_CONTEXTS`; patient and planning identifiers remain outside command-line arguments and technical logs.
- A shell-free `ESAPI-Runner-Hub.CitrixLauncher.exe` provides a stable Citrix Studio target, resolves only the version selected by `current.txt`, and accepts one packed Runner command.

### Safety

- Context series run sequentially, stop at the first non-zero child exit, reject write-enabled applications, and accept at most 100 entries.
- The direct Citrix launcher invokes only the validated versioned Runner path, waits for its exit, propagates the exit code, and never logs forwarded argument values.

### Validation

- Synthetic regressions cover two ordered context launches and refusal of a write-enabled series.
- End-to-end launcher tests cover separate and Citrix-packed arguments, invalid pointers, missing settings, exit-code propagation, and privacy-safe logs.

## [0.2.4] - 2026-08-04

### Fixed

- Short-lived command-line runs flush their final privacy-safe diagnostic to the configured log with a local fallback and a bounded network wait.

### Validation

- A regression verifies that immediate CLI diagnostics retain the event and exception type without exception details or context identifiers.

## [0.2.3] - 2026-08-04

### Added

- Privacy-safe `--run-context` and `--replay-latest` command-line modes for reproducible context-script diagnosis through the existing Hub and Citrix launcher.
- Script Host diagnostics now record a safe context-resolution reason code centrally and in a local fallback log.

### Fixed

- Selected plans and plan sums are resolved within their selected course, including repeated plan IDs across courses and multi-plan scopes.
- Plan and plan-sum dropdown labels include the owning course.

### Validation

- Synthetic regressions cover duplicate plan and structure-set IDs across courses, course-aware GUI selection, private CLI transfer, protected replay, and redacted host diagnostics.

## [0.2.2] - 2026-08-04

### Added

- Configured Eclipse plug-in cards can launch their `.esapi.dll` or `.cs` target directly with the selected patient, plan, plan sum, or structure set while retaining the Eclipse entry.
- Script Host failures identify a privacy-safe execution phase in addition to the exception type.

### Fixed

- Long compact application paths wrap inside catalogue cards.
- The isolated host creates the WPF `Application` required by UI plug-ins such as ClearPlan.
- Single-file compilation includes the XML and XAML framework references required by the Eclipse 18 API surface.

### Validation

- Regression tests cover dual Eclipse/direct cards, reference-only cards, WPF application hosting, source references, safe failure stages, and path wrapping.

## [0.2.1] - 2026-08-04

### Fixed

- Removed the Script Host runtime dependency on the Hub executable while preserving the serialized launch contract.
- Direct binary and single-file context scripts can now use the stable host beside an immutable versioned Citrix Hub binary.

### Validation

- Added cross-assembly payload compatibility and host runtime-independence tests.

## [0.2.0] - 2026-08-04

### Added

- Direct context scripts for supported compiled `.esapi.dll` and single-file `.cs` tools through an isolated Eclipse 18 host.
- Patient, course, plan, plan-sum, structure-set, and image selection with explicit per-tool context requirements.
- DPAPI-encrypted launch history with lifecycle status and restart from current application settings.
- Catalogue filters and card metadata for standalone, single-file, binary, read-only, and write-enabled tools.
- Compact institutional paths and optional STR Hub README links.

### Safety

- Write-enabled context scripts require a fresh save/discard decision after every start or relaunch.
- The encrypted launch history contains no clear patient, plan, structure-set, or image identifiers.
- Child failures, missing targets, corrupt history, and unavailable network paths remain isolated from the Hub.

### Validation

- Synthetic tests cover direct context resolution, source compilation, write gating, protected relaunches, retention, corruption recovery, and release packaging.

## [0.1.6] - 2026-08-04

### Changed

- Compile-time authorization metadata and the UKL live configuration now use the Eclipse 18 API/Types pair from the managed `_Assets` directory.
- The release build rejects missing references and versions other than Eclipse 18 before compiling.

### Validation

- The Citrix pointer remains immutable and rollback-capable while the Eclipse 18 build is tested independently.

## [0.1.5] - 2026-08-04

### Fixed

- The Citrix launcher accepts both CRLF and Git-style LF line endings in `current.txt`.
- Launcher boundaries and stable error codes are written to the configured shared log directory.
- The entry executable declares the ESAPI API metadata reference required by ESAPI 16.1 standalone authorization; no Varian DLL is copied into the repository or release package.

### Validation

- Added regressions for LF release pointers, shared launcher diagnostics, and the ESAPI entry-assembly authorization contract.

## [0.1.4] - 2026-08-04

### Fixed

- The stable Citrix batch entry invokes the Hub directly so the runner window remains in the published application process tree.
- ESAPI initialization failures retain their root exception for privacy-safe technical logging.

### Privacy

- Shared logs record only the exception type; exception messages, arguments, search text, and patient data remain excluded.

### Validation

- Added a synthetic regression for a nested `Application.CreateApplication()` failure and its root exception type.

## [0.1.3] - 2026-08-04

### Added

- Stable Citrix batch entry point with an editable version pointer and immutable versioned Hub binaries.
- Documented Studio command, release switch, rollback, local technical log, and launcher exit codes.

### Fixed

- Release builds no longer overwrite the legacy live executable path or risk replacing `dist/settings.ini`.

## [0.1.2] - 2026-08-03

### Fixed

- Missing or outdated configured ESAPI assembly paths now fall back to the highest complete local Varian RTM installation under Program Files.
- Incomplete RTM installations are skipped, while an explicit valid API/Types pair remains authoritative.

### Validation

- Added synthetic regression coverage for configured-path precedence, version ordering, incomplete installations, and the complete reflection loader fallback.

## [0.1.1] - 2026-08-01

### Added

- Eclipse plug-in catalogue entries for `.esapi.dll` and `.cs` tools, visibly separated from externally launchable executables.
- Editable launch-kind selection in `settings.ini` and the graphical settings window.
- Privacy-safe lifecycle events for child start, successful exit, and non-zero exit in the configured technical log directory.

### Safety

- Eclipse plug-ins never expose an external start action and cannot declare patient transfer because they require the live Eclipse `ScriptContext`.
- Launch kind and target extension are cross-validated, so plug-ins cannot accidentally appear as startable executables.
- Central technical logs use one privacy-safe file per Hub process and a bounded background queue; unavailable network storage never blocks application launches or exit handling.
- Child processes that exit before event subscription are still reported as completed instead of remaining visually stuck as running.

## [0.1.0] - 2026-08-01

### Added

- Reflection-only ESAPI patient-directory loader with immediate disposal and detached local search.
- Configurable runner and standalone EXE catalogue backed by `settings.ini` and a WPF settings editor.
- `None`, `Optional`, and `Required` patient modes with argument or environment transport.
- Independent child-process launch, exit monitoring, crash isolation, and asynchronous path readiness.
- Privacy-safe logging and crash reports that omit patient and command-line values.
- Synthetic offline UI mode, synthetic VMS assembly, process fixtures, and automated x64 tests.
- Dedicated window/taskbar icon, public documentation, version metadata, and release tooling.

### Known limitations

- Version 0.1 does not search courses or plans.
- Eclipse plug-in DLLs require a separate compatible runner EXE.
- Live Eclipse workstation validation is distinct from the synthetic automated test suite.
