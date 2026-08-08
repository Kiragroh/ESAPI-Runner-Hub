# ESAPI Dual Script Host Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `executing-plans` to implement this plan task by task. This repository is handled inline because parallel agents were not requested.

**Goal:** Ship a backward-compatible read-only ESAPI host and a separately approvable write-enabled host, with automatic mode-based routing from the Runner Hub.

**Architecture:** Compile the reviewed host source into two x64 .NET Framework 4.8 executables. A compile-time capability and conditional ESAPI assembly marker create distinct approval identities; configuration chooses only the two executable paths, while validated `WriteMode` alone controls routing.

**Tech Stack:** C# 7.3, WPF, .NET Framework 4.8, MSBuild, Eclipse 18 ESAPI references, custom executable test harness, PowerShell release scripts.

---

## Task 1: Establish the dual-host contract with failing tests

**Files:**
- Modify: `tests/ESAPI.RunnerHub.Tests/ContextConfigurationTests.cs`
- Modify: `tests/ESAPI.RunnerHub.Tests/SettingsViewModelTests.cs`
- Modify: `tests/ESAPI.RunnerHub.Tests/ContextLaunchProtocolTests.cs`
- Modify: `tests/ESAPI.RunnerHub.Tests/ScriptHostCoreTests.cs`
- Modify: `tests/ESAPI.RunnerHub.Tests/ProjectShapeTests.cs`
- Modify: `tests/ESAPI.RunnerHub.Tests/ExampleSettingsTests.cs`
- Modify: `tests/ESAPI.RunnerHub.Tests/Program.cs`
- Add: `tests/ESAPI.RunnerHub.Tests/DualScriptHostTests.cs`
- Modify: `tests/ESAPI.RunnerHub.Tests/ESAPI.RunnerHub.Tests.csproj`

1. Add tests for `WriteScriptHostExecutable`, INI round-trip/resolution, Settings UI binding, and conditional validation when enabled direct write applications exist.
2. Add tests that `ReadOnly` routes to the read host and both `ConfirmSave` and `ExecuteAndDiscard` route to the write host.
3. Add host capability tests proving each host kind rejects the other kind's modes before session creation.
4. Add save-decision and context-series tests proving `ExecuteAndDiscard` never saves and both write modes are refused in series execution.
5. Add project/release-shape tests for the new project, executable, assembly marker, example setting, and vendor-free packaging.
6. Build and run the test executable; record the expected failures before production edits.

## Task 2: Implement configuration and automatic host selection

**Files:**
- Modify: `src/ESAPI.RunnerHub/Configuration/ApplicationDefinition.cs`
- Modify: `src/ESAPI.RunnerHub/Configuration/HubConfiguration.cs`
- Modify: `src/ESAPI.RunnerHub/Configuration/IniConfigurationStore.cs`
- Modify: `src/ESAPI.RunnerHub/Configuration/ConfigurationValidator.cs`
- Add: `src/ESAPI.RunnerHub/Launching/ScriptHostSelector.cs`
- Modify: `src/ESAPI.RunnerHub/Launching/ContextScriptRequestComposer.cs`
- Modify: `src/ESAPI.RunnerHub/ESAPI.RunnerHub.csproj`

1. Add `ExecuteAndDiscard` to both Runner and host request contracts.
2. Add default and resolved write-host paths to Hub settings and INI parsing/serialization.
3. Validate a missing write host only when an enabled direct context application requires it.
4. Route host selection solely from the validated mode; do not accept a host path from request JSON.
5. Run the focused configuration and routing tests until green.

## Task 3: Implement distinct read and write host capabilities

**Files:**
- Add: `src/ESAPI.ScriptHost/Host/ScriptHostCapability.cs`
- Modify: `src/ESAPI.ScriptHost/Contracts/ContextLaunchPayload.cs`
- Modify: `src/ESAPI.ScriptHost/EsapiAuthorizationMarker.cs`
- Modify: `src/ESAPI.ScriptHost/Program.cs`
- Modify: `src/ESAPI.ScriptHost/Host/ScriptHostApplication.cs`
- Modify: `src/ESAPI.ScriptHost/Host/SaveDecision.cs`
- Modify: `src/ESAPI.ScriptHost/Host/ScriptMetadataInspector.cs`
- Modify: `src/ESAPI.ScriptHost/ESAPI.ScriptHost.csproj`
- Add: `src/ESAPI.WriteScriptHost/ESAPI.WriteScriptHost.csproj`
- Add: `src/ESAPI.WriteScriptHost/Properties/AssemblyInfo.cs`
- Modify: `ESAPI-Runner-Hub.sln`
- Modify: `tests/ESAPI.RunnerHub.Tests/ESAPI.RunnerHub.Tests.csproj`

1. Introduce compile-time `ReadOnly`/`WriteEnabled` capability and validate it before ESAPI application/session creation.
2. Emit `[assembly: ESAPIScript(IsWriteable = true)]` only with `WRITE_HOST`.
3. Compile the same host sources into `ESAPI-Write-Script-Host.exe` without extracting approval-critical logic into a replaceable DLL.
4. Enforce child metadata compatibility: read-only children only in read mode; writable children only in either write mode.
5. Preserve confirm-save behavior and force execute-and-discard to close without `SaveModifications()`.
6. Build both hosts, run all automated tests, and reflect over both binaries to verify the marker boundary.

## Task 4: Expose both hosts in Settings and catalogue UX

**Files:**
- Modify: `src/ESAPI.RunnerHub/ViewModels/SettingsViewModel.cs`
- Modify: `src/ESAPI.RunnerHub/SettingsWindow.xaml`
- Modify: `src/ESAPI.RunnerHub/SettingsWindow.xaml.cs`
- Modify: `src/ESAPI.RunnerHub/MainWindow.xaml.cs`
- Modify: `src/ESAPI.RunnerHub/ViewModels/MainViewModel.cs` as required by bindings
- Modify: `settings.example.ini`

1. Show separate English path controls for the read-only and write-enabled executables.
2. Add the third write-mode choice without converting existing `ConfirmSave` entries.
3. Persist and validate both paths through the existing settings workflow.
4. Run Settings and configuration tests and manually inspect XAML bindings.

## Task 5: Package, document, and version v0.3.3

**Files:**
- Modify: `tools/build-release.ps1`
- Modify: `tools/validate-vendor-free.ps1` if artifact expectations require it
- Modify: `src/ESAPI.RunnerHub/Properties/AssemblyInfo.cs`
- Modify: `src/ESAPI.ScriptHost/Properties/AssemblyInfo.cs`
- Modify: `versionInfo.json`
- Modify: `README.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/CLINICAL_VALIDATION.md`
- Modify: `docs/RELEASE_VERIFICATION.md`
- Modify: `docs/FINAL_VERIFICATION.md`

1. Set all product versions to 0.3.3 and increment the visible build metadata.
2. Package both host executables/configs and include them in deterministic hashes without redistributing Varian assemblies.
3. Document approval boundaries, `ExecuteAndDiscard`, child-script governance, and the unchanged Citrix entry point in English.
4. Run the complete release build, test harness, Citrix launcher tests, vendor-free validation, and package hash verification.

## Task 6: Integrate and verify the productive release

**Files:**
- Update generated release artifacts under `dist/` only through `tools/build-release.ps1`
- Update productive `dist/settings.ini` while preserving all existing catalogue entries
- Update STR Hub/InHouse metadata through the supported Hub registration workflow

1. Review the full diff and ensure no unrelated or patient-near files are included.
2. Commit the implementation, merge the feature branch into `main`, tag `v0.3.3`, and push the configured remotes.
3. Deploy the versioned Runner, both stable host filenames, and the shared two-path setting.
4. Verify hashes, versions, executable metadata, Citrix pointer, read-only launch, logs, and the Hub-visible version.
5. Report the one remaining clinical action explicitly: register/evaluate/approve the exact write-host binary before a write-mode live test can succeed.
