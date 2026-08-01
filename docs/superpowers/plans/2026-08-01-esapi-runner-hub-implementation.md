# ESAPI Runner Hub Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build, verify, deploy, and publish a single-file Windows launcher with fast ESAPI patient search, configurable isolated application starts, a shared UNC deployment, and a dedicated InHouse Tools entry.

**Architecture:** A vendor-free .NET Framework 4.8 WPF process loads `PatientSummaries` through a reflection adapter, copies primitive patient data, and disposes ESAPI before launching applications. Every configured runner or standalone EXE starts as an independent child process; configuration comes from a portable INI model shared by the GUI editor and file parser.

**Tech Stack:** C# 7.3, WPF, .NET Framework 4.8 x64, PowerShell 5+, custom dependency-free console tests, MSBuild, Git, GitHub CLI, SQLite/InHouse Hub.

---

## File map

- `ESAPI-Runner-Hub.sln` — solution containing the launcher, test runner, child fixture, and synthetic VMS-shaped fixture assembly.
- `src/ESAPI.RunnerHub/ESAPI.RunnerHub.csproj` — single portable WPF executable.
- `src/ESAPI.RunnerHub/Configuration/*` — INI parsing, validation, path resolution, and atomic persistence.
- `src/ESAPI.RunnerHub/Patients/*` — detached patient records, local search, and reflection-only ESAPI directory loading.
- `src/ESAPI.RunnerHub/Launching/*` — command construction, non-blocking path checks, child launch, and exit tracking.
- `src/ESAPI.RunnerHub/Privacy/*` — privacy-safe technical logging and crash reporting.
- `src/ESAPI.RunnerHub/MainWindow.*` — approved catalogue/patient/process overview.
- `src/ESAPI.RunnerHub/SettingsWindow.*` — GUI editor for the same `settings.ini` model.
- `tests/ESAPI.RunnerHub.Tests/*` — dependency-free executable test suite.
- `tests/Fixtures/RunnerFixture/*` — controllable success/error/crash/argument-capture child EXE.
- `tests/Fixtures/FakeVmsApi/*` — synthetic assembly exposing `VMS.TPS.Common.Model.API.Application` and `PatientSummaries`.
- `tools/build-release.ps1` — clean test/build/package pipeline.
- `tools/validate-vendor-free.ps1` — assembly-reference and package-content guard.
- `settings.example.ini` — public synthetic/default configuration.
- `README.md`, `CHANGELOG.md`, `versionInfo.json`, `LICENSE` — public and InHouse release metadata.

### Task 1: Buildable x64 WPF solution

**Files:**
- Create: `ESAPI-Runner-Hub.sln`
- Create: `src/ESAPI.RunnerHub/ESAPI.RunnerHub.csproj`
- Create: `src/ESAPI.RunnerHub/App.xaml`
- Create: `src/ESAPI.RunnerHub/App.xaml.cs`
- Create: `src/ESAPI.RunnerHub/Properties/AssemblyInfo.cs`
- Create: `tests/ESAPI.RunnerHub.Tests/ESAPI.RunnerHub.Tests.csproj`
- Create: `tests/ESAPI.RunnerHub.Tests/Program.cs`

- [ ] **Step 1: Write a failing solution-shape test**

Create a console test runner whose first checks require x64, .NET Framework 4.8, WPF, and an executable output:

```csharp
Test("project targets net48 x64 WPF", () =>
{
    var project = File.ReadAllText(PathFromRoot("src/ESAPI.RunnerHub/ESAPI.RunnerHub.csproj"));
    AssertContains(project, "<TargetFrameworkVersion>v4.8</TargetFrameworkVersion>");
    AssertContains(project, "<PlatformTarget>x64</PlatformTarget>");
    AssertContains(project, "<OutputType>WinExe</OutputType>");
});
```

- [ ] **Step 2: Run the test and confirm it fails**

Run: `MSBuild.exe ESAPI-Runner-Hub.sln /t:Build /p:Configuration=Debug /p:Platform=x64`  
Expected: FAIL because the solution/projects do not exist yet.

- [ ] **Step 3: Add minimal old-style .NET Framework projects and STA WPF startup**

`App.xaml.cs` must select offline smoke mode without touching ESAPI:

```csharp
[STAThread]
public static void Main()
{
    var app = new App();
    app.InitializeComponent();
    app.Run(new MainWindow(Environment.GetCommandLineArgs()));
}
```

- [ ] **Step 4: Build and run the shape test**

Run the solution build and `tests/ESAPI.RunnerHub.Tests/bin/x64/Debug/ESAPI.RunnerHub.Tests.exe`.  
Expected: build succeeds and test runner prints `PASS project targets net48 x64 WPF`.

- [ ] **Step 5: Commit**

Commit exact solution/project/startup files with `git commit -m "build: add net48 x64 WPF solution"`.

### Task 2: INI configuration model

**Files:**
- Create: `src/ESAPI.RunnerHub/Configuration/HubConfiguration.cs`
- Create: `src/ESAPI.RunnerHub/Configuration/ApplicationDefinition.cs`
- Create: `src/ESAPI.RunnerHub/Configuration/IniConfigurationStore.cs`
- Create: `src/ESAPI.RunnerHub/Configuration/ConfigurationValidator.cs`
- Modify: `tests/ESAPI.RunnerHub.Tests/Program.cs`

- [ ] **Step 1: Write failing parser and validation tests**

Cover comments, hub values, application sections, relative paths, environment expansion, duplicate IDs, enum errors, required patient transport, and atomic round-trip:

```csharp
var config = IniConfigurationStore.Parse(sample, @"C:\portable\settings.ini");
AssertEqual(10, config.Hub.SearchMaxResults);
AssertEqual(PatientMode.Optional, config.Applications[0].PatientMode);
AssertEqual(@"C:\portable\apps\ClearPlan.exe", config.Applications[0].ResolvedExecutable);
AssertFalse(config.Serialize().Contains("TEST-001"));
```

- [ ] **Step 2: Run tests and confirm missing-type failures**

Run test EXE; expected FAIL mentioning `IniConfigurationStore`.

- [ ] **Step 3: Implement the minimal typed INI model**

Use the exact enums and fields:

```csharp
public enum PatientMode { None, Optional, Required }
public enum PatientTransport { None, Argument, Environment }

public sealed class ApplicationDefinition
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Category { get; set; }
    public string Description { get; set; }
    public string Executable { get; set; }
    public string WorkingDirectory { get; set; }
    public string Arguments { get; set; }
    public PatientMode PatientMode { get; set; }
    public PatientTransport PatientTransport { get; set; }
    public string PatientArgumentTemplate { get; set; }
    public string PatientEnvironmentKey { get; set; }
    public bool Enabled { get; set; }
    public int SortOrder { get; set; }
}
```

Unknown keys are preserved only in their section for safe GUI round-trips; patient values never enter this model.

- [ ] **Step 4: Run all configuration tests**

Expected: parse/validate/save tests pass; a read-only save test leaves the original unchanged.

- [ ] **Step 5: Commit**

Commit with `git commit -m "feat: add portable INI configuration"`.

### Task 3: Detached patient search

**Files:**
- Create: `src/ESAPI.RunnerHub/Patients/PatientRecord.cs`
- Create: `src/ESAPI.RunnerHub/Patients/PatientSearchIndex.cs`
- Modify: `tests/ESAPI.RunnerHub.Tests/Program.cs`

- [ ] **Step 1: Write failing search tests**

Use synthetic records and require tokenized case-insensitive matching, exact-ID priority, multi-field matching, deduplication, and result limits:

```csharp
var hits = new PatientSearchIndex(records).Find("muster 001", 10);
AssertEqual("TEST-001", hits.Single().Id);
AssertEqual("TEST-001", index.Find("test-001", 10).First().Id);
AssertEqual(2, index.Find("anna", 2).Count);
```

- [ ] **Step 2: Run tests and confirm failure**

Expected: FAIL because `PatientSearchIndex` is missing.

- [ ] **Step 3: Implement normalized immutable records and deterministic ranking**

Normalize only letters and digits, split tokens, require every token in ID/last/first, then order exact ID, ID prefix, name prefix, and stable source order.

- [ ] **Step 4: Run all tests**

Expected: all search and configuration tests pass.

- [ ] **Step 5: Commit**

Commit with `git commit -m "feat: add fast detached patient search"`.

### Task 4: Reflection-only ESAPI patient directory loader

**Files:**
- Create: `src/ESAPI.RunnerHub/Patients/PatientDirectoryLoadResult.cs`
- Create: `src/ESAPI.RunnerHub/Patients/ReflectionEsapiPatientDirectoryLoader.cs`
- Create: `tests/Fixtures/FakeVmsApi/FakeVmsApi.csproj`
- Create: `tests/Fixtures/FakeVmsApi/Application.cs`
- Modify: `tests/ESAPI.RunnerHub.Tests/Program.cs`

- [ ] **Step 1: Write failing loader tests**

Require load/copy/dispose, missing assembly offline state, and no compile-time VMS references:

```csharp
var result = loader.Load(fakeApiPath, fakeTypesPath);
AssertTrue(result.IsAvailable);
AssertEqual("TEST-001", result.Patients[0].Id);
AssertTrue(File.Exists(fakeDisposeMarker));
```

- [ ] **Step 2: Run tests and confirm failure**

Expected: FAIL because the reflection loader is missing.

- [ ] **Step 3: Implement STA-bound reflection loading**

The loader must resolve local assemblies, invoke `Application.CreateApplication`, enumerate summaries into strings, dispose `Application` in `finally`, remove the temporary resolver, and return a privacy-safe technical error category.

- [ ] **Step 4: Run tests and inspect launcher references**

Run tests and `ildasm /text /nobar ESAPI-Runner-Hub.exe | findstr /i "VMS.TPS EsapiEssentials"`.  
Expected: tests pass and `findstr` returns no matches.

- [ ] **Step 5: Commit**

Commit with `git commit -m "feat: load ESAPI patient directory by reflection"`.

### Task 5: Argument composition, path readiness, and isolated process launching

**Files:**
- Create: `src/ESAPI.RunnerHub/Launching/LaunchRequest.cs`
- Create: `src/ESAPI.RunnerHub/Launching/ArgumentComposer.cs`
- Create: `src/ESAPI.RunnerHub/Launching/PathProbe.cs`
- Create: `src/ESAPI.RunnerHub/Launching/ChildProcessLauncher.cs`
- Create: `src/ESAPI.RunnerHub/Launching/RunningProcessInfo.cs`
- Create: `tests/Fixtures/RunnerFixture/RunnerFixture.csproj`
- Create: `tests/Fixtures/RunnerFixture/Program.cs`
- Modify: `tests/ESAPI.RunnerHub.Tests/Program.cs`

- [ ] **Step 1: Write failing launch tests**

Test None/Optional/Required modes, argument and environment transfer, unsafe patient IDs, missing executable, timeout, successful exit, exit 7, and deliberate child crash:

```csharp
var request = ArgumentComposer.Compose(app, patient, true);
AssertContains(request.Arguments, "--patient-id TEST-001");
AssertFalse(request.LogSummary.Contains("TEST-001"));
```

- [ ] **Step 2: Run tests and confirm failure**

Expected: FAIL because launch components are missing.

- [ ] **Step 3: Implement minimal safe launching**

Use `UseShellExecute=false`, never log expanded arguments, validate transferred IDs with `^[A-Za-z0-9._-]+$`, set only the configured child environment key, and enable exit events. Path probes run on background tasks and resolve as `Ready`, `Missing`, or `Unavailable` after the configured timeout.

- [ ] **Step 4: Run fixture integration tests**

Expected: the Hub test process survives fixture exit 7 and `Environment.FailFast`; another fixture can start afterward.

- [ ] **Step 5: Commit**

Commit with `git commit -m "feat: launch applications in isolated processes"`.

### Task 6: Main WPF catalogue and patient workflow

**Files:**
- Create: `src/ESAPI.RunnerHub/MainWindow.xaml`
- Create: `src/ESAPI.RunnerHub/MainWindow.xaml.cs`
- Create: `src/ESAPI.RunnerHub/ViewModels/MainViewModel.cs`
- Create: `src/ESAPI.RunnerHub/ViewModels/ApplicationCardViewModel.cs`
- Create: `src/ESAPI.RunnerHub/ViewModels/ProcessRowViewModel.cs`
- Create: `src/ESAPI.RunnerHub/Infrastructure/ObservableObject.cs`
- Modify: `tests/ESAPI.RunnerHub.Tests/Program.cs`

- [ ] **Step 1: Write failing view-model tests**

Require selected-patient retention, clear/change, optional dual actions, required disablement, category/filter results, ESAPI offline status, and process-row updates.

- [ ] **Step 2: Run tests and confirm failure**

Expected: FAIL because view models are missing.

- [ ] **Step 3: Implement approved single-window layout**

Build the header/status, patient search/suggestions/selected card, category sidebar, application cards, and running-process footer shown in the approved mockup. `--offline-ui-smoke` injects synthetic patients/apps and never calls ESAPI.

- [ ] **Step 4: Run tests and UI smoke**

Run tests, then start `ESAPI-Runner-Hub.exe --offline-ui-smoke --settings settings.example.ini`.  
Expected: synthetic GUI opens, patient filter works, and closing returns exit 0.

- [ ] **Step 5: Commit**

Commit with `git commit -m "feat: add runner hub main window"`.

### Task 7: GUI settings editor

**Files:**
- Create: `src/ESAPI.RunnerHub/SettingsWindow.xaml`
- Create: `src/ESAPI.RunnerHub/SettingsWindow.xaml.cs`
- Create: `src/ESAPI.RunnerHub/ViewModels/SettingsViewModel.cs`
- Modify: `src/ESAPI.RunnerHub/MainWindow.xaml.cs`
- Modify: `tests/ESAPI.RunnerHub.Tests/Program.cs`

- [ ] **Step 1: Write failing settings-view-model tests**

Require add, edit, delete, browse-result assignment, validation summary, exact settings path display, atomic save, and reload.

- [ ] **Step 2: Run tests and confirm failure**

Expected: FAIL because settings UI model is missing.

- [ ] **Step 3: Implement settings window**

Provide Hub paths and values at the top, application list at left, typed fields at right, EXE/directory/VMS assembly browse buttons, Add/Delete/Save/Cancel, and immediate validation. Save and reload the catalogue only after a successful atomic write.

- [ ] **Step 4: Run tests and UI smoke**

Expected: save/reload tests pass; UI smoke can add a synthetic app without writing patient data.

- [ ] **Step 5: Commit**

Commit with `git commit -m "feat: edit runner settings in the GUI"`.

### Task 8: Privacy-safe diagnostics and crash resilience

**Files:**
- Create: `src/ESAPI.RunnerHub/Privacy/TechnicalLog.cs`
- Create: `src/ESAPI.RunnerHub/Privacy/CrashReporter.cs`
- Modify: `src/ESAPI.RunnerHub/App.xaml.cs`
- Modify: `src/ESAPI.RunnerHub/MainWindow.xaml.cs`
- Modify: `tests/ESAPI.RunnerHub.Tests/Program.cs`

- [ ] **Step 1: Write failing redaction tests**

Send a synthetic patient ID/name through selection and launch errors, then assert neither appears in the log/crash report. Require network and ESAPI errors to remain visible by category.

- [ ] **Step 2: Run tests and confirm failure**

Expected: FAIL because technical logging is missing.

- [ ] **Step 3: Implement diagnostics boundaries**

Log UTC timestamp, level, event code, app configuration ID, exception type, and sanitized message. Never log query text, selected-patient fields, expanded arguments, environment values, child stdout/stderr, or full UNC contents.

- [ ] **Step 4: Run privacy and crash tests**

Expected: all tests pass and synthetic identifiers are absent from every generated diagnostic file.

- [ ] **Step 5: Commit**

Commit with `git commit -m "feat: add privacy-safe diagnostics"`.

### Task 9: Public documentation and release metadata

**Files:**
- Create: `README.md`
- Create: `LICENSE`
- Create: `CHANGELOG.md`
- Create: `versionInfo.json`
- Create: `settings.example.ini`
- Modify: `tests/ESAPI.RunnerHub.Tests/Program.cs`

- [ ] **Step 1: Write failing metadata/privacy tests**

Require `version=0.1.0`, `build=1`, matching changelog, MIT license, no VMS binaries, no clinical patient data, and no local-only user paths.

- [ ] **Step 2: Run tests and confirm failure**

Expected: FAIL because release files are missing.

- [ ] **Step 3: Write complete documentation and synthetic example config**

Document portable use, ESAPI assembly settings, application modes, argument/environment patient transfer, offline mode, crash isolation, privacy, Citrix, known v1 plan-search limitation, build/test, shared deployment, and public release.

- [ ] **Step 4: Run all tests**

Expected: metadata and privacy tests pass.

- [ ] **Step 5: Commit**

Commit with `git commit -m "docs: prepare initial public release"`.

### Task 10: Deterministic build and vendor-free package

**Files:**
- Create: `tools/validate-vendor-free.ps1`
- Create: `tools/build-release.ps1`
- Create: `tools/create-deterministic-zip.ps1`
- Modify: `.gitignore`

- [ ] **Step 1: Write failing package validation**

Run validator before `dist` exists; expected FAIL with `Release directory missing`.

- [ ] **Step 2: Implement clean build/test/package pipeline**

Resolve MSBuild, clean/build x64 Release, run tests, copy the EXE/docs/example INI, scan assembly references and file names, create fixed-timestamp ZIP, and write SHA-256 manifest.

- [ ] **Step 3: Run the pipeline twice**

Run `powershell -ExecutionPolicy Bypass -File tools/build-release.ps1` twice.  
Expected: all tests pass and both ZIP SHA-256 values match.

- [ ] **Step 4: Inspect package contents**

Expected: exactly launcher EXE, example INI, README, LICENSE, CHANGELOG, versionInfo, and SHA manifest; no symbols or vendor DLLs.

- [ ] **Step 5: Commit**

Commit with `git commit -m "build: add deterministic vendor-free release"`.

### Task 11: Internal Git and shared UNC deployment

**Files:**
- Create outside repo: `\\medizin.uni-leipzig.de\data\Archiv\STR\STR-Physik\11. Scripting\GitUKL\ESAPI-Runner-Hub.git`
- Create checkout: `\\medizin.uni-leipzig.de\data\Archiv\STR\STR-Physik\11. Scripting\ESAPI-MG\ESAPI-Runner-Hub`
- Create deployment config: shared `settings.ini`

- [ ] **Step 1: Confirm targets do not already contain unrelated data**

Resolve and list both exact target paths. Abort rather than overwrite if a conflicting repository exists.

- [ ] **Step 2: Create and push internal bare remote**

Initialize the bare GitUKL repository, add `local-backup`, push `main`, and verify `git ls-remote` equals local HEAD.

- [ ] **Step 3: Create shared checkout and build there**

Clone `local-backup`, run the release pipeline, and verify the directly runnable `dist/ESAPI-Runner-Hub.exe` plus root `versionInfo.json`.

- [ ] **Step 4: Write internal `settings.ini`**

Configure existing verified VMS assemblies and runner/standalone paths for ClearPlan, PlanCheck, and eDoc-Uploader. Validate every path without recursively scanning the shares.

- [ ] **Step 5: Commit no local settings**

Verify shared `settings.ini` remains ignored and repository status is clean.

### Task 12: Public GitHub repository and v0.1.0 release

**Files:**
- Public repository: `Kiragroh/ESAPI-Runner-Hub`
- Release assets: `ESAPI-Runner-Hub-v0.1.0-win-x64.zip`, `SHA256SUMS.txt`

- [ ] **Step 1: Create public repository and push main**

Use GitHub CLI, preserve `local-backup`, set `origin`, push, and verify public visibility and remote HEAD.

- [ ] **Step 2: Tag the verified commit**

Create annotated tag `v0.1.0`, push it, and verify tag commit equals the packaged source commit.

- [ ] **Step 3: Create GitHub release**

Publish the ZIP and SHA manifest with concise installation and vendor-dependency notes.

- [ ] **Step 4: Verify from a clean unauthenticated clone/download**

Clone HTTPS into a temporary directory, rebuild, download the release asset, compare SHA-256, and inspect the ZIP for forbidden files/references.

- [ ] **Step 5: Record verification**

Add `docs/RELEASE_VERIFICATION.md` with commands, commit/tag alignment, asset names, and hashes; publish a follow-up build only if this changes release content.

### Task 13: Dedicated InHouse Tools entry

**Files:**
- Modify outside repo: `RT-UKL-Hub/web/backend/inhouse.db`
- Create backup: `RT-UKL-Hub/Doku/inhouse_backup_before_add_esapi_runner_hub_<timestamp>.db`

- [ ] **Step 1: Re-query schema and duplicates**

Require no existing `ESAPI Runner Hub` name/path/URL row and leave interface entry ID 59 unchanged.

- [ ] **Step 2: Back up and insert parameterized row**

Insert label `Eclipse`, author `MG`, shared EXE path, public URL, `cockpit_managed=0`, `status=active`, responsible `grohmanmax,schaeferse`, and a complete privacy-safe README.

- [ ] **Step 3: Read row back and verify assets/version resolution**

Check exact stored fields, README length/content, path existence, Git root, `versionInfo.json`, version `0.1.0`, and build `1` using the Hub backend resolver or authenticated endpoint.

- [ ] **Step 4: Verify the InHouse UI/API**

Confirm the separate card is visible and opens the shared path; note cache delay if the list still carries prior data.

- [ ] **Step 5: Leave unrelated Hub changes untouched**

Show that only the ignored/live database and its dedicated backup changed as part of registration; do not stage or commit the Hub's unrelated dirty worktree.

### Task 14: Final end-to-end verification

**Files:**
- Create: `docs/FINAL_VERIFICATION.md`

- [ ] **Step 1: Run clean tests and release build**

Require zero failed tests and a clean local Git status.

- [ ] **Step 2: Run offline GUI and crash-isolation smoke tests**

Use synthetic patients, launch success/error/crash fixtures, and verify another child launches afterward.

- [ ] **Step 3: Verify all Git endpoints**

Compare local `main`, `origin/main`, `local-backup/main`, public tag, and GitHub release source commit.

- [ ] **Step 4: Verify deployment and InHouse row**

Check shared EXE/config/version metadata, row ID/path/URL/status/responsible, and Hub version payload.

- [ ] **Step 5: Document remaining clinical gate**

State whether live Eclipse patient search was executed on this workstation. If it was unavailable, provide the exact workstation validation command and do not label the project clinically validated.

- [ ] **Step 6: Commit and publish final verification**

Commit the verification document, rebuild/re-release if public asset contents changed, and re-verify hashes and tag alignment.

