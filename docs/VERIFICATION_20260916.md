# Public source verification - 2026-09-16

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
