# Changelog

All notable changes to ESAPI Runner Hub are documented here.

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
