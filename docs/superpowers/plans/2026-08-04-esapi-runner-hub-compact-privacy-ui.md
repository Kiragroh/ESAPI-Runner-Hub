# ESAPI Runner Hub Compact Privacy UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver v0.3.0 with a compact two-column WPF interface, reliable activity replay state, privacy-safe screenshot mode, English product copy, and updated Citrix documentation.

**Architecture:** Keep the current MVVM and isolated-child-process architecture. Add replay-state evaluation and privacy/filter state to `MainViewModel`, expose row-specific replay explanations from `ActivityRowViewModel`, and express the responsive layout and privacy presentation in XAML. Offline smoke data stays synthetic and bypasses productive history, while productive ESAPI discovery, DPAPI context storage, launch composition, and STA ownership remain unchanged.

**Tech Stack:** C# 7.x, WPF, .NET Framework 4.8 x64, custom executable test harness, PowerShell 5.1 release tooling, Git/GitHub CLI.

---

## File map

- `src/ESAPI.RunnerHub/ViewModels/ActivityRowViewModel.cs`: replay eligibility and user-facing reason for one history row.
- `src/ESAPI.RunnerHub/ViewModels/MainViewModel.cs`: command invalidation, filter reset, privacy display state, and replay-state evaluation.
- `src/ESAPI.RunnerHub/MainWindow.xaml`: two-column responsive catalogue, centered filter toolbar, privacy treatments, and flexible activity rows.
- `src/ESAPI.RunnerHub/MainWindow.xaml.cs`: isolated synthetic smoke catalogue, selected context, and synthetic activity.
- `src/ESAPI.ScriptHost/Program.cs`: English Script Host dialogs.
- `src/ESAPI.CitrixLauncher/Program.cs`: English launcher errors.
- `tests/ESAPI.RunnerHub.Tests/LaunchHistoryViewModelTests.cs`: replay notification and replay-reason regressions.
- `tests/ESAPI.RunnerHub.Tests/MainViewModelTests.cs`: privacy and filter-reset behavior.
- `tests/ESAPI.RunnerHub.Tests/ProjectShapeTests.cs`: responsive XAML, privacy binding, English copy, and documentation shape.
- `README.md`: Citrix rationale and UI screenshot.
- `docs/images/esapi-runner-hub-overview.png`: synthetic privacy-safe UI screenshot.
- `settings.example.ini`: English example catalogue copy.
- `dist/settings.ini`: translate only live names/descriptions/categories while preserving all paths and operational settings.
- `versionInfo.json`, `CHANGELOG.md`: v0.3.0 build 19 release metadata.
- `docs/FINAL_VERIFICATION.md`, `docs/RELEASE_VERIFICATION.md`: fresh verification evidence.

### Task 1: Establish a clean baseline

**Files:**
- Inspect: `ESAPI-Runner-Hub.sln`
- Inspect: `tests/ESAPI.RunnerHub.Tests/Program.cs`

- [ ] **Step 1: Verify repository scope**

Run:

```powershell
git status --short
git branch --show-current
```

Expected: branch `main`; no uncommitted product files.

- [ ] **Step 2: Build a non-publishing baseline**

Run:

```powershell
$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
& $msbuild .\ESAPI-Runner-Hub.sln /t:Rebuild /p:Configuration=Release /p:Platform=x64 /p:EsapiReferenceDirectory="..\_Assets" /p:OutputPath="$PWD\obj\baseline\"
```

Expected: exit code 0 and Eclipse 18 references resolved from `..\_Assets`.

- [ ] **Step 3: Run the current test harness**

Run:

```powershell
.\obj\baseline\ESAPI.RunnerHub.Tests.exe
```

Expected: 129 tests pass and 0 fail before the new regressions are added.

### Task 2: Repair replay state and command notification with TDD

**Files:**
- Modify: `tests/ESAPI.RunnerHub.Tests/LaunchHistoryViewModelTests.cs`
- Modify: `src/ESAPI.RunnerHub/ViewModels/ActivityRowViewModel.cs`
- Modify: `src/ESAPI.RunnerHub/ViewModels/MainViewModel.cs`

- [ ] **Step 1: Write failing replay regressions**

Register tests that assert:

```csharp
TestHarness.Test("replay command refreshes when asynchronous readiness arrives", RefreshesReplayCommand);
TestHarness.Test("replay rows explain every unavailable state", ExplainsReplayAvailability);
TestHarness.Test("running activity cannot be replayed until terminal", DisablesReplayWhileRunning);
```

`RefreshesReplayCommand` loads an exited `WithoutPatient` row, subscribes to `RunAgainCommand.CanExecuteChanged`, calls `UpdateApplicationReadiness`, and asserts that the event fired and `CanExecute(row)` is true. `ExplainsReplayAvailability` checks the exact strings `Application path is unavailable`, `Protected context is unavailable`, `Application was removed from the catalogue`, and `Ready to run again` through real loaded rows. `DisablesReplayWhileRunning` loads a persisted `Running` entry, marks its application ready, asserts the running reason, changes the entry to `Exited`, refreshes readiness, and asserts replay is enabled. This avoids a timing-dependent child-process test.

- [ ] **Step 2: Run the harness and verify RED**

Run the Release build to `obj\red-replay`, then:

```powershell
.\obj\red-replay\ESAPI.RunnerHub.Tests.exe
```

Expected: failures because replay command notification and row explanation do not exist.

- [ ] **Step 3: Add row replay state**

Add to `ActivityRowViewModel`:

```csharp
private bool protectedContextAvailable;
private string replayAvailabilityText = "Application path is unavailable";

public bool ProtectedContextAvailable { get { return protectedContextAvailable; } }
public string ReplayAvailabilityText { get { return replayAvailabilityText; } }

public void SetReplayAvailability(bool canReplay, string explanation)
{
    SetCanRunAgain(canReplay);
    if (replayAvailabilityText == explanation) return;
    replayAvailabilityText = explanation ?? string.Empty;
    RaiseOnUi(nameof(ReplayAvailabilityText));
}
```

Initialize protected-context availability from the constructor and provide a setter used after DPAPI failures.

- [ ] **Step 4: Centralize replay evaluation and command invalidation**

Keep the concrete `RelayCommand` in `MainViewModel` and expose it as `ICommand`. Add one evaluator with this precedence:

```csharp
if (card == null) return Disabled("Application was removed from the catalogue");
if (!card.IsReady) return Disabled("Application path is unavailable");
if (!row.ProtectedContextAvailable) return Disabled("Protected context is unavailable");
if (row.State == LaunchHistoryState.Starting || row.State == LaunchHistoryState.Running)
    return Disabled("The application is still running");
return Enabled("Ready to run again");
```

Call it after history loading, readiness changes, launch insertion, process start, terminal exit, failed start, and DPAPI-unprotect failure. After each affected batch call `runAgainCommand.RaiseCanExecuteChanged()`.

- [ ] **Step 5: Run the replay regressions and full harness GREEN**

Expected: new replay tests and all existing tests pass.

- [ ] **Step 6: Commit replay repair**

```powershell
git add src/ESAPI.RunnerHub/ViewModels/ActivityRowViewModel.cs src/ESAPI.RunnerHub/ViewModels/MainViewModel.cs tests/ESAPI.RunnerHub.Tests/LaunchHistoryViewModelTests.cs
git commit -m "fix: refresh protected activity replay state"
```

### Task 3: Add filter reset and privacy presentation state with TDD

**Files:**
- Modify: `tests/ESAPI.RunnerHub.Tests/MainViewModelTests.cs`
- Modify: `src/ESAPI.RunnerHub/ViewModels/MainViewModel.cs`

- [ ] **Step 1: Write failing view-model tests**

Register:

```csharp
TestHarness.Test("privacy display mode is explicit and temporary", TogglesPrivacyDisplay);
TestHarness.Test("catalogue filters reset together", ResetsCatalogueFilters);
```

The privacy test asserts `IsPrivacyBlurEnabled == false`, label `Privacy blur`, one command execution, then `true` and label `Show details`. The reset test activates search, category, and artifact filters, asserts `HasActiveFilters`, executes `ResetFiltersCommand`, and asserts empty search plus `All categories` and `All formats`.

- [ ] **Step 2: Run and verify RED**

Expected: compile/test failure because the new properties and commands are absent.

- [ ] **Step 3: Implement minimal state and commands**

Add:

```csharp
public bool IsPrivacyBlurEnabled { get { return isPrivacyBlurEnabled; } }
public string PrivacyActionLabel { get { return isPrivacyBlurEnabled ? "Show details" : "Privacy blur"; } }
public bool HasActiveFilters { get { return !string.IsNullOrWhiteSpace(applicationFilter) || selectedCategory != "All categories" || selectedArtifactFilter.Kind != ApplicationArtifactFilter.All; } }
public ICommand TogglePrivacyBlurCommand { get; private set; }
public ICommand ResetFiltersCommand { get; private set; }
```

The privacy command only changes presentation properties. The reset command assigns the three default filter values and refreshes `HasActiveFilters`; it does not touch patient or planning context.

- [ ] **Step 4: Run full harness GREEN**

Expected: all tests pass.

- [ ] **Step 5: Commit view-model controls**

```powershell
git add src/ESAPI.RunnerHub/ViewModels/MainViewModel.cs tests/ESAPI.RunnerHub.Tests/MainViewModelTests.cs
git commit -m "feat: add privacy and catalogue filter controls"
```

### Task 4: Build the compact responsive WPF layout with shape tests

**Files:**
- Modify: `tests/ESAPI.RunnerHub.Tests/ProjectShapeTests.cs`
- Modify: `src/ESAPI.RunnerHub/MainWindow.xaml`
- Modify: `src/ESAPI.RunnerHub/MainWindow.xaml.cs`

- [ ] **Step 1: Write failing XAML and smoke-fixture tests**

Add shape assertions for these stable markers:

```csharp
TestHarness.AssertContains(xaml, "x:Name=\"CatalogueFilterBar\"");
TestHarness.AssertContains(xaml, "HorizontalScrollBarVisibility=\"Disabled\"");
TestHarness.AssertContains(xaml, "Command=\"{Binding TogglePrivacyBlurCommand}\"");
TestHarness.AssertContains(xaml, "ReplayAvailabilityText");
TestHarness.AssertContains(xaml, "Width=\"318\"");
TestHarness.AssertFalse(xaml.Contains("Width=\"154\""));
TestHarness.AssertFalse(xaml.Contains("<GridView>"));
```

Also assert the smoke fixture uses four synthetic applications, bypasses productive history, and adds a synthetic terminal activity.

- [ ] **Step 2: Run and verify RED**

Expected: the old category rail, fixed `GridView`, and missing privacy markers fail the new checks.

- [ ] **Step 3: Replace the main-area layout**

Use two columns (`334` and `*`). Place search, category, format, and reset in `CatalogueFilterBar`, centered above a vertically scrolling/horizontally disabled `ItemsControl`. Use a centered `WrapPanel` and 318 px cards with wrapping compact paths.

- [ ] **Step 4: Add reusable privacy styles**

Create XAML styles with a `DataTrigger` bound to the window data context. When `IsPrivacyBlurEnabled` is true, apply a `BlurEffect`, reduce opacity, disable hit testing where necessary, suppress full-path/context tooltips, and replace patient-specific launch-button content with `Start with patient`.

- [ ] **Step 5: Replace fixed recent activity columns**

Render the activity header and items as matching proportional grids with star-sized columns. Bind action tooltip to `ReplayAvailabilityText` and rely on `RunAgainCommand.CanExecute`; do not add a second stale `IsEnabled` gate.

- [ ] **Step 6: Isolate and enrich offline smoke data**

Construct smoke mode with a `null` history store, four synthetic cards, one selected synthetic patient/context, and one exited synthetic activity. No local productive history or institutional path may enter the smoke UI.

- [ ] **Step 7: Build and run full harness GREEN**

Expected: no XAML compilation error and all tests pass.

- [ ] **Step 8: Commit compact UI**

```powershell
git add src/ESAPI.RunnerHub/MainWindow.xaml src/ESAPI.RunnerHub/MainWindow.xaml.cs tests/ESAPI.RunnerHub.Tests/ProjectShapeTests.cs
git commit -m "feat: redesign compact privacy-aware Runner UI"
```

### Task 5: Convert product copy and live catalogue metadata to English

**Files:**
- Modify: `tests/ESAPI.RunnerHub.Tests/ProjectShapeTests.cs`
- Modify: `tests/ESAPI.RunnerHub.Tests/ExampleSettingsTests.cs`
- Modify: `src/ESAPI.ScriptHost/Program.cs`
- Modify: `src/ESAPI.CitrixLauncher/Program.cs`
- Modify: `settings.example.ini`
- Modify in place, not packaged from source: `dist/settings.ini`

- [ ] **Step 1: Add failing English-copy checks**

Assert Script Host uses `The script ended without saving`, `Failure phase:`, and `Failure type:`. Assert Citrix launcher uses English error titles/messages. Assert example settings and productive live catalogue descriptions contain no known German product phrases while application paths remain byte-for-byte equal to a pre-edit path inventory.

- [ ] **Step 2: Run and verify RED**

Expected: current German Script Host and launcher strings fail.

- [ ] **Step 3: Translate source and example copy**

Translate only user-visible messages and descriptions. Keep identifiers, executable names, enum values, arguments, filenames, and paths unchanged.

- [ ] **Step 4: Translate live catalogue metadata safely**

Before editing, export every `[Application.*]` `Executable`, `Arguments`, and all `[Hub]` path values. Change only `Name`, `Category`, and `Description` values that are German. Re-read the file and assert the protected inventory is identical.

- [ ] **Step 5: Run full harness GREEN**

Expected: all tests pass and live path inventory comparison reports no change.

- [ ] **Step 6: Commit tracked English copy**

```powershell
git add src/ESAPI.ScriptHost/Program.cs src/ESAPI.CitrixLauncher/Program.cs settings.example.ini tests/ESAPI.RunnerHub.Tests/ProjectShapeTests.cs tests/ESAPI.RunnerHub.Tests/ExampleSettingsTests.cs
git commit -m "docs: standardize Runner product copy in English"
```

`dist/settings.ini` remains a protected live configuration change and is verified separately because it is not a release-source file.

### Task 6: Document Citrix value and create the real synthetic screenshot

**Files:**
- Modify: `README.md`
- Modify: `citrix/README-Citrix.md`
- Modify: `docs/CONTEXT_DEBUGGING.md`
- Create: `docs/images/esapi-runner-hub-overview.png`
- Modify: `tests/ESAPI.RunnerHub.Tests/ProjectShapeTests.cs`

- [ ] **Step 1: Add failing documentation assertions**

Assert README contains `Why a Citrix runner?`, `Published Application`, `ARIA toolbar`, `remote desktop`, `child process`, and the image path. Assert Citrix documentation describes the stable single publication and user-scoped CLI request workflow without `%**` forwarding claims.

- [ ] **Step 2: Run and verify RED**

Expected: missing rationale and screenshot fail.

- [ ] **Step 3: Write public English documentation**

Explain that without the Hub, standalone tools usually require separate Citrix publications, blue ARIA-toolbar links, or application-server remote desktop. Explain the stable one-publication model, context reuse, child isolation, and request-based debugging safety boundary.

- [ ] **Step 4: Build a screenshot candidate**

Build Release to a temporary output, start `ESAPI-Runner-Hub.exe --offline-ui-smoke`, activate `Privacy blur`, size the window to 1920 x 1080, and capture only the window to `docs/images/esapi-runner-hub-overview.png`.

- [ ] **Step 5: Visually inspect both supported sizes**

Inspect the 1920 x 1080 capture and a live 1080 x 680 window. Confirm centered filters, four cards when space permits, two cards at minimum width, readable wrapped paths, responsive activity, enabled replay, and no horizontal scrollbar.

- [ ] **Step 6: Verify screenshot privacy**

Confirm the screenshot contains only synthetic patient labels and no complete UNC/local institutional path. Privacy mode must visibly cover patient/context/path displays and revealing tooltips must be unavailable.

- [ ] **Step 7: Run documentation tests GREEN and commit**

```powershell
git add README.md citrix/README-Citrix.md docs/CONTEXT_DEBUGGING.md docs/images/esapi-runner-hub-overview.png tests/ESAPI.RunnerHub.Tests/ProjectShapeTests.cs
git commit -m "docs: explain Citrix workflow with privacy-safe UI"
```

### Task 7: Package, release, and update STR Hub visibility

**Files:**
- Modify: `versionInfo.json`
- Modify: `CHANGELOG.md`
- Modify: `docs/FINAL_VERIFICATION.md`
- Modify: `docs/RELEASE_VERIFICATION.md`
- Generated/updated: `dist/versions/ESAPI-Runner-Hub.v0.3.0.exe`
- Generated/updated: `dist/ESAPI-Runner-Hub-v0.3.0-win-x64.zip`
- Generated/updated: `dist/SHA256SUMS.txt`
- Update: RT-UKL-Hub InHouse rows 61 and 62

- [ ] **Step 1: Update release metadata**

Set version `0.3.0`, build `19`, date `2026-08-04`, English `lastChange`, and prepend a matching changelog entry describing replay repair, compact privacy UI, English copy, and Citrix documentation. Add the matching `CHANGELOG.md` section.

- [ ] **Step 2: Run full release pipeline**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\build-release.ps1
```

Expected: solution build succeeds, all unit tests pass, both Citrix launcher suites pass, vendor-free validation passes, deterministic ZIP is created, and immutable v0.3.0 binary is published.

- [ ] **Step 3: Run fresh direct verification**

Run the built test harness again, both launcher suites, `tools\validate-vendor-free.ps1`, JSON parsing, `git diff --check`, and SHA-256 verification. Record exact counts and hashes in both verification documents.

- [ ] **Step 4: Activate the verified Citrix binary**

Update `citrix/current.txt` to `ESAPI-Runner-Hub.v0.3.0.exe` only after Step 3 succeeds. Smoke-launch through `citrix/ESAPI-Runner-Hub.CitrixLauncher.exe` and verify its startup event in the protected central log.

- [ ] **Step 5: Commit and tag the release**

```powershell
git add versionInfo.json CHANGELOG.md docs/FINAL_VERIFICATION.md docs/RELEASE_VERIFICATION.md citrix/current.txt citrix/ESAPI-Runner-Hub.CitrixLauncher.exe
git commit -m "release: publish ESAPI Runner Hub v0.3.0"
git tag -a v0.3.0 -m "ESAPI Runner Hub v0.3.0"
```

- [ ] **Step 6: Push and publish GitHub assets**

Push `main` and `v0.3.0` to `origin` and `github`. Create the GitHub release with the ZIP and `SHA256SUMS.txt`, then query the release URL and attached asset names.

- [ ] **Step 7: Update and verify STR Hub entries**

Back up the RT-UKL-Hub `inhouse.db`, update rows 61 and 62 with the English Runner/Citrix workflow and screenshot where applicable, and query both rows back. Verify the Hub version resolver returns version `0.3.0`, build `19`, and the new changelog title. Do not add a duplicate manual DB changelog entry.

- [ ] **Step 8: Final requirements audit**

Re-read the design specification and confirm every acceptance criterion against test output, captured images, released files, Git tags/remotes, GitHub assets, live `current.txt`, live settings inventory, and Hub payload. Report any clinical-live validation that remains intentionally separate from software verification.
