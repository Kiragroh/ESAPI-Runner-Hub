# Native ClearPlan testing with ESAPI Runner Hub

Runner Hub is an optional execution and debugging route for native ESAPI checks. It is not a ClearPlan calculation dependency, a treatment-planning-system replacement, or an authorization or approval bypass.

| Evidence layer | Runner required? | What it establishes |
| --- | --- | --- |
| ClearPlan core regression tests (495 in the documented ClearPlan snapshot) | No | Behavior of detached calculation logic and synthetic fixtures |
| ClearPlan simulator | No | Synthetic application behavior without native patient access |
| Native ESAPI checks and interactive ClearPlan GUI | Optional host route | Execution against an explicitly selected context in a licensed, authorized Eclipse/ESAPI environment |
| Clinical acceptance and report/render review | No particular launcher is sufficient | Separate local review of results, context, presentation, and intended clinical use |

The test count belongs to a particular ClearPlan revision, not to Runner's own test suite. ClearPlan can also run inside Eclipse or another suitable approved host. Runner is especially useful where Citrix exposes a single published application rather than a full desktop: it provides one stable entry point, precise context selection, isolated child execution, and reproducible request/result correlation.

## Controlled native procedure

1. Record the exact ClearPlan and Runner revisions, configuration identity, ESAPI version, selected test, and expected output. Keep identifiers and native artifacts on approved protected storage.
2. Obtain local authorization for the target script, host and data. Use a read-only target with `WriteMode=ReadOnly` and the read-only Script Host. Runner does not grant privileges or remove vendor approval requirements.
3. Specify the exact patient, course and plan (or other required planning context). For cross-VDA work use a shared request rather than `--replay-latest`, whose result depends on saved history.
4. Submit through the configured shared request directory. The helper creates a Windows-SID-specific pending marker and opens the normal Citrix published shortcut. The VDA atomically claims the request and checks the requesting SID. This route does not depend on client argument forwarding.
5. Match the request ID to the result, host, timestamps and exit code. Inspect the intended application output separately; process exit zero is not proof that a GUI rendered correctly, a report was reviewed, or a clinical calculation was validated.
6. For a read-only series, use explicit contexts and the bounded serial protocol described in [Context and Citrix debugging](CONTEXT_DEBUGGING.md). Review individual target evidence, not only the aggregate process result.

Each child opens its own selected ESAPI context. ESAPI access and patient lifetime remain governed by the host's STA/session rules; no live patient or plan object is passed between processes. Process isolation limits failures affecting the Hub but does not authorize unsafe concurrent access.

## Privacy and evidence boundaries

Technical logs omit patient names, IDs, expanded arguments, environment values and child output. This does **not** make the entire debugging workflow anonymous: shared request JSON, target-specific helper exports, screenshots and reports may contain identifiable data. They require restricted storage and must not be committed, attached to public issues, or bundled in releases.

The SID check is request ownership validation within a trusted, correctly ACL-protected deployment. It is not a substitute for share permissions, script review, vendor licensing, host approval or institutional governance.

## History in 0.3.12

New saved context envelopes use Windows DPAPI-NG scoped to the current account SID. Cross-domain-host decryption depends on the Windows/domain environment and must be validated locally. Legacy CurrentUser-DPAPI envelopes are upgraded only when readable and verified; unavailable opaque entries are retained.

`HistoryFile` is the primary file; optional `HistoryFallbackFile` supplies local recovery and `HistoryMigrationFile` imports previous history without deleting it. Exclusive file locks, merge-before-save, backups and separate durable snapshots protect concurrent history and completed outcomes. The defaults remain local, 30 days and 100 rows; shared paths must be configured as user-specific protected locations.

History I/O runs in the background. Window close allows bounded shared synchronization and checks local write completion, including view models retained across Settings reloads. A loaded Starting/Running row is displayed as **Previous session - status unknown** and is not silently rewritten as Interrupted; another process may still own it. Selecting the patient and launching from the current catalogue remains distinct from replay. These are persistence behaviors, not proof of target completion or clinical correctness.

## Portable verification

Runner tests use synthetic fixtures, including a VMS-shaped test assembly built from this repository. The synthetic assembly is not the vendor API and cannot establish native compatibility. Build/rebuild against local vendor references is a separate compile check; live ESAPI execution and local clinical acceptance remain additional gates.

See [Clinical validation checklist](CLINICAL_VALIDATION.md) and the request protocol in [Context and Citrix debugging](CONTEXT_DEBUGGING.md).
