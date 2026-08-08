# Live Eclipse validation checklist

Automated tests and public screenshots use only synthetic data. Before local clinical rollout, validate the exact build on an authorized Eclipse workstation with institutionally approved test data and target applications.

## Patient directory

- Confirm the configured API and Types assemblies match the installed Eclipse/ESAPI version.
- Start the Hub and confirm the ESAPI status reaches `ready` without retaining an open patient.
- Search by exact test ID, partial ID, first name, last name, and multiple tokens.
- Select, clear, refresh, and switch the test patient repeatedly.
- Verify courses, plans, plan sums, structure sets, and images populate correctly and that a structure set or image can be selected without a plan.

## Launch contracts

- Verify one classic runner can start without patient transfer and performs its own selection.
- Verify an argument-aware test EXE receives exactly the selected test ID.
- Verify an environment-aware test EXE receives the ID only under its configured child key.
- Verify a required-patient card cannot start without an exact selection.
- Start several applications for the same patient, then switch patient and repeat.
- Start a validated direct binary and direct single-file source with the required patient/planning context.
- Confirm `ReadOnly` launches use `ESAPI-Script-Host.exe` and that this executable has no write-enabled ESAPI approval.
- Register the exact released `ESAPI-Write-Script-Host.exe` as a standalone write-enabled script, complete institutional evaluation/validation/approval, and record its SHA-256. Any rebuilt binary is a new approval candidate.
- With a reviewed synthetic write test script, verify `ConfirmSave` uses the write host and presents a fresh save/discard question after normal completion; verify discard, cancel, failure, and abnormal exit never save.
- Verify `ExecuteAndDiscard` uses the write host, shows the initial write warning, never asks to save, and leaves no persistent patient change.
- Verify the read host refuses both write modes and the write host refuses a read-only payload before a patient is opened.
- Confirm every child write script retains its own version, approval evidence, and validation record. Do not treat host approval as approval of dynamically loaded child code.

## Isolation and paths

- Trigger a controlled non-zero child exit and controlled child crash; verify the Hub remains usable and can start another child.
- Configure a missing local EXE and an unavailable optional network path; verify only those cards are disabled.
- Confirm a slow network probe does not freeze patient search, settings, or other application cards.

## Privacy

- Inspect technical logs and crash reports; verify they contain no patient ID/name, search text, expanded argument, environment value, or child output.
- Confirm closing and reopening the Hub restores no recent patient selection.
- Confirm recent activity is restored but its JSON contains no clear patient, course, plan, structure-set, or image identifier.
- Confirm **Run again** reconstructs the current configuration, resolves the saved context afresh, and safely disables entries whose application or context no longer exists.

Record the Eclipse version, ESAPI assembly versions, Hub commit/tag, target application versions, workstation class, test date, tester, and result in the local validation system. Do not commit clinical identifiers or screenshots to the public repository.

Until the released write host has been approved, an authorization failure when a child reaches `BeginModifications()` is the expected clinical-system result. It does not invalidate read-host operation or the synthetic release gates.
