# ESAPI Runner Hub Design

**Date:** 2026-08-01  
**Status:** Approved architecture and main-screen direction  
**Project:** `ESAPI-Runner-Hub`  
**Target platform:** Windows x64, .NET Framework 4.8, WPF

## Purpose

ESAPI Runner Hub is a portable Windows launcher for ESAPI-related applications on Eclipse/Citrix workstations. It provides a single overview of configured applications, an immediate patient search, optional patient-ID transfer to child applications, and process isolation so that a failing script does not terminate the launcher.

The application is a separate public project. It does not modify ClearPlan, PlanCheck, eDoc-Uploader, or the EsapiEssentials runner. Existing runner executables and standalone executables are registered in `settings.ini` or through the same settings editor in the GUI.

## Goals

- Show all configured ESAPI runner and standalone applications in one window.
- Load an ESAPI patient directory when the Hub starts and then search it locally with low latency.
- Keep the selected patient in memory so multiple applications can be started for the same patient.
- Let the user clear or change the patient between starts.
- Allow applications to start without a patient when their configuration permits it.
- Pass a selected patient ID by command-line argument or environment variable when the target application supports this.
- Run every target application in a separate operating-system process.
- Keep the Hub usable after a target exits with an error or crashes.
- Make every path and application entry editable in `settings.ini` and in the GUI.
- Ship one vendor-free launcher EXE that can pass through Citrix and starts in offline/catalogue mode when ESAPI is unavailable.
- Publish source and release artifacts in a public GitHub repository without Varian/VMS assemblies.
- Maintain a local Git repository, an internal shared checkout, and a separate InHouse Tools entry.

## Non-goals for version 1

- Searching or opening courses and plans in the Hub.
- Passing live ESAPI objects or `ScriptContext` between processes.
- Automatically converting an arbitrary Eclipse plug-in DLL into a standalone executable.
- Replacing the patient/plan window of an existing runner executable.
- Persisting recent patients, patient names, or patient IDs.
- Managing or monitoring applications through Cockpit.
- Clinical validation of the launched scripts; each target retains its own approval and validation status.

An Eclipse plug-in that only exists as a DLL must have its own compatible runner executable before it can be registered in version 1. Existing applications such as ClearPlan and PlanCheck are registered through their runner EXEs.

## Selected architecture

### Hub process

`ESAPI-Runner-Hub.exe` owns the WPF user interface, configuration, patient index, path validation, launch commands, and child-process status. It never executes a configured clinical application in-process.

The executable has no compile-time dependency on `VMS.TPS.*` or EsapiEssentials. The ESAPI patient loader uses reflection against locally configured VMS assembly paths. This keeps the public source and binary vendor-free and allows the same EXE to open on a non-Eclipse computer in offline mode.

### Patient index

The patient search follows the proven eDoc-Uploader pattern while tightening the ESAPI lifetime boundary:

1. On the WPF STA thread, register an assembly resolver for the configured VMS API and Types assemblies.
2. Load `VMS.TPS.Common.Model.API.dll` and call `Application.CreateApplication()` through a small reflection adapter.
3. Enumerate `Application.PatientSummaries` on the same STA thread.
4. Copy only `Id`, `FirstName`, and `LastName` into immutable plain records with pre-normalized search fields.
5. Dispose the ESAPI `Application` immediately after the copy. No ESAPI object survives the load operation.
6. Filter the detached records locally after a short input debounce and return at most the configured number of matches.

The Hub therefore has immediate patient-search capability after the one-time directory load but does not retain an open patient, plan, or ESAPI application session while child programs run. A manual refresh repeats the load-and-dispose sequence.

No patient record is written to disk. Logs contain only technical state such as `patient directory loaded`, duration, count, or error category; they do not contain patient IDs or names.

### Child processes

Every application starts with `ProcessStartInfo.UseShellExecute = false` in its own process. The Hub monitors process ID, start time, exit time, and exit code. It does not capture or persist standard output by default because child output may contain clinical data.

The user can start another configured application while a previous child process is still running. A child crash, non-zero exit, missing dependency, or unhandled exception changes only that launch row. It does not close the Hub or clear the patient selection.

The Hub does not claim that concurrent ESAPI access is safe for every target. Application cards and documentation state that the validation and modification behaviour remains the responsibility of the configured application.

## User interface

The approved main window has four stable areas:

1. **Header:** product name, ESAPI state (`loading`, `ready`, `offline`, `error`), settings button.
2. **Patient context:** ID/name search, suggestion list, selected-patient card, clear action, and privacy note.
3. **Application catalogue:** category list, application filter, readiness state, patient-mode badge, and launch actions.
4. **Running processes:** compact rows for running and completed children without patient data or expanded arguments.

Patient selection is optional at Hub level. Application configuration determines the card behaviour:

- `None`: one `Start` action; Hub patient selection is ignored.
- `Optional`: `Start with <ID>` and `Start without patient` actions when a patient is selected; otherwise `Start without patient` remains available.
- `Required`: start remains disabled until an exact patient selection exists.

The selected patient remains active for subsequent starts until the user clears it, selects a different match, refreshes the patient index, or closes the Hub. It is never restored on the next application start.

All screenshots, fixtures, and public documentation use synthetic identities only.

## Configuration

The portable source of truth is `settings.ini` next to the EXE. `--settings <path>` selects an alternative file. The settings view always shows the resolved file path. If the file or directory is read-only, the GUI reports this and offers `Save as`; it does not silently write to an unrelated profile directory.

The settings editor and INI parser operate on the same model. Saving uses a temporary file and atomic replacement where supported. A failed save leaves the prior file unchanged.

Example schema:

```ini
[Hub]
EsapiApiAssembly=C:\Program Files\Varian\Eclipse Scripting API\VMS.TPS.Common.Model.API.dll
EsapiTypesAssembly=C:\Program Files\Varian\Eclipse Scripting API\VMS.TPS.Common.Model.Types.dll
SearchMaxResults=10
SearchDebounceMs=150
PathProbeTimeoutMs=1500

[Application.clearplan]
Name=ClearPlan
Category=Plan review
Description=Integrated planning review
Executable=apps\ClearPlan\ClearPlan.Runner.exe
WorkingDirectory=apps\ClearPlan
PatientMode=Optional
PatientTransport=None
Arguments=
Enabled=true
SortOrder=10

[Application.edoc]
Name=eDoc-Uploader
Category=Documents
Description=ARIA document workbench
Executable=apps\eDoc\eDoc-Uploader.exe
WorkingDirectory=apps\eDoc
PatientMode=Required
PatientTransport=Argument
PatientArgumentTemplate=--patient-id "{PatientId}"
Arguments=--workbench
Enabled=true
SortOrder=20
```

Supported application fields are deliberately limited to what version 1 needs:

- identity: stable section ID, `Name`, `Category`, `Description`, `Enabled`, `SortOrder`;
- execution: `Executable`, `WorkingDirectory`, `Arguments`;
- patient handling: `PatientMode=None|Optional|Required`, `PatientTransport=None|Argument|Environment`, `PatientArgumentTemplate`, `PatientEnvironmentKey`.

Paths may be absolute, relative to the settings file, or contain normal Windows environment variables. Version 1 launches EXE files only; it does not invoke arbitrary shell text, BAT files, PowerShell, or URLs.

For `Argument` transport, `{PatientId}` is replaced in the patient argument template. For `Environment` transport, the ID is supplied only to the child under the configured environment-variable key. The Hub never displays or logs the fully expanded command line after patient substitution.

## Error handling and network resilience

Path checks never run synchronously on the UI thread. Each configured path is probed independently with cancellation and the configured timeout. The Hub does not recursively scan network shares or local drives.

- Missing local path: application card shows `path missing`; start is disabled.
- Unreachable or slow UNC path: card shows `network path unavailable`; the remaining catalogue stays usable.
- ESAPI assemblies missing or incompatible: patient search changes to offline mode; applications with `PatientMode=None` and optional applications without patient can still start.
- ESAPI directory load fails: exception details are reduced to a technical, non-identifying message; no application-wide crash.
- Child start fails: the card receives an error state; Hub and patient index remain intact.
- Child exits non-zero or crashes: process row records only application ID, timing, and exit code.
- Invalid INI entry: the editor identifies the section and invalid field; valid applications still load.

Top-level unhandled-exception handling writes a privacy-safe crash report and shows a recoverable error dialog where possible. It does not include patient selection or expanded child arguments.

## Testing and verification

Implementation follows test-driven development. Required automated coverage includes:

- INI parsing, validation, relative path resolution, and atomic save behaviour;
- patient-mode and patient-transport rules;
- argument quoting and `{PatientId}` substitution;
- environment-only patient transfer;
- tokenized, case-insensitive patient search and exact selection;
- proof that patient data is absent from settings and Hub logs;
- asynchronous path-probe timeout and independent failure states;
- child success, non-zero exit, start failure, and crash isolation using fixture EXEs;
- reflection patient loader against a synthetic VMS-shaped test assembly;
- offline startup without VMS assemblies;
- vendor-free assembly and release-package scan;
- deterministic x64 release packaging and SHA-256 manifest;
- synthetic/offline UI smoke mode for screenshots and Citrix layout checks.

Live validation on an Eclipse workstation is a separate gate. It must confirm actual ESAPI patient-directory loading, repeated searches, patient switching, argument transfer to a test application, multiple sequential and overlapping child starts, missing network paths, and child crash isolation. No real patient data may appear in screenshots, Git, release assets, or validation reports.

## Repository, release, and deployment

### Local Git

The primary local repository is:

```text
C:\Users\grohmanmax\Seafile\Meine Bibliothek\Paper\ClearPlan\ESAPI-Runner-Hub
```

The default branch is `main`. Commits remain focused and exclude local settings, proprietary assemblies, patient data, build output, and unrelated ClearPlan manuscript files.

### Public GitHub

The public repository is planned as:

```text
https://github.com/Kiragroh/ESAPI-Runner-Hub
```

It contains source, tests, sample configuration, MIT license, README, changelog, version metadata, build scripts, and vendor-free release artifacts. The initial public release is `v0.1.0` / build `1`. The release ZIP contains the launcher EXE, `settings.example.ini`, documentation, license, and SHA-256 manifest; it contains no `VMS.TPS.*`, EsapiEssentials, patient data, local clinical paths, or credentials.

### Internal shared checkout

The internal launch/deployment root is:

```text
\\medizin.uni-leipzig.de\data\Archiv\STR\STR-Physik\11. Scripting\ESAPI-MG\ESAPI-Runner-Hub
```

The directly runnable artifact is:

```text
\\medizin.uni-leipzig.de\data\Archiv\STR\STR-Physik\11. Scripting\ESAPI-MG\ESAPI-Runner-Hub\dist\ESAPI-Runner-Hub.exe
```

The shared checkout retains `.git` and root `versionInfo.json` so the Hub version resolver can walk up from the executable to the release metadata. An internal bare backup remote is created under `GitUKL\ESAPI-Runner-Hub.git` without replacing the public GitHub origin.

`versionInfo.json` is the InHouse-visible version source. `CHANGELOG.md` carries the same release history for repository users.

## InHouse Tools entry

A new entry named **ESAPI Runner Hub** is created after the shared verified executable exists. It is separate from the existing interface entry `scripts.id = 59`, **Eclipse Scripting API (ESAPI)**.

Planned values:

- `name`: `ESAPI Runner Hub`
- `short_desc`: `Portable Übersicht mit schneller ESAPI-Patientensuche zum isolierten Starten konfigurierter Runner- und Standalone-Anwendungen.`
- `label`: `Eclipse`
- `author`: `MG`
- `path`: `\\medizin.uni-leipzig.de\data\Archiv\STR\STR-Physik\11. Scripting\ESAPI-MG\ESAPI-Runner-Hub\dist\ESAPI-Runner-Hub.exe`
- `url`: public GitHub repository
- `cockpit_managed`: `0`
- `status`: `active`
- `responsible`: `grohmanmax,schaeferse`

The README explains purpose, patient handling, configuration, shared path, process isolation, privacy, ESAPI dependency, and the absence of Hub plan search in version 1. It contains no patient identifiers, credentials, local-only paths, or raw logs.

Before insertion, the live database schema and duplicate candidates are queried and `web/backend/inhouse.db` is backed up. After insertion, the row is read back and the Hub detail/version payload or backend resolver is verified. Manual DB changelog data is not duplicated from Git release news.

## Acceptance criteria

The project is complete when all of the following are true:

- A single x64 EXE starts from a portable folder on a non-Eclipse PC in offline mode.
- On an Eclipse workstation, the Hub builds a searchable patient index and releases the ESAPI application before child programs run.
- A selected synthetic/test patient can be passed to a fixture application by argument and environment variable.
- Optional, required, and no-patient application modes behave as specified.
- Multiple child applications can be launched without blocking the Hub.
- A deliberately crashing child leaves the Hub, patient search, and other running children usable.
- Missing optional network paths produce visible non-fatal states.
- GUI edits and `settings.ini` produce the same configuration.
- Automated tests, vendor-free scan, release build, and synthetic UI smoke tests pass.
- Local Git, internal backup, public GitHub `v0.1.0`, and release asset hashes are verified.
- The shared UNC folder contains the runnable EXE and matching version metadata.
- The separate InHouse entry resolves the shared path and current Git build.
