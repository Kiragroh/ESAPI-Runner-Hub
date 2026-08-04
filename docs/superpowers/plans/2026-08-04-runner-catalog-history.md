# Runner Catalogue and Persistent History Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish a Runner Hub release that directly starts ClearPlan and supported source/binary context scripts, explains every card, filters by artifact type, and securely retains restartable launch activity across Windows sessions.

**Architecture:** Extend the INI model with explicit metadata and safe inference, then keep display classification in a focused `ApplicationMetadata` component. Persist detached launch records in a local atomic JSON store; protect context identifiers with Windows DPAPI and always recompose a relaunch from the current application definition. Existing request composers, child-process isolation, Eclipse 18 context adapter, and save/discard gate remain authoritative.

**Tech Stack:** C# 7.3, WPF, .NET Framework 4.8 x64, `DataContractJsonSerializer`, Windows DPAPI `ProtectedData`, PowerShell release tooling, dependency-free executable tests, Eclipse 18 ESAPI reflection host.

---

## File structure

- `src/ESAPI.RunnerHub/Configuration/ApplicationDefinition.cs` — artifact/access enums and per-card metadata.
- `src/ESAPI.RunnerHub/Configuration/HubConfiguration.cs` — Hub URL and history retention/path settings.
- `src/ESAPI.RunnerHub/Configuration/IniConfigurationStore.cs` — backward-compatible persistence.
- `src/ESAPI.RunnerHub/Catalog/ApplicationMetadata.cs` — type/access classification, compact path, README URI.
- `src/ESAPI.RunnerHub/History/LaunchHistoryEntry.cs` — detached persisted record and enums.
- `src/ESAPI.RunnerHub/History/ProtectedContextEnvelope.cs` — minimal context DTO and DPAPI boundary.
- `src/ESAPI.RunnerHub/History/LaunchHistoryStore.cs` — atomic JSON load/save, retention, corruption recovery.
- `src/ESAPI.RunnerHub/ViewModels/ActivityRowViewModel.cs` — unified active and historical row, relaunch state.
- `src/ESAPI.RunnerHub/ViewModels/ApplicationCardViewModel.cs` — card metadata presentation.
- `src/ESAPI.RunnerHub/ViewModels/MainViewModel.cs` — artifact filter, history lifecycle, README and relaunch commands.
- `src/ESAPI.RunnerHub/MainWindow.xaml` — filters, richer cards, persistent activity table.
- `src/ESAPI.RunnerHub/SettingsWindow.xaml` and `ViewModels/SettingsViewModel.cs` — editable metadata/history settings.
- `tests/ESAPI.RunnerHub.Tests/*` — test-first contracts for each component.
- `dist/settings.ini`, `settings.example.ini` — live and portable configuration.
- `README.md`, `CHANGELOG.md`, `versionInfo.json`, assembly metadata, release tooling — publication contract.

### Task 1: Stabilize the approved planning-context baseline

**Files:**
- Modify/verify: `src/ESAPI.RunnerHub/Context/ContextDescriptor.cs`
- Modify/verify: `src/ESAPI.RunnerHub/Context/ContextSelection.cs`
- Modify/verify: `src/ESAPI.RunnerHub/ViewModels/MainViewModel.cs`
- Modify/verify: `src/ESAPI.RunnerHub/MainWindow.xaml`
- Modify/verify: `src/ESAPI.ScriptHost/Host/ContextResolver.cs`
- Test: `tests/ESAPI.RunnerHub.Tests/MainContextViewModelTests.cs`
- Test: `tests/ESAPI.RunnerHub.Tests/ScriptHostCoreTests.cs`

- [ ] **Step 1: Build and run the already-written context tests**

Run:

```powershell
$msbuild = & "$env:ProgramFiles(x86)\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
& $msbuild ESAPI-Runner-Hub.sln /t:Build /p:Configuration=Debug /p:Platform=x64 "/p:EsapiReferenceDirectory=\\medizin.uni-leipzig.de\data\Archiv\STR\STR-Physik\11. Scripting\ESAPI-MG\_Assets"
& .\tests\ESAPI.RunnerHub.Tests\bin\x64\Debug\ESAPI.RunnerHub.Tests.exe
```

Expected: `RESULT 80 passed, 0 failed` or a larger all-green count.

- [ ] **Step 2: Commit only the current context selector/host work**

Stage the current context/config/settings UI files and tests, verify `git diff --cached --check`, then commit:

```powershell
git commit -m "feat: select complete ESAPI planning context"
```

### Task 2: Add configuration metadata and history settings

**Files:**
- Modify: `src/ESAPI.RunnerHub/Configuration/ApplicationDefinition.cs`
- Modify: `src/ESAPI.RunnerHub/Configuration/HubConfiguration.cs`
- Modify: `src/ESAPI.RunnerHub/Configuration/IniConfigurationStore.cs`
- Modify: `src/ESAPI.RunnerHub/Configuration/ConfigurationValidator.cs`
- Modify: `src/ESAPI.RunnerHub/ViewModels/SettingsViewModel.cs`
- Modify: `src/ESAPI.RunnerHub/SettingsWindow.xaml`
- Test: `tests/ESAPI.RunnerHub.Tests/CatalogConfigurationTests.cs`

- [ ] **Step 1: Write failing configuration round-trip and validation tests**

Define the intended API in `CatalogConfigurationTests.cs`:

```csharp
var config = IniConfigurationStore.ParseText(@"
[Hub]
StrHubBaseUrl=https://str-hub.example/
HistoryFile=%LOCALAPPDATA%\ESAPI Runner Hub\launch-history.json
HistoryRetentionDays=30
HistoryMaxEntries=100
[Application.clearplan-direct]
Name=ClearPlan directly
Executable=ClearPlan.esapi.dll
LaunchKind=EsapiContextScript
ArtifactKind=Binary
AccessMode=ReadOnly
HubScriptId=62
ContextRequirement=PlanningItem
PatientMode=Required
PatientTransport=None
", tempIni);
TestHarness.AssertEqual(ApplicationArtifactKind.Binary, config.Applications.Single().ArtifactKind);
TestHarness.AssertEqual(ApplicationAccessMode.ReadOnly, config.Applications.Single().AccessMode);
TestHarness.AssertEqual(62, config.Applications.Single().HubScriptId);
TestHarness.AssertEqual(30, config.Hub.HistoryRetentionDays);
TestHarness.AssertEqual(100, config.Hub.HistoryMaxEntries);
```

Also assert rejection of negative IDs, retention outside `1..365`, and max entries outside `1..1000`.

- [ ] **Step 2: Run tests and verify RED**

Expected: compile failure because the enums and fields do not exist.

- [ ] **Step 3: Implement minimal enums, defaults, INI cases, validation, and settings bindings**

Add:

```csharp
public enum ApplicationArtifactKind { Auto, Standalone, SingleFile, Binary }
public enum ApplicationAccessMode { Auto, ReadOnly, WriteEnabled, Unknown }
```

Use defaults `Auto`, `Auto`, no Hub ID, `%LOCALAPPDATA%\ESAPI Runner Hub\launch-history.json`, 30 days, and 100 entries. Persist every value through `IniConfigurationStore.Save` and expose enum lists in `SettingsViewModel`.

- [ ] **Step 4: Run all tests and verify GREEN**

Expected: all configuration and regression tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src/ESAPI.RunnerHub/Configuration src/ESAPI.RunnerHub/ViewModels/SettingsViewModel.cs src/ESAPI.RunnerHub/SettingsWindow.xaml tests/ESAPI.RunnerHub.Tests
git commit -m "feat: configure runner catalogue metadata"
```

### Task 3: Classify and explain catalogue entries

**Files:**
- Create: `src/ESAPI.RunnerHub/Catalog/ApplicationMetadata.cs`
- Modify: `src/ESAPI.RunnerHub/ESAPI.RunnerHub.csproj`
- Modify: `src/ESAPI.RunnerHub/ViewModels/ApplicationCardViewModel.cs`
- Test: `tests/ESAPI.RunnerHub.Tests/ApplicationMetadataTests.cs`

- [ ] **Step 1: Write failing metadata tests**

Cover extension inference, explicit override, access inference, compact paths, and Hub links:

```csharp
TestHarness.AssertEqual(ApplicationArtifactKind.Standalone, ApplicationMetadata.ArtifactFor(ExeDefinition()));
TestHarness.AssertEqual(ApplicationArtifactKind.SingleFile, ApplicationMetadata.ArtifactFor(SourceDefinition()));
TestHarness.AssertEqual(ApplicationArtifactKind.Binary, ApplicationMetadata.ArtifactFor(BinaryDefinition()));
TestHarness.AssertEqual("Physik-Skripte\\ESAPI-MG\\plugins\\ColorCode.cs",
    ApplicationMetadata.CompactPath(@"\\server\data\Archiv\STR\STR-Physik\11. Scripting\ESAPI-MG\plugins\ColorCode.cs"));
TestHarness.AssertEqual("https://str-hub.example/#/inhouse/62",
    ApplicationMetadata.BuildReadmeUri("https://str-hub.example/", 62).AbsoluteUri);
TestHarness.AssertEqual(null, ApplicationMetadata.BuildReadmeUri(string.Empty, 62));
```

Assert `ConfirmSave => WriteEnabled`, `ReadOnly => ReadOnly`, and unknown external EXEs remain `Unknown` unless configured.

- [ ] **Step 2: Run tests and verify RED**

Expected: missing `ApplicationMetadata`.

- [ ] **Step 3: Implement deterministic metadata**

`CompactPath` searches case-insensitively for `\STR-Physik\11. Scripting\`, replaces that root with `Physik-Skripte\`, and otherwise returns a filename or an ellipsized final three-component tail. `BuildReadmeUri` accepts only absolute HTTP(S) base URLs and positive IDs.

Expose card properties `ArtifactLabel`, `AccessLabel`, `CompactPath`, `HasHubReadme`, and `HubReadmeUri`.

- [ ] **Step 4: Run all tests and verify GREEN**

- [ ] **Step 5: Commit**

```powershell
git add src/ESAPI.RunnerHub/Catalog src/ESAPI.RunnerHub/ViewModels/ApplicationCardViewModel.cs src/ESAPI.RunnerHub/ESAPI.RunnerHub.csproj tests/ESAPI.RunnerHub.Tests
git commit -m "feat: explain configured script artifacts"
```

### Task 4: Add the artifact filter and card actions

**Files:**
- Modify: `src/ESAPI.RunnerHub/ViewModels/MainViewModel.cs`
- Modify: `src/ESAPI.RunnerHub/MainWindow.xaml`
- Modify: `src/ESAPI.RunnerHub/MainWindow.xaml.cs`
- Test: `tests/ESAPI.RunnerHub.Tests/MainViewModelTests.cs`

- [ ] **Step 1: Write failing combined-filter and README tests**

Create entries for `.exe`, `.cs`, and `.esapi.dll`; select `SingleFile`; assert only the source entry remains. Then set a category and text filter and assert all three predicates combine. Assert the README command is disabled without a valid URI.

- [ ] **Step 2: Run tests and verify RED**

Expected: missing `SelectedArtifactFilter`/filter options.

- [ ] **Step 3: Implement the filter and card layout**

Add `ApplicationArtifactFilter { All, Standalone, SingleFile, Binary }` and an `ArtifactFilterOption` list with user-facing labels. Extend `UpdateVisibleApplications` with the selected kind predicate. Add a horizontal filter control above the card grid.

Increase card minimum height only enough for two badges and one compact path line. Add a secondary `STR Hub README` button wired to a command that uses `ProcessStartInfo` with `UseShellExecute=true`; opening documentation is not recorded as a tool launch.

- [ ] **Step 4: Run tests and verify GREEN**

- [ ] **Step 5: Commit**

```powershell
git add src/ESAPI.RunnerHub/MainWindow.xaml src/ESAPI.RunnerHub/MainWindow.xaml.cs src/ESAPI.RunnerHub/ViewModels tests/ESAPI.RunnerHub.Tests/MainViewModelTests.cs
git commit -m "feat: filter and document runner cards"
```

### Task 5: Persist encrypted launch history

**Files:**
- Create: `src/ESAPI.RunnerHub/History/LaunchHistoryEntry.cs`
- Create: `src/ESAPI.RunnerHub/History/ProtectedContextEnvelope.cs`
- Create: `src/ESAPI.RunnerHub/History/LaunchHistoryStore.cs`
- Modify: `src/ESAPI.RunnerHub/ESAPI.RunnerHub.csproj`
- Modify: `tests/ESAPI.RunnerHub.Tests/ESAPI.RunnerHub.Tests.csproj`
- Test: `tests/ESAPI.RunnerHub.Tests/LaunchHistoryTests.cs`

- [ ] **Step 1: Write failing DPAPI and store tests**

Use a unique temporary directory and a synthetic selection:

```csharp
var selection = new ContextSelection { PatientId = "SYN-1001", CourseId = "C1", PlanId = "P1", StructureSetId = "SS1", ImageId = "IMG1" };
var protector = new ProtectedContextEnvelope();
var encrypted = protector.Protect(selection);
TestHarness.AssertFalse(encrypted.Contains("SYN-1001"));
TestHarness.AssertEqual("P1", protector.Unprotect(encrypted).PlanId);
```

Save more than the configured maximum and entries older than retention; assert only the newest eligible entries remain. Replace the file with invalid JSON; assert `Load()` returns an empty collection without throwing. Assert the persisted text contains neither patient nor plan IDs.

- [ ] **Step 2: Run tests and verify RED**

Expected: missing history types.

- [ ] **Step 3: Implement the detached, atomic store**

Define data-contract enums `LaunchMode` and `LaunchHistoryState`, plus `LaunchHistoryEntry` fields from the spec. `ProtectedContextEnvelope` serializes a private context DTO to UTF-8 and calls:

```csharp
ProtectedData.Protect(bytes, optionalEntropy, DataProtectionScope.CurrentUser)
```

Use an application-specific fixed entropy byte string. `LaunchHistoryStore.Save` writes `<path>.<guid>.tmp`, then `File.Replace` when the destination exists or `File.Move` otherwise. Catch load/save errors at the public boundary and return a technical result without sensitive exception text.

- [ ] **Step 4: Run tests and verify GREEN**

- [ ] **Step 5: Commit**

```powershell
git add src/ESAPI.RunnerHub/History src/ESAPI.RunnerHub/ESAPI.RunnerHub.csproj tests/ESAPI.RunnerHub.Tests
git commit -m "feat: persist protected launch history"
```

### Task 6: Record lifecycle and recompose relaunches

**Files:**
- Create: `src/ESAPI.RunnerHub/ViewModels/ActivityRowViewModel.cs`
- Modify: `src/ESAPI.RunnerHub/ViewModels/MainViewModel.cs`
- Modify: `src/ESAPI.RunnerHub/MainWindow.xaml.cs`
- Remove after migration: `src/ESAPI.RunnerHub/ViewModels/ProcessRowViewModel.cs`
- Test: `tests/ESAPI.RunnerHub.Tests/LaunchHistoryViewModelTests.cs`

- [ ] **Step 1: Write failing launch/relaunch tests**

Using `RunnerFixture.exe` and a temporary history store, assert:

- a successful standalone start creates `Running` then `Exited`;
- a failed target creates `FailedToStart` and remains retryable after the file returns;
- a patient-aware relaunch passes the decrypted patient ID through the current definition;
- a context relaunch decrypts the exact plan/structure/image selection and passes it through `ContextScriptRequestComposer`;
- a removed or disabled application produces `Unavailable` and does not start a process;
- a script failure never closes the Hub and a later relaunch still starts.

- [ ] **Step 2: Run tests and verify RED**

Expected: no persisted `Activities` collection or `RunAgainCommand`.

- [ ] **Step 3: Implement one launch pipeline**

Introduce a private method with this contract:

```csharp
private void Launch(ApplicationCardViewModel card, LaunchMode mode, PatientRecord patient, ContextSelection selection)
```

It creates one history record, composes with the existing `ArgumentComposer` or `ContextScriptRequestComposer`, starts via `ChildProcessLauncher`, subscribes to `RunningProcessInfo.Exited`, updates and saves the record, and inserts an `ActivityRowViewModel`. All existing start commands call this method.

`RunAgain(ActivityRowViewModel row)` looks up the current enabled card by application ID, decrypts context only for patient/context modes, and calls the same method. Never persist or reuse expanded arguments, environment dictionaries, or executable paths.

- [ ] **Step 4: Run all tests and verify GREEN**

- [ ] **Step 5: Commit**

```powershell
git add src/ESAPI.RunnerHub/ViewModels src/ESAPI.RunnerHub/MainWindow.xaml.cs tests/ESAPI.RunnerHub.Tests
git commit -m "feat: relaunch protected activity records"
```

### Task 7: Replace the process footer with persistent activity UI

**Files:**
- Modify: `src/ESAPI.RunnerHub/MainWindow.xaml`
- Test: `tests/ESAPI.RunnerHub.Tests/ProjectShapeTests.cs`

- [ ] **Step 1: Write a failing XAML shape test**

Assert the main XAML binds to `Activities`, contains `RunAgainCommand`, and includes columns/bindings for application, type, context summary, started, status, and action.

- [ ] **Step 2: Run tests and verify RED**

- [ ] **Step 3: Implement the compact activity panel**

Use a `ListView` with a `GridViewColumn.CellTemplate` button:

```xml
<Button Content="Run again"
        Command="{Binding DataContext.RunAgainCommand, RelativeSource={RelativeSource AncestorType=Window}}"
        CommandParameter="{Binding}"
        IsEnabled="{Binding CanRunAgain}" />
```

Show up to the retained entries with a vertical scrollbar. The context summary displays only decrypted IDs in the active user session; it never becomes log or JSON text.

- [ ] **Step 4: Run tests and verify GREEN**

- [ ] **Step 5: Commit**

```powershell
git add src/ESAPI.RunnerHub/MainWindow.xaml tests/ESAPI.RunnerHub.Tests/ProjectShapeTests.cs
git commit -m "feat: show restartable activity sessions"
```

### Task 8: Configure direct ClearPlan, binary, and source examples

**Files:**
- External live update (Git-ignored): `dist/settings.ini`
- Modify: `settings.example.ini`
- Test: `tests/ESAPI.RunnerHub.Tests/ExampleSettingsTests.cs`

- [ ] **Step 1: Write failing live-settings tests**

Load `settings.example.ini` and assert it contains distinct standalone, direct binary, and direct source examples with explicit artifact/access metadata. Add a focused live-validation command that loads the Git-ignored `dist/settings.ini` and asserts distinct `clearplan-runner` and `clearplan-direct` entries. The live validation must also assert direct ClearPlan is a binary, read-only EsapiEssentials context script requiring a planning item; Plan FieldNamer is a write-enabled binary requiring a plan; and at least one direct `.cs` entry is classified single-file/read-only. Known Hub IDs 13, 14, 34, 35, 42, 47, 54, 59, or 62 may only be assigned to their verified mappings.

- [ ] **Step 2: Run tests and verify RED**

Expected: the tracked example lacks the new examples and the live INI has only the old ClearPlan runner.

- [ ] **Step 3: Update live and example INIs**

Add `ScriptHostExecutable=ESAPI-Script-Host.exe`, history/Hub settings, explicit artifact/access metadata, and the following to the preserved Git-ignored live INI:

```ini
[Application.clearplan-direct]
Name=ClearPlan directly
LaunchKind=EsapiContextScript
Executable=..\ClearPlan_OpenSource\built\ClearPlan.esapi.dll
WorkingDirectory=..\ClearPlan_OpenSource\built
ScriptEngine=EsapiEssentials
EntryType=VMS.TPS.Script
ContextRequirement=PlanningItem
ScopeMode=Multiple
WriteMode=ReadOnly
PatientMode=Required
PatientTransport=None
ArtifactKind=Binary
AccessMode=ReadOnly
HubScriptId=62
```

Configure `_Plan-FieldNamer\debug\Plan_FieldNamer.esapi.dll` as `Eclipse`, `Plan`, `Single`, `ConfirmSave`, `Binary`, `WriteEnabled`, Hub ID 47. Convert conservative read-only source examples such as `PatientDataDiscovery.cs` into direct `EsapiContextScript` entries while leaving unknown scripts as Eclipse reference cards.

- [ ] **Step 4: Run all tests and verify GREEN**

- [ ] **Step 5: Commit**

```powershell
git add settings.example.ini tests/ESAPI.RunnerHub.Tests
git commit -m "feat: configure direct ESAPI applications"
```

After the commit, query the live INI back through `IniConfigurationStore.Load`, print only application IDs and non-sensitive classifications, and retain the live file in place for release staging.

### Task 9: Package the script host and document the new workflow

**Files:**
- Modify: `tools/build-release.ps1`
- Modify: `tools/validate-vendor-free.ps1`
- Modify: `README.md`
- Modify: `docs/CLINICAL_VALIDATION.md`
- Test: `tests/ESAPI.RunnerHub.Tests/ReleaseMetadataTests.cs`

- [ ] **Step 1: Write failing package-contract tests**

Assert the release script packages `ESAPI-Script-Host.exe` adjacent to the Runner and preserves live `dist/settings.ini`. Assert the vendor scan accepts the first-party host EXE but still rejects `VMS.TPS.*` and other vendor DLLs.

- [ ] **Step 2: Run tests and verify RED**

- [ ] **Step 3: Update release tooling and docs**

Build both Hub and host x64 Release outputs. Copy the host to the ZIP and `dist`; publish an immutable versioned host if the Citrix launcher references it, otherwise keep the versioned Hub reading the stable `dist` host through `dist/settings.ini`. Document filters, metadata, DPAPI history, relaunch behavior, direct ClearPlan, source compilation cache, write confirmation, and the separate clinical validation gate.

- [ ] **Step 4: Run tests and verify GREEN**

- [ ] **Step 5: Commit**

```powershell
git add tools README.md docs/CLINICAL_VALIDATION.md tests/ESAPI.RunnerHub.Tests/ReleaseMetadataTests.cs
git commit -m "build: package isolated ESAPI script host"
```

### Task 10: Release, update STR Hub README, and verify live state

**Files:**
- Modify: `src/ESAPI.RunnerHub/Properties/AssemblyInfo.cs`
- Create/modify: `src/ESAPI.ScriptHost/Properties/AssemblyInfo.cs`
- Modify: `versionInfo.json`
- Modify: `CHANGELOG.md`
- Modify: `citrix/current.txt`
- External controlled update: STR Hub `inhouse.db`, script ID 62 README only, after backup

- [ ] **Step 1: Write failing v0.2.0 release assertions**

Update tests first to require version `0.2.0`, build `8`, matching assembly/file/informational versions, changelog text for direct context scripts and encrypted history, and Citrix current version `0.2.0`.

- [ ] **Step 2: Run tests and verify RED**

- [ ] **Step 3: Apply release metadata and update README content**

Set date `2026-08-04`. Preserve previous JSON changelog entries. Back up `inhouse.db`, update only script ID 62's README using a parameterized SQLite statement, and query the row back. Do not add a manual DB changelog entry.

- [ ] **Step 4: Build and verify the release**

Run:

```powershell
.\tools\build-release.ps1 -Version 0.2.0 -EsapiReferenceDirectory "\\medizin.uni-leipzig.de\data\Archiv\STR\STR-Physik\11. Scripting\ESAPI-MG\_Assets"
```

Expected: all C# tests, Citrix launcher tests, vendor-free validation, ZIP creation, SHA-256 generation, and immutable version publication pass. Confirm `dist\ESAPI-Runner-Hub.exe` and `dist\ESAPI-Script-Host.exe` exist and hash successfully; confirm `dist\settings.ini` is preserved.

- [ ] **Step 5: Perform non-clinical live smoke checks**

Use the already-open Runner window or a single synthetic smoke process—never open multiple duplicates. Verify visible direct ClearPlan/binary/source cards, type/access/path badges, filters, README button, persistent activity reload, and safe unavailable behavior. Do not launch a write-enabled clinical script or use real patient data for automated evidence.

- [ ] **Step 6: Commit, tag, push, and verify Hub version**

```powershell
git add src/ESAPI.RunnerHub/Properties/AssemblyInfo.cs src/ESAPI.ScriptHost/Properties/AssemblyInfo.cs versionInfo.json CHANGELOG.md citrix/current.txt
git commit -m "release: publish ESAPI Runner Hub v0.2.0"
git tag -a v0.2.0 -m "ESAPI Runner Hub v0.2.0"
git push <configured-public-remote> main --tags
git push <configured-local-backup-remote> main --tags
```

Create or update the GitHub release with the vendor-free ZIP and checksum files. Verify the STR Hub version resolver for script ID 62 reports version `0.2.0`, build `8`, and the new changelog; if HTTP is authentication-gated, run the backend resolver against the live DB checkout.

## Final verification checklist

- [ ] All new behavior was introduced by a failing test and then made green.
- [ ] Full Debug and Release x64 builds use Eclipse 18 `_Assets` references.
- [ ] ClearPlan runner and direct binary cards are both visible and launch through different contracts.
- [ ] At least one direct source script and two direct binary scripts are configured.
- [ ] Read/write badges agree with explicit metadata and the host write gate.
- [ ] Compact paths never alter launch paths.
- [ ] History JSON contains no clear patient, plan, structure, or image identifiers.
- [ ] Relaunch uses current application definitions and re-resolves ESAPI context.
- [ ] Child crashes and missing network paths remain isolated.
- [ ] Live settings survive packaging.
- [ ] Vendor assemblies are absent from public artifacts and Git.
- [ ] Version, build, changelog, tag, GitHub release, local backup, and STR Hub view agree.
