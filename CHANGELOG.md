# Changelog

All notable changes to ESAPI Runner Hub are documented here.

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
