# ESAPI Runner Hub: catalogue metadata and persistent launch history

**Date:** 2026-08-04

**Status:** Approved as Design A

**Scope:** Extend the existing Eclipse 18 Runner Hub without replacing its isolated child-process or context-host architecture.

## Objective

Make every configured tool understandable and directly usable from one catalogue. The Hub must distinguish standalone executables, single-file C# scripts, and compiled ESAPI binaries; expose read/write intent and a shortened source location; link to the corresponding STR Hub README; and retain a privacy-preserving, restartable activity history across Hub sessions.

ClearPlan remains available in two explicit modes: its existing standalone runner and a direct `ClearPlan.esapi.dll` launch through the Hub's isolated ESAPI script host. Binary and source context scripts use the selected patient, course, plan or plan sum, structure set, and image.

## Configuration contract

The existing INI configuration remains the source of truth. Add optional fields without breaking old entries:

- Hub: `StrHubBaseUrl`, `HistoryFile`, `HistoryRetentionDays`, and `HistoryMaxEntries`.
- Application: `ArtifactKind=Auto|Standalone|SingleFile|Binary`, `AccessMode=Auto|ReadOnly|WriteEnabled|Unknown`, and `HubScriptId`.

`ArtifactKind=Auto` classifies `.exe` as standalone, `.cs` as single-file, and `.dll`/`.esapi.dll` as binary. `AccessMode=Auto` uses the direct host `WriteMode` where available and otherwise reports `Unknown`; live entries receive explicit values where their behavior is known. Configuration values override inference.

The live configuration contains separate ClearPlan entries:

1. `ClearPlan Runner`, launching `ClearPlan.Runner.exe` as a standalone process.
2. `ClearPlan directly`, launching `ClearPlan.esapi.dll` with `ScriptEngine=EsapiEssentials`, `ContextRequirement=PlanningItem`, `ScopeMode=Multiple`, and `WriteMode=ReadOnly`.

Known STR Hub mappings use their existing script IDs. Entries without a dedicated page omit the README button or use an explicitly configured parent page; the runner never invents an ID.

## Catalogue presentation and filtering

Each card shows:

- category and application name;
- artifact badge: `Standalone`, `Single-file (.cs)`, or `Binary (.dll)`;
- access badge: `Read-only`, `Write-enabled`, or `Access unknown`;
- compact source location;
- readiness state and applicable launch actions;
- an `STR Hub README` button only when both the Hub base URL and an application script ID are configured.

Paths below the institutional scripting root are displayed as `Physik-Skripte\...`. This is presentation only; launching always uses the resolved full path. Other paths show a compact filename or an ellipsized tail and never change the configured value.

The catalogue adds mutually exclusive filters `All`, `Standalone`, `Single-file`, and `Binary`. Text and category filters continue to combine with the artifact filter.

## Persistent activity model

Replace the process-only footer with a recent activity list containing both active processes and persisted launches. Each history record stores:

- stable history ID and application configuration ID;
- application name and artifact/access labels captured at launch;
- UTC start and optional finish time;
- lifecycle state (`Starting`, `Running`, `Exited`, `FailedToStart`, `Unavailable`);
- exit code when available;
- launch mode (`WithoutPatient`, `WithPatient`, or `Context`);
- an optional encrypted context envelope.

The history store is local to the Windows user, defaults to `%LOCALAPPDATA%\ESAPI Runner Hub\launch-history.json`, keeps at most 100 entries for 30 days, writes atomically, and treats I/O failure as non-fatal. The UI remains usable if history cannot be read or saved.

## Context privacy and relaunch

Names, search text, arguments, environment values, and child output are never persisted. For context launches, the minimum relaunch identifiers—patient, course, plan, plan sum, structure set, image, and scope IDs—are serialized and protected with Windows DPAPI in `CurrentUser` scope before being written. The JSON contains only the encrypted blob, not clear identifiers.

`Run again` behavior:

- standalone launches are recomposed from the current application definition, not stale command lines;
- context launches decrypt the saved selection, validate the current application definition and required context, then start a new isolated script-host process;
- identifiers are resolved afresh by ESAPI in the child host, so stale or removed plans fail safely;
- removed, disabled, or missing applications show `Unavailable` and cannot relaunch;
- write-enabled context scripts still require the child host's save/discard confirmation on every run.

No live ESAPI object is retained by the Hub or reused after a patient session closes.

## Components

- `ApplicationDefinition` and INI store: new optional metadata and Hub/history settings.
- `ApplicationMetadata`: deterministic artifact/access classification, compact path, and README URI creation.
- `LaunchHistoryEntry`, `LaunchHistoryStore`, and `ProtectedContextEnvelope`: detached persistence and DPAPI protection.
- `ApplicationCardViewModel`: metadata badges, compact path, README command state.
- `MainViewModel`: artifact filter, history collection, lifecycle updates, and relaunch command.
- Existing `ArgumentComposer` and `ContextScriptRequestComposer`: remain the only request-composition paths and are reused for relaunch.
- Existing `ChildProcessLauncher` and `ESAPI-Script-Host.exe`: preserve process isolation and ESAPI STA ownership.

## Error handling and safety

- Missing network targets mark only the affected card/history action unavailable.
- History and DPAPI failures are reported technically without identifiers and never close the Hub.
- Context mismatch, ambiguity, or deleted Eclipse objects fails in the isolated host and never saves modifications.
- The Hub does not infer write safety from a filename. Explicit metadata or verified host write mode controls the badge.
- Unit and synthetic integration tests are not clinical validation; Eclipse workstation acceptance remains a separate gate.

## Tests and acceptance

Test-driven coverage must establish:

- INI parsing, round-trip, defaults, and validation for all new fields;
- extension-based artifact classification and configuration override;
- read/write badge logic and compact institutional path rendering;
- README URI creation and safe suppression for incomplete/invalid settings;
- combined category, text, and artifact filtering;
- atomic history retention, corrupt-file recovery, and process lifecycle updates;
- DPAPI context round-trip with no clear identifiers in the persisted file;
- relaunch of standalone, patient-aware, and context scripts through the normal composers;
- disabled/missing application and stale-context failure behavior;
- separate live ClearPlan runner and direct binary entries;
- regression coverage for patient search, existing executable launches, Eclipse reference cards, child crash isolation, Citrix launcher behavior, and vendor-free packaging.

Release acceptance requires a clean x64 .NET Framework 4.8 build, all automated tests, vendor-free package validation, updated `versionInfo.json` and `CHANGELOG.md`, focused Git commit and push, preserved live `dist/settings.ini`, immutable Citrix binary publication, and verification of the STR Hub-visible version.
