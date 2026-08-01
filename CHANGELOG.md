# Changelog

All notable changes to ESAPI Runner Hub are documented here.

## [0.1.1] - 2026-08-01

### Added

- Eclipse plug-in catalogue entries for `.esapi.dll` and `.cs` tools, visibly separated from externally launchable executables.
- Editable launch-kind selection in `settings.ini` and the graphical settings window.

### Safety

- Eclipse plug-ins never expose an external start action and cannot declare patient transfer because they require the live Eclipse `ScriptContext`.

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
