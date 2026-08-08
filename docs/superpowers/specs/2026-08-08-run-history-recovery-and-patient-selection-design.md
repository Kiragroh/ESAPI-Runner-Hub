# Run History Recovery And Patient Selection Design

**Date:** 2026-08-08

**Status:** Approved direction; written specification for implementation

**Target:** ESAPI Runner Hub v0.3.3 successor, Eclipse 18, Citrix application delivery

## Objective

Keep the Hub alive when a launched child process exits, make completed or interrupted history entries replayable, and let a user restore only the patient selection from a history row without launching an application.

## Root Cause

`System.Diagnostics.Process.Exited` is raised outside the WPF UI thread. The current exit callback changes row state and then raises `RunAgainCommand.CanExecuteChanged` directly from that process thread. WPF throws an unhandled `InvalidOperationException`, the Hub exits with code 1, and the callback never reaches history persistence or the terminal child-exit log entry. The stored history consequently retains `Starting` or `Running`, so **Run again** remains disabled after the next Hub start.

## Alternatives Considered

### Marshal only `CanExecuteChanged`

Rejected. Other callback work also touches UI-bound state and could introduce a second cross-thread failure later.

### Ignore cross-thread exceptions

Rejected. This would conceal an invalid WPF access and still leave persistence timing unreliable.

### Marshal the complete terminal transition

Selected. The whole transition from running to terminal state executes on the WPF dispatcher captured by the Hub. State, property notifications, command availability, history persistence, and logging therefore stay ordered.

For patient restoration, a dedicated **Select patient** action is selected instead of making the context cell or entire row clickable. It is explicit and avoids accidental selection changes.

## Exit Handling

The process-exit callback posts one idempotent terminal action to the UI dispatcher. The action:

1. records `Exited`, finish time, and exit code;
2. refreshes the activity row;
3. recomputes replay availability;
4. raises command availability on the UI thread;
5. persists history;
6. writes the terminal technical-log event.

The existing interlocked guard remains responsible for exactly-once handling when an early-exit check and the asynchronous event race.

## Interrupted History Recovery

On startup, persisted `Starting` and `Running` entries cannot represent child processes still owned by the new Hub instance. They are converted to a new terminal `Interrupted` state before the rows are shown and the repaired history is persisted. Their exit code remains unknown.

An interrupted entry becomes replayable when its application is ready and its protected context is available. The UI status is **Interrupted** rather than falsely reporting completion or a fabricated exit code.

## Select Patient From History

Each history row exposes a separate **Select patient** command. It is available when:

- the entry was launched with patient or planning context;
- the existing DPAPI-protected context can be decrypted by the current Windows user; and
- that patient is present in the currently loaded ESAPI patient directory.

Executing the command selects the matching current `PatientRecord` through the existing patient-selection path. This triggers the normal treatment-context load but does not start or replay any application. It does not restore a prior course, plan, structure set, or image automatically; those remain visible in the history summary and can be selected deliberately after the patient context loads.

Entries started without a patient show the action disabled with **No patient stored for this run**. Missing protected context shows **Protected context is unavailable**. A patient absent from the current ESAPI directory shows **Patient is unavailable in the current directory**. No patient identifier is added to technical logs.

## UI

The activity table keeps **Run again** as the replay action and adds **Select patient** beside it. Both buttons fit without horizontal scrolling by using compact labels and the existing flexible columns. Tooltips state why either action is unavailable.

## Tests

Tests must fail before production changes and then cover:

- a child exit posted from a background thread completing through the captured UI dispatcher;
- terminal state being persisted and **Run again** becoming enabled;
- persisted `Starting` and `Running` rows recovering as `Interrupted` on startup;
- selecting an available history patient without starting an application;
- disabled patient selection for no-patient, unreadable-context, and missing-directory cases;
- existing patient/context replay, history retention, privacy, launcher, and dual-host tests remaining green.

## Acceptance Criteria

- A child script may exit normally or with an error without closing the Hub.
- The terminal row is persisted before the terminal log event completes.
- A completed, failed, or interrupted eligible row can be run again.
- Stale running rows from prior Hub crashes are repaired automatically.
- **Select patient** restores only the patient selection and never starts a script.
- Patient IDs remain confined to existing protected local history and are not added to technical logs.
- The Citrix application definition and dual read/write host routing remain unchanged.
