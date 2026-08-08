# ESAPI Dual Script Host Design

**Date:** 2026-08-08

**Status:** Approved direction; written specification for implementation

**Target:** ESAPI Runner Hub, Eclipse 18, Citrix application delivery

## Objective

Split direct ESAPI context execution into two visibly and technically separate executable scopes:

- `ESAPI-Script-Host.exe` remains the default read-only host for patient search helpers, plan checks, reporting, previews, and data-mining scripts that never call `Patient.BeginModifications()`.
- `ESAPI-Write-Script-Host.exe` is a separate write-enabled stand-alone ESAPI executable for the small number of reviewed scripts that call `BeginModifications()`.

The write host is intended to be registered and approved as a stable executable in Eclipse Script Administration. Ordinary catalogue and `settings.ini` changes must not rebuild it. Child write scripts remain independently versioned, reviewed, and approved according to local governance; the host must not be presented as a bypass for script approval.

## Alternatives Considered

### One host with a runtime switch

Rejected. A single executable would collapse read-only and write-enabled execution into one ESAPI approval identity. A configuration mistake could route an ordinary catalogue entry through the approved write scope.

### One dedicated runner per write script

Rejected as the default. It provides a narrow approval boundary but duplicates patient/context selection and produces avoidable executable and Citrix maintenance for every write tool.

### Two hosts with automatic selection

Selected. It keeps read-only execution generic and makes write execution an explicit, separately approved boundary while preserving one Runner UI and one Citrix application.

## Host Identities

The existing read-only host keeps its filename for backward compatibility. It has no `ESAPIScript(IsWriteable=true)` assembly attribute and accepts only `WriteMode=ReadOnly`.

The new write host is compiled from the same reviewed source set but as a distinct executable with:

```csharp
[assembly: ESAPIScript(IsWriteable = true)]
```

It accepts only `WriteMode=ConfirmSave` or `WriteMode=ExecuteAndDiscard`. Both executables validate their capability before opening an ESAPI session or patient. A mismatched payload fails closed and is recorded without clinical identifiers.

Shared source is compiled into both executables rather than moved into a replaceable runtime DLL. This keeps the executable approval identity coupled to the code that resolves context, invokes scripts, and saves or discards changes.

## Write Modes

`WriteMode` has three values:

- `ReadOnly`: selects the read-only host; the child assembly must not declare `IsWriteable=true`; the host never calls `SaveModifications()`.
- `ConfirmSave`: selects the write host; the child assembly must declare `IsWriteable=true`; normal completion produces the existing explicit save/discard question.
- `ExecuteAndDiscard`: selects the write host; the child assembly must declare `IsWriteable=true`; the host warns that write authorization is still required, executes the script, and always closes the patient without saving.

`ExecuteAndDiscard` is not labelled Preview. It may execute in-memory ESAPI mutations and therefore still requires an approved write scope. A genuine preview remains a read-only script or entry point that does not call `BeginModifications()`.

Exceptions, context failures, compilation failures, crashes, and abnormal exits never save in any mode. Context series remain restricted to `ReadOnly` applications.

## Configuration And UI

The Hub section of `settings.ini` contains two paths:

```ini
ScriptHostExecutable=ESAPI-Script-Host.exe
WriteScriptHostExecutable=ESAPI-Write-Script-Host.exe
```

`ScriptHostExecutable` retains its existing name and meaning for backward compatibility. Missing `WriteScriptHostExecutable` is a validation error only when at least one enabled direct context application uses a write mode.

The Settings window labels the paths **Read-only script host executable** and **Write-enabled script host executable**. The application editor exposes the three write modes with no automatic conversion of existing entries. Current `ConfirmSave` entries remain `ConfirmSave`.

The request composer chooses the executable solely from the validated `WriteMode`; application paths or script metadata cannot override the host choice. The selected host path is not supplied by external request JSON.

## Approval And Governance Boundary

The write host is a stand-alone write-enabled ESAPI script and must be registered, evaluation-tested, validated, and approved in the clinical system before use. Rebuilding or changing the write-host executable creates a new release requiring the local retire/evaluation/approval process.

Changing `settings.ini`, adding a read-only catalogue entry, or updating the Citrix pointer does not change the write-host binary. Updating a child write script does not silently make it approved: its own version and local approval evidence remain required. The Runner checks assembly write metadata and configuration consistency but does not claim to replace Eclipse Script Administration or institutional QA.

The live catalogue remains stored in the protected shared scripting area. Only explicitly configured `EsapiContextScript` entries can reach either host.

## Build, Packaging, And Citrix

The solution builds both host projects for x64 .NET Framework 4.8 against Eclipse 18 API metadata. Release packaging includes:

- versioned `ESAPI-Runner-Hub.v0.3.3.exe`,
- `ESAPI-Script-Host.exe`,
- `ESAPI-Write-Script-Host.exe`,
- matching configuration files and symbols where currently retained,
- no redistributed Varian assemblies.

The existing Citrix command and application definition remain unchanged. The shared `settings.ini` selects both host paths, and the version pointer continues to select only the main Runner executable.

## Tests

Automated tests must first fail against the current one-host implementation, then cover:

- INI parsing, serialization, path resolution, settings editing, and validation for both host paths;
- automatic read-host selection for `ReadOnly` and write-host selection for both write modes;
- read host rejection of write payloads before ESAPI session creation;
- write host rejection of read-only payloads before ESAPI session creation;
- `ExecuteAndDiscard` never calling `SaveModifications()` after normal completion;
- `ConfirmSave` retaining the one-time explicit save decision;
- child assembly write metadata matching the configured mode;
- read-host assembly lacking and write-host assembly containing `ESAPIScript(IsWriteable=true)`;
- command-line context series refusing both write modes;
- release artifacts containing both hosts while remaining vendor-free;
- existing privacy, crash isolation, context resolution, activity history, and Citrix launcher tests remaining green.

Live validation on the Eclipse 18 Citrix VDA must verify a read-only context script, a write script with discard, and a write script with confirm-save. Until the write host has been approved, its expected live result is the Eclipse authorization failure at `BeginModifications()`; this does not invalidate read-host operation.

## Acceptance Criteria

- The Hub and Citrix launcher start exactly as before.
- Read-only direct scripts always use the read-only executable.
- `ConfirmSave` and `ExecuteAndDiscard` always use the write-enabled executable.
- Each host rejects the other host's modes before opening a patient.
- The write host contains the write-enabled ESAPI assembly marker; the read host does not.
- Discard mode never persists changes; confirm-save persists only after explicit confirmation.
- The productive package and live settings contain both host paths.
- The write-host approval requirement and child-script governance are documented in English in the README and clinical validation checklist.
