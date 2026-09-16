# Public source verification - 2026-09-16

## Source-only development candidate 0.3.13 (build 31)

The shared history Save is queued separately after local staging. This prevents an inline continuation blocked on shared storage from delaying the next local recovery write. Only detached history snapshots are involved; no ESAPI object access or host authorization changed.

- Unchanged two-second regression: RED on the curated 0.3.12 baseline, GREEN after the scheduling fix; 10 focused repetitions passed.
- Full portable suite: **202 passed, 0 failed**.
- Release/x64 rebuild against local Eclipse 18 metadata and synthetic rebuild: passed.
- CMD launcher: 8 passed; EXE launcher: 5 passed.
- Offscreen history/window lifecycle: 2 passed, including bounded shared-storage close.
- Offscreen real card-template rendering: passed with synthetic data.

This candidate is published as source only. No deployed binary, Citrix pointer, release tag or release asset was changed or published, and no new native patient run was performed. Read host 0.3.4, write host 0.3.5 and Citrix launcher 0.3.3 version contracts are unchanged. The operational 0.3.12 captures do not validate the new 0.3.13 source.

## Curated 0.3.12 baseline (build 30)

The public source was synchronized from the inspected operational 0.3.12 source while preserving synthetic configuration examples, independent-project disclaimers and public history. No clinical runtime, Citrix pointer or deployed binary was changed.

- Release/x64 rebuild against locally available Eclipse 18 metadata: passed.
- Synthetic Release/x64 rebuild: passed.
- Portable Runner suite: **201 passed, 1 failed**. The unchanged two-second delayed-history-load test exposed shared I/O blocking a later local-recovery continuation; this baseline is not an all-green release.
- CMD launcher contract: 8 passed; EXE launcher contract: 5 passed.
- Offscreen history/window lifecycle checks: 2 passed.
- Offscreen real card-template rendering with synthetic data: passed.

The marker-inspection test now accepts its adjacent synthetic VMS-shaped fixture instead of requiring an ancestor vendor-reference directory. This is test portability, not native ESAPI validation. No vendor binary, local settings, patient data, clinical log or private helper output was added to source control.

Portable tests, compile checks and offscreen synthetic rendering do not establish native clinical compatibility, GUI/report correctness for real data, or clinical acceptance. Earlier native ClearPlan captures used operational Runner 0.3.12 and must not be attributed to a subsequently changed public source revision.
