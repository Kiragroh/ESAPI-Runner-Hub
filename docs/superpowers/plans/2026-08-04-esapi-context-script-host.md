# ESAPI Context Script Host Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an isolated Eclipse 18 context-script host so the Hub can select patient, plan/plan sum, or standalone structure set once and start configured `*.esapi.dll` and `*.cs` tools safely.

**Architecture:** The WPF Hub loads detached context descriptors on an ESAPI STA thread and transports selected identifiers in a process-local environment payload. A separate x64 .NET Framework 4.8 WinExe resolves the context in its own ESAPI application, invokes either an EsapiEssentials script or the strictly version-gated Eclipse 18 `ScriptContext` adapter, and owns the save/discard decision.

**Tech Stack:** C# 7.3, WPF, .NET Framework 4.8, reflection-based ESAPI integration, `DataContractJsonSerializer`, CodeDOM C# compilation, custom executable test harness, PowerShell release validation.

---

## File map

- Modify `src/ESAPI.RunnerHub/Configuration/ApplicationDefinition.cs`: new launch, engine, context, scope, and write settings.
- Modify `src/ESAPI.RunnerHub/Configuration/IniConfigurationStore.cs`: INI parse/serialize for the new settings.
- Modify `src/ESAPI.RunnerHub/Configuration/ConfigurationValidator.cs`: fail-closed configuration combinations and extensions.
- Create `src/ESAPI.RunnerHub/Context/ContextDescriptor.cs`: detached course, plan, plan-sum, structure-set descriptors.
- Create `src/ESAPI.RunnerHub/Context/ContextSelection.cs`: active and scope identifiers plus requirement validation.
- Create `src/ESAPI.RunnerHub/Esapi/ReflectionContextDirectoryLoader.cs`: short-lived reflection-based patient context enumeration.
- Create `src/ESAPI.RunnerHub/Launching/ContextLaunchPayload.cs`: shared serialized host contract without command-line PHI.
- Create `src/ESAPI.RunnerHub/Launching/ContextScriptRequestComposer.cs`: host request and environment payload creation.
- Modify `src/ESAPI.RunnerHub/ViewModels/MainViewModel.cs`: context loading/selection and direct-host launch.
- Modify `src/ESAPI.RunnerHub/ViewModels/ApplicationCardViewModel.cs`: context readiness, direct launch labels, and warning state.
- Modify `src/ESAPI.RunnerHub/MainWindow.xaml` and `.xaml.cs`: context selector and asynchronous context load.
- Modify `src/ESAPI.RunnerHub/SettingsWindow.xaml` and `ViewModels/SettingsViewModel.cs`: editable context-script settings.
- Create `src/ESAPI.ScriptHost/ESAPI.ScriptHost.csproj`: isolated x64 WinExe.
- Create `src/ESAPI.ScriptHost/Program.cs`: STA entry point and privacy-safe exit handling.
- Create `src/ESAPI.ScriptHost/Host/EsapiSession.cs`: load, open, resolve, save/close, and dispose ESAPI.
- Create `src/ESAPI.ScriptHost/Host/ContextResolver.cs`: exact object resolution from selected identifiers.
- Create `src/ESAPI.ScriptHost/Host/EsapiEssentialsInvoker.cs`: public `PluginScriptContext` population and invocation.
- Create `src/ESAPI.ScriptHost/Host/Eclipse18ScriptContextAdapter.cs`: strict `ScriptContext` field adapter.
- Create `src/ESAPI.ScriptHost/Host/EclipseScriptInvoker.cs`: classic `Execute` invocation and write metadata detection.
- Create `src/ESAPI.ScriptHost/Host/SourceScriptCompiler.cs`: local content-addressed CodeDOM cache.
- Create `src/ESAPI.ScriptHost/Host/SaveDecision.cs`: deterministic save/discard state machine.
- Extend `tests/FakeVms.Api/Application.cs`: synthetic course/plan/plan-sum/structure-set graph and save counters.
- Add focused tests under `tests/ESAPI.RunnerHub.Tests/` and register them in `Program.cs`/the test project.
- Modify the solution, project files, build/release scripts, settings examples, README, changelog, version metadata, and Citrix pointer.

### Task 1: Configuration contract

**Files:**
- Modify: `src/ESAPI.RunnerHub/Configuration/ApplicationDefinition.cs`
- Modify: `src/ESAPI.RunnerHub/Configuration/IniConfigurationStore.cs`
- Modify: `src/ESAPI.RunnerHub/Configuration/ConfigurationValidator.cs`
- Test: `tests/ESAPI.RunnerHub.Tests/ContextConfigurationTests.cs`

- [ ] **Step 1: Write failing configuration tests**

```csharp
var app = IniConfigurationStore.ParseText(@"[Application.direct]
Name=Direct
Executable=Tool.esapi.dll
LaunchKind=EsapiContextScript
ScriptEngine=Eclipse
ContextRequirement=PlanOrStructureSet
ScopeMode=Multiple
WriteMode=ConfirmSave
EntryType=VMS.TPS.Script
ExtraReferences=Helper.dll", @"C:\portable\settings.ini").Applications.Single();
TestHarness.AssertEqual(LaunchKind.EsapiContextScript, app.LaunchKind);
TestHarness.AssertEqual(ScriptEngine.Eclipse, app.ScriptEngine);
TestHarness.AssertContains(IniConfigurationStore.Serialize(configuration), "WriteMode=ConfirmSave");
```

Also assert that direct scripts reject `.exe`, executable entries reject `.cs`, context scripts reject `PatientTransport`, `ContextRequirement=None` rejects non-`None` scope, and `ConfirmSave` rejects an Eclipse reference-only entry.

- [ ] **Step 2: Run the test host and observe missing enum/property failures**

Run: `MSBuild.exe ESAPI-Runner-Hub.sln /t:Build /p:Configuration=Debug /p:Platform=x64 /p:EsapiReferenceDirectory=..\_Assets`

Expected: compilation fails because the direct-host configuration types do not exist.

- [ ] **Step 3: Add the explicit configuration types and defaults**

```csharp
public enum LaunchKind { Executable, EclipsePlugin, EsapiContextScript }
public enum ScriptEngine { Auto, EsapiEssentials, Eclipse }
public enum ContextRequirement { None, Patient, StructureSet, Plan, PlanningItem, PlanOrStructureSet }
public enum ScopeMode { None, Single, Multiple }
public enum WriteMode { ReadOnly, ConfirmSave }

public ScriptEngine ScriptEngine { get; set; }
public ContextRequirement ContextRequirement { get; set; }
public ScopeMode ScopeMode { get; set; }
public WriteMode WriteMode { get; set; }
public string EntryType { get; set; }
public string ExtraReferences { get; set; }
```

Defaults are `Auto`, `None`, `None`, and `ReadOnly`. Parse and serialize each value by `Enum.TryParse(..., true, out value)`. Direct context scripts accept only `.cs`, `.dll`, or `.esapi.dll`, force `PatientTransport=None`, and require a patient unless `ContextRequirement=None`.

- [ ] **Step 4: Run all tests and commit**

Run the Release test executable; expected result is all tests passing.

Commit: `feat: configure direct ESAPI context scripts`

### Task 2: Detached context catalogue and validation

**Files:**
- Create: `src/ESAPI.RunnerHub/Context/ContextDescriptor.cs`
- Create: `src/ESAPI.RunnerHub/Context/ContextSelection.cs`
- Create: `src/ESAPI.RunnerHub/Esapi/ReflectionContextDirectoryLoader.cs`
- Modify: `tests/FakeVms.Api/Application.cs`
- Test: `tests/ESAPI.RunnerHub.Tests/ContextDirectoryTests.cs`
- Test: `tests/ESAPI.RunnerHub.Tests/ContextSelectionTests.cs`

- [ ] **Step 1: Define failing synthetic graph tests**

```csharp
var result = loader.Load(api, types, "SYN-1001");
TestHarness.AssertEqual("C1", result.Courses.Single().Id);
TestHarness.AssertEqual("P1", result.Plans.Single().Id);
TestHarness.AssertEqual("SS1", result.Plans.Single().StructureSetId);
TestHarness.AssertTrue(result.StructureSets.Any(x => x.Id == "SS-ONLY"));
```

Add requirement cases proving that plan auto-selects course/structure set, standalone structure set is valid, plan sum is a planning item, and ambiguous IDs are invalid.

- [ ] **Step 2: Run and verify the tests fail because the context types are absent**

- [ ] **Step 3: Implement immutable detached descriptors and requirement validation**

```csharp
public sealed class PlanDescriptor {
    public string Id { get; set; }
    public string CourseId { get; set; }
    public string StructureSetId { get; set; }
    public string ImageId { get; set; }
    public string Kind { get; set; }
    public string Display { get { return Id + " · " + Kind; } }
}

public sealed class ContextSelection {
    public string PatientId { get; set; }
    public string CourseId { get; set; }
    public string PlanId { get; set; }
    public string PlanSumId { get; set; }
    public string StructureSetId { get; set; }
    public IList<string> PlanIdsInScope { get; set; }
    public IList<string> PlanSumIdsInScope { get; set; }
    public string MissingFor(ContextRequirement requirement) {
        if (requirement == ContextRequirement.None) return string.Empty;
        if (string.IsNullOrWhiteSpace(PatientId)) return "Patient required";
        if (requirement == ContextRequirement.Patient) return string.Empty;
        if (requirement == ContextRequirement.StructureSet)
            return string.IsNullOrWhiteSpace(StructureSetId) ? "Structure set required" : string.Empty;
        if (requirement == ContextRequirement.Plan)
            return string.IsNullOrWhiteSpace(PlanId) ? "Plan required" : string.Empty;
        if (requirement == ContextRequirement.PlanningItem)
            return string.IsNullOrWhiteSpace(PlanId) && string.IsNullOrWhiteSpace(PlanSumId)
                ? "Plan or plan sum required" : string.Empty;
        return string.IsNullOrWhiteSpace(PlanId) && string.IsNullOrWhiteSpace(PlanSumId) &&
               string.IsNullOrWhiteSpace(StructureSetId)
            ? "Plan, plan sum, or structure set required" : string.Empty;
    }
}
```

The loader locates the configured Eclipse assemblies, creates an application, calls `OpenPatientById`, copies identifiers from `Courses`, `PlanSetups`, `PlanSums`, `StructureSets`, and `Image`, calls `ClosePatient`, disposes, and removes its assembly resolver in `finally`.

- [ ] **Step 4: Run all tests and commit**

Commit: `feat: load detached ESAPI planning context`

### Task 3: Private environment launch protocol

**Files:**
- Create: `src/ESAPI.RunnerHub/Launching/ContextLaunchPayload.cs`
- Create: `src/ESAPI.RunnerHub/Launching/ContextScriptRequestComposer.cs`
- Modify: `src/ESAPI.RunnerHub/Launching/LaunchRequest.cs`
- Test: `tests/ESAPI.RunnerHub.Tests/ContextLaunchProtocolTests.cs`

- [ ] **Step 1: Write failing round-trip and privacy tests**

```csharp
var request = ContextScriptRequestComposer.Compose(app, patient, selection, hub, hostPath);
TestHarness.AssertEqual(string.Empty, request.Arguments);
TestHarness.AssertFalse(request.LogSummary.Contains(patient.Id));
var payload = ContextLaunchPayload.Decode(request.EnvironmentVariables[ContextLaunchPayload.EnvironmentKey]);
TestHarness.AssertEqual(patient.Id, payload.PatientId);
TestHarness.AssertEqual("P1", payload.PlanId);
```

- [ ] **Step 2: Run and verify missing protocol failures**

- [ ] **Step 3: Implement a DataContract JSON payload**

The payload includes a random launch token, tool ID, script/API/Types paths, engine, entry type, write mode, patient/course/plan/plan-sum/structure-set identifiers, and selected scopes. `Encode` writes UTF-8 JSON to base64; `Decode` validates the token and size. The process command line stays empty and the log summary is `start app=<id> context=yes transport=environment`.

- [ ] **Step 4: Run all tests and commit**

Commit: `feat: transport ESAPI context privately`

### Task 4: Isolated script host core

**Files:**
- Create: `src/ESAPI.ScriptHost/ESAPI.ScriptHost.csproj`
- Create: `src/ESAPI.ScriptHost/Program.cs`
- Create: `src/ESAPI.ScriptHost/Host/EsapiSession.cs`
- Create: `src/ESAPI.ScriptHost/Host/ContextResolver.cs`
- Create: `src/ESAPI.ScriptHost/Host/SaveDecision.cs`
- Modify: `ESAPI-Runner-Hub.sln`
- Test: `tests/ESAPI.RunnerHub.Tests/ScriptHostCoreTests.cs`

- [ ] **Step 1: Write failing exact-resolution and save-state tests against FakeVms.Api**

```csharp
TestHarness.AssertThrows<InvalidOperationException>(() => resolver.ResolvePlan("duplicate"));
TestHarness.AssertEqual(SaveAction.SaveOnce, SaveDecision.AfterSuccess(WriteMode.ConfirmSave, UserChoice.Save));
TestHarness.AssertEqual(SaveAction.CloseWithoutSave, SaveDecision.AfterFailure());
```

- [ ] **Step 2: Run and verify missing host project/types**

- [ ] **Step 3: Implement the STA host lifecycle**

`Program.Main` reads and removes `ESAPI_RUNNER_CONTEXT`, decodes it, constructs `EsapiSession`, resolves the exact objects, invokes the selected engine, applies the save state machine, closes the patient, and returns stable exit codes. All exception dialogs and logs contain only a fixed category and exception type.

`EsapiSession` registers resolution for the configured API/Types and script-local dependencies before `Assembly.LoadFrom`, selects the shortest public static `CreateApplication` overload, calls `OpenPatientById`, exposes `CurrentUser`, calls `SaveModifications` at most once, calls `ClosePatient`, and disposes in `finally`.

- [ ] **Step 4: Run all tests and commit**

Commit: `feat: add isolated ESAPI script host`

### Task 5: EsapiEssentials and Eclipse 18 invocation

**Files:**
- Create: `src/ESAPI.ScriptHost/Host/EsapiEssentialsInvoker.cs`
- Create: `src/ESAPI.ScriptHost/Host/Eclipse18ScriptContextAdapter.cs`
- Create: `src/ESAPI.ScriptHost/Host/EclipseScriptInvoker.cs`
- Test: `tests/ESAPI.RunnerHub.Tests/ScriptInvocationTests.cs`

- [ ] **Step 1: Write failing adapter-shape and synthetic invocation tests**

Tests assert one compatible entry type, public EsapiEssentials property assignment, exact Eclipse version `1.0.600.194`, required private fields `m_user`, `m_course`, `m_image`, `m_patient`, `m_planSetup`, `m_planSetups`, `m_planSums`, `m_planSum`, `m_structureSet`, and rejection of a mismatched assembly shape.

- [ ] **Step 2: Run and verify missing invoker failures**

- [ ] **Step 3: Implement both invokers without retaining objects**

The EsapiEssentials invoker loads the script-local `EsapiEssentials.dll`, creates `PluginScriptContext`, assigns every public context/scope property, then invokes `Run` or the supported window form. The Eclipse invoker constructs `ScriptContext(object, object, object, string)`, verifies and sets only the known Eclipse 18 fields, finds `Execute(ScriptContext[, Window])`, and invokes on the host STA thread.

Read `ESAPIScriptAttribute.IsWriteable` via `CustomAttributeData`. `ConfirmSave` requires explicit writable metadata; `ReadOnly` rejects writable metadata.

- [ ] **Step 4: Run all tests and commit**

Commit: `feat: invoke essentials and Eclipse 18 scripts`

### Task 6: Configured C# source compilation

**Files:**
- Create: `src/ESAPI.ScriptHost/Host/SourceScriptCompiler.cs`
- Test: `tests/ESAPI.RunnerHub.Tests/SourceScriptCompilerTests.cs`

- [ ] **Step 1: Write failing cache and diagnostic tests**

The first compile creates `%LOCALAPPDATA%\ESAPI Runner Hub\ScriptCache\<sha256>.esapi.dll`; the same inputs reuse it; changing source or a referenced assembly version produces a different hash; compile errors return sanitized line/error-code diagnostics without source content.

- [ ] **Step 2: Run and verify the compiler is absent**

- [ ] **Step 3: Implement CodeDOM compilation**

Use `CSharpCodeProvider` with `/platform:x64`, `GenerateExecutable=false`, `GenerateInMemory=false`, configured Eclipse API/Types references, framework WPF references, and normalized existing `ExtraReferences`. Reject unconfigured extensions and missing references before compilation.

- [ ] **Step 4: Run all tests and commit**

Commit: `feat: compile configured ESAPI source scripts`

### Task 7: Hub context UX and settings

**Files:**
- Modify: `src/ESAPI.RunnerHub/ViewModels/MainViewModel.cs`
- Modify: `src/ESAPI.RunnerHub/ViewModels/ApplicationCardViewModel.cs`
- Modify: `src/ESAPI.RunnerHub/MainWindow.xaml`
- Modify: `src/ESAPI.RunnerHub/MainWindow.xaml.cs`
- Modify: `src/ESAPI.RunnerHub/ViewModels/SettingsViewModel.cs`
- Modify: `src/ESAPI.RunnerHub/SettingsWindow.xaml`
- Test: `tests/ESAPI.RunnerHub.Tests/MainContextViewModelTests.cs`
- Test: `tests/ESAPI.RunnerHub.Tests/SettingsViewModelTests.cs`

- [ ] **Step 1: Write failing selection/readiness tests**

Select a patient, load descriptors, select plan `P1`, and assert course/structure set auto-selection plus enabled direct launch. Clear the plan and select `SS-ONLY`; assert a `PlanOrStructureSet` tool remains enabled while a `Plan` tool reports `Plan required`.

- [ ] **Step 2: Run and verify missing properties/commands**

- [ ] **Step 3: Add the compact selector and direct-host card behavior**

The patient panel exposes course, planning item, structure set, and scope controls only after patient selection. `SelectPatient` triggers asynchronous context loading through the window coordinator. Cards show `Context script`, requirement, `Read-only` or `Save confirmation`, and one action label matching the selected context. The existing Eclipse reference card and EXE buttons remain unchanged.

Settings expose all new enums, entry type, and extra references. File browsing includes `.exe;*.esapi.dll;*.dll;*.cs`.

- [ ] **Step 4: Run offline UI smoke plus all tests and commit**

Commit: `feat: select plan and structure context in hub`

### Task 8: Live catalogue, packaging, and documentation

**Files:**
- Modify: `settings.example.ini`
- Modify: `dist/settings.ini`
- Modify: `tools/build-release.ps1`
- Modify: `tools/validate-vendor-free.ps1`
- Modify: `tests/ESAPI.RunnerHub.Tests/ReleaseMetadataTests.cs`
- Modify: `README.md`
- Modify: `CHANGELOG.md`
- Modify: `versionInfo.json`
- Modify: `citrix/current.txt`

- [ ] **Step 1: Write failing release-contract tests**

Assert the solution builds `ESAPI-Script-Host.exe`, the package contains both EXEs, neither VMS nor EsapiEssentials DLL files are packaged, live settings contain direct ClearPlan and PlanFieldNamer entries, and the Citrix pointer targets the new immutable version.

- [ ] **Step 2: Run and verify packaging tests fail**

- [ ] **Step 3: Update release and live configuration**

Add direct ClearPlan pointing to `ClearPlan_OpenSource\built\ClearPlan.esapi.dll` with `EsapiEssentials`, `PlanningItem`, `Multiple`, `ReadOnly`. Add PlanFieldNamer pointing to `_Plan-FieldNamer\debug\Plan_FieldNamer.esapi.dll` with `Eclipse`, `Plan`, `Single`, `ConfirmSave`. Preserve their existing runner/reference entries.

Package `ESAPI-Runner-Hub.exe` and `ESAPI-Script-Host.exe`, retain immutable versioned Citrix artifacts, update the vendor-free allowlist for the first-party host EXE, and document context selection, save behavior, privacy, and Eclipse 18 gating.

- [ ] **Step 4: Build and validate the release**

Run: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\build-release.ps1`

Expected: build succeeds, all unit/integration/Citrix tests pass, vendor-free validation passes, deterministic ZIP and immutable Citrix binaries are published.

- [ ] **Step 5: Run safe live checks**

Start the Citrix `dist` Hub, verify patient search, load a designated synthetic/nonclinical context, run a read-only direct fixture, verify a deliberate child failure leaves the Hub usable, and only run PlanFieldNamer saving against an explicitly nonclinical patient. If a suitable synthetic patient is unavailable, complete technical build validation and record the live write check as not performed rather than using clinical data.

- [ ] **Step 6: Commit and push**

Commit: `release: add isolated ESAPI context host`

Push `main` to the configured internal and public remotes only after confirming the vendor-free check and reviewing the staged file list.

## Final verification

- [ ] `git diff --check` is clean.
- [ ] Full Release build and test suite passes from the UNC repository.
- [ ] Offline UI smoke starts and closes without an unhandled exception.
- [ ] Both release EXEs have the expected icon and version.
- [ ] No patient/context identifier appears in logs, arguments, repository files, or release manifests.
- [ ] Existing executable launch, patient argument/environment transport, Eclipse reference cards, and Citrix launcher tests still pass.
- [ ] The final commit contains no vendor DLL, patient data, local logs, or unrelated repository changes.
