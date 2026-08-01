# Live Eclipse validation checklist

Automated tests and public screenshots use only synthetic data. Before local clinical rollout, validate the exact build on an authorized Eclipse workstation with institutionally approved test data and target applications.

## Patient directory

- Confirm the configured API and Types assemblies match the installed Eclipse/ESAPI version.
- Start the Hub and confirm the ESAPI status reaches `ready` without retaining an open patient.
- Search by exact test ID, partial ID, first name, last name, and multiple tokens.
- Select, clear, refresh, and switch the test patient repeatedly.

## Launch contracts

- Verify one classic runner can start without patient transfer and performs its own selection.
- Verify an argument-aware test EXE receives exactly the selected test ID.
- Verify an environment-aware test EXE receives the ID only under its configured child key.
- Verify a required-patient card cannot start without an exact selection.
- Start several applications for the same patient, then switch patient and repeat.

## Isolation and paths

- Trigger a controlled non-zero child exit and controlled child crash; verify the Hub remains usable and can start another child.
- Configure a missing local EXE and an unavailable optional network path; verify only those cards are disabled.
- Confirm a slow network probe does not freeze patient search, settings, or other application cards.

## Privacy

- Inspect technical logs and crash reports; verify they contain no patient ID/name, search text, expanded argument, environment value, or child output.
- Confirm closing and reopening the Hub restores no recent patient selection.

Record the Eclipse version, ESAPI assembly versions, Hub commit/tag, target application versions, workstation class, test date, tester, and result in the local validation system. Do not commit clinical identifiers or screenshots to the public repository.
