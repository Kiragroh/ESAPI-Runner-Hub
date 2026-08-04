# ESAPI Context Script Host Design

**Date:** 2026-08-04

**Status:** Approved design

**Target:** ESAPI Runner Hub, Eclipse 18

## Objective

Extend ESAPI Runner Hub so that one central user interface can start all relevant in-house ESAPI tool forms:

- regular standalone executables,
- the existing per-application runners, including the ClearPlan runner,
- compiled Eclipse context scripts (`*.esapi.dll`),
- suitable Eclipse C# context scripts (`*.cs`), and
- EsapiEssentials-based context scripts such as ClearPlan directly, without opening a second patient selector.

Patient selection remains optional per tool. Tools that need treatment context can additionally require a course, plan, plan sum, or structure set. A script failure must not terminate the Hub, and no modifications may be saved implicitly after an error.

## Chosen Architecture

The Hub remains the central catalog and selection interface. Context scripts are executed in a new, separate process named `ESAPI-Script-Host.exe`.

Each script launch receives its own host process and its own ESAPI application instance on one STA thread. Consequently, a crash or hang in a context script cannot terminate the Hub or corrupt the Hub's ESAPI session. Existing executable entries continue to use the current launch path, and the dedicated ClearPlan runner remains available as an additional launch option.

Direct in-process script execution inside the Hub is deliberately excluded because a plugin exception, blocked UI, or incompatible assembly could otherwise terminate the shared launcher. Creating a dedicated runner executable for every plugin is also excluded because it duplicates configuration and context-selection code.

## Tool Configuration

The existing INI-based catalog is extended with explicit context-script settings. Paths remain editable in the Hub settings UI.

```ini
[ClearPlan direct]
Path=...\ClearPlan.esapi.dll
LaunchKind=EsapiContextScript
ScriptEngine=EsapiEssentials
ContextRequirement=PlanOrStructureSet
ScopeMode=Multiple
WriteMode=ReadOnly
EntryType=VMS.TPS.Script
ExtraReferences=
```

Supported values are:

- `LaunchKind=Executable|EclipsePlugin|EsapiContextScript`
- `ScriptEngine=Auto|EsapiEssentials|Eclipse`
- `ContextRequirement=None|Patient|StructureSet|Plan|PlanningItem|PlanOrStructureSet`
- `ScopeMode=None|Single|Multiple`
- `WriteMode=ReadOnly|ConfirmSave`

`EclipsePlugin` keeps the current behavior of exposing a script for use from Eclipse or through an existing dedicated runner. `EsapiContextScript` executes the target directly through the new host. The same target may therefore have both a classic Eclipse entry and a direct-host entry.

`EntryType` is optional when exactly one compatible script type can be found. `ExtraReferences` is an optional semicolon-separated list used for additional local compile or load dependencies. Ambiguous engines or entry types fail closed and are reported in the UI instead of guessing.

Configuration is authoritative for context requirements and saving behavior. Metadata detection may supply safe defaults, but it cannot silently weaken a configured requirement or enable saving.

## Context Selection

After selecting a patient, the Hub offers a compact context selector containing:

- course,
- active planning item (`PlanSetup` or `PlanSum`),
- structure set, including a structure set without a plan,
- image derived from the selected structure set, and
- optional plan and plan-sum scope selections for scripts that operate on several planning items.

Selecting a plan automatically selects its course, structure set, and image. The structure set remains independently selectable so that contouring scripts can run without a treatment plan. Scope selection is separate from the active planning item.

The start action is enabled only if the configured context requirement is satisfied. When it is disabled, the UI states the missing item, for example `Structure set required` or `Plan or plan sum required`.

Context semantics are explicit:

- `None`: no patient or treatment context is required.
- `Patient`: patient is populated; plan and structure-set properties remain null unless selected.
- `StructureSet`: patient, course where available, structure set, and image are populated; plan properties may remain null.
- `Plan`: a `PlanSetup` is required and supplies course, structure set, image, and active plan.
- `PlanningItem`: either a `PlanSetup` or `PlanSum` is required.
- `PlanOrStructureSet`: either an active planning item or an independently selected structure set is accepted.

For a plan sum, the active `PlanSum` and selected component plans are placed in scope. A structure set is only populated if it is explicitly selected or can be resolved unambiguously. Ambiguous identifiers or conflicting combinations are rejected before the script runs.

The Hub reads the selected patient's context through a short-lived ESAPI operation on a dedicated STA thread. It copies only plain context descriptors such as identifiers, types, and display labels, then closes the patient and disposes the ESAPI application. Live ESAPI objects are never stored in the Hub, moved across threads, or passed between processes.

## Context Transport and Privacy

The Hub starts the host with a one-time launch token. Patient and planning-context identifiers are transferred through an inherited, process-local environment payload rather than command-line arguments. The payload is consumed immediately and never persisted.

Patient identifiers, patient names, search strings, course identifiers, plan identifiers, structure-set identifiers, and the context payload must not appear in application logs, process arguments, exception telemetry, or generated configuration diagnostics. Logs contain only privacy-safe event categories, tool identifiers, exit codes, and sanitized error types.

The host resolves every requested object again inside its own ESAPI application. If the patient, course, plan, plan sum, structure set, or scope item cannot be resolved exactly, the host performs no script invocation and no save operation.

## Runtime and Assembly Resolution

`ESAPI-Script-Host.exe` targets x64 .NET Framework 4.8 and Eclipse 18. It creates one ESAPI application, opens at most one patient, resolves the requested context, invokes the script on the same STA thread, and then closes and disposes all ESAPI resources.

Assembly resolution searches, in order:

1. the script's directory,
2. configured `ExtraReferences`,
3. the configured Eclipse 18 runtime or the repository's `_Assets` reference location.

Varian assemblies are runtime or build references only. They must not be copied into a public release artifact.

C# source scripts are compiled locally into a cache keyed by the source content, compiler options, engine, and referenced assembly versions. Compilation never writes beside a script on a network share. Only explicitly configured files are compiled; the feature is not a general command or arbitrary-source execution interface.

## Script Engines

### EsapiEssentials

An EsapiEssentials script derives from `EsapiEssentials.Plugin.ScriptBase` or `ScriptBaseWithWindow` and implements `Run(PluginScriptContext context)`. The host creates a fully populated `PluginScriptContext` from the selected patient, course, plan, plan sum, structure set, image, and scope selections, then calls the script directly. It does not call the public EsapiEssentials `ScriptRunner.Run` entry point because that runner opens its own selection window and would duplicate the Hub context workflow.

### Classic Eclipse Context Scripts

A classic plugin exposes `VMS.TPS.Script.Execute(VMS.TPS.Common.Model.API.ScriptContext)` or the supported overload with a WPF `Window`.

Eclipse does not provide public setters for all standalone `ScriptContext` properties. The host therefore uses a narrowly scoped, version-gated adapter for Eclipse 18.0.1.261 / API assembly version 1.0.600.194. It constructs `ScriptContext` with the public constructor and assigns only the known Eclipse 18 internal fields required for patient, course, active plan, plan collections, plan sum, structure set, and image.

Before every invocation, the adapter verifies the expected assembly version, constructor, field names, and field types. Any mismatch fails closed with an actionable compatibility message. No script is invoked against a partially populated or unverified classic context.

This reflection adapter is an explicit compatibility boundary and receives dedicated tests. Adding another Eclipse version requires a separate tested adapter rather than relaxing the checks.

## Write-Enabled Scripts and Saving

Write-enabled scripts are supported only when their catalog entry declares `WriteMode=ConfirmSave`. The host also inspects `ESAPIScript(IsWriteable=true)` where available and rejects inconsistent or unknown write behavior rather than enabling saving implicitly.

Before a write-enabled script starts, the UI warns that the script may modify the open patient. The script continues to call `Patient.BeginModifications()` itself where required by its existing implementation.

After the script and its UI return normally, the host asks whether the changes should be saved:

- **Save:** call `Application.SaveModifications()` exactly once, then close the patient.
- **Discard:** close the patient without saving.
- **Cancel:** treat as discard and close without saving.
- **Exception, crash, context error, or abnormal exit:** never save.

Read-only entries never call `SaveModifications()`. The host owns the single save decision; context scripts do not receive the ESAPI `Application` object.

## Process and Error Behavior

Each launch is isolated in a child process. A nonzero exit code, missing dependency, compile failure, version mismatch, context-resolution error, or plugin exception is shown as a concise tool status in the Hub while the Hub remains usable for another script or another patient.

Script windows run on the host's ESAPI STA thread. The Hub may start another isolated child after a failure. Concurrent launches are technically independent, but the UI warns when another patient-context tool is already running because clinical workstation and database policies remain authoritative.

The host never saves after a failed invocation. Context validation and engine compatibility checks occur before user code is called.

## User Interface

The existing tool cards remain the primary catalog. A selected tool shows its launch kind, required context, write mode, and availability. For context scripts, the patient panel expands into the course/planning-item/structure-set selector. Tools without patient requirements remain one-click launches.

The main action states the resulting operation, for example:

- `Start without patient`
- `Start for selected patient`
- `Start with structure set`
- `Start with plan`

Write-enabled entries use a visually distinct warning state. Missing context, an offline ESAPI runtime, unsupported Eclipse versions, or unresolved dependencies are shown before launch rather than as a late message box from the plugin.

## Test Strategy

Implementation follows test-driven development: each behavior starts with a failing automated test, followed by the minimum production change and a passing regression run.

Automated tests cover:

- parsing and validation of every new configuration enum and combination,
- context-requirement validation and user-facing missing-context messages,
- exact plan, plan-sum, structure-set, image, and scope resolution,
- rejection of ambiguous and conflicting context,
- context mapping for EsapiEssentials and the Eclipse 18 adapter,
- strict version, constructor, field-name, and field-type checks,
- write-metadata/configuration consistency,
- the save state machine: only normal completion plus explicit Save saves exactly once,
- discard, cancel, exception, crash, and context error never saving,
- source-compile caching and invalidation after source/reference changes,
- dependency-resolution failures,
- child-process crash isolation and a successful subsequent launch,
- privacy-safe logging and absence of context identifiers from arguments and logs,
- vendor-free public release validation.

The fake VMS API is extended with courses, plans, plan sums, structure sets, images, and modification/save counters. Synthetic fixture scripts cover read-only execution, write-enabled execution, missing plan, missing structure set, and deliberate exceptions.

Live workstation validation uses only a designated synthetic nonclinical patient and includes:

1. selecting a standalone structure set and running a contouring-context fixture,
2. selecting a plan and verifying the automatically derived structure set and image,
3. starting ClearPlan directly from its `*.esapi.dll` without a second selector,
4. starting PlanFieldNamer directly, first discarding and then explicitly saving changes,
5. deliberately failing a fixture and immediately launching a second tool from the same Hub.

These checks demonstrate technical execution and isolation only. They do not constitute clinical validation of the scripts or treatment-planning results.

## Release and Documentation

The implementation is released as a new Hub version with updated `versionInfo.json`, changelog, README, Citrix launcher artifacts, and a rebuilt `dist/ESAPI-Runner-Hub.exe` plus `dist/ESAPI-Script-Host.exe`. The release validation must confirm that both executables start on the Eclipse 18 workstation and that no Varian DLL is included in the distributable or public repository.

The README documents the supported script shapes, configuration keys, context behavior, write confirmation, Eclipse 18 compatibility boundary, privacy behavior, and the distinction between technical execution and clinical validation.

## Acceptance Criteria

The design is complete when all of the following are true:

- the Hub can launch standalone tools with or without patient arguments as configured,
- existing dedicated runners such as ClearPlan remain available,
- ClearPlan can also be run directly from its EsapiEssentials assembly with Hub-selected context,
- classic Eclipse `*.esapi.dll` and configured `*.cs` scripts can receive a validated Eclipse 18 context,
- a plan automatically supplies its structure set and image,
- a standalone structure set can be selected without a plan,
- multi-plan and plan-sum scope can be supplied where configured,
- missing required context prevents launch with a useful explanation,
- write-enabled scripts require explicit save confirmation and never save after failure,
- one script crash does not terminate the Hub or prevent the next launch,
- no patient or planning-context identifiers are persisted or logged,
- automated and synthetic live checks pass, and
- the Citrix `dist` artifacts and documentation match the released version.
