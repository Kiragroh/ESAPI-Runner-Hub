# Run History Recovery And Patient Selection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep the Runner Hub open after child-process exit, recover stale running history, and select a history patient without launching a script.

**Architecture:** Capture the UI `SynchronizationContext` when `MainViewModel` is created and post the complete, idempotent child-exit transition to it. Reconcile nonterminal persisted history during load, and resolve history patients on demand from the DPAPI envelope plus current patient index. Keep replay and patient selection as separate commands.

**Tech Stack:** C# 7.3, .NET Framework 4.8, WPF, `System.Diagnostics.Process`, `SynchronizationContext`, DPAPI-protected JSON history, x64 MSBuild, custom executable test harness.

---

## File Map

- Modify `tests/RunnerFixture/Program.cs`: deterministic delayed child process for exit-thread tests.
- Modify `tests/ESAPI.RunnerHub.Tests/LaunchHistoryViewModelTests.cs`: regression tests for UI dispatch, interrupted recovery, and patient restoration.
- Modify `tests/ESAPI.RunnerHub.Tests/PatientSearchTests.cs`: exact-ID lookup coverage.
- Modify `tests/ESAPI.RunnerHub.Tests/ProjectShapeTests.cs`: WPF action-column contract.
- Modify `src/ESAPI.RunnerHub/History/LaunchHistoryEntry.cs`: append terminal `Interrupted` state without renumbering existing values.
- Modify `src/ESAPI.RunnerHub/Patients/PatientSearchIndex.cs`: exact current-directory lookup by ID.
- Modify `src/ESAPI.RunnerHub/ViewModels/ActivityRowViewModel.cs`: patient-selection availability and tooltip state.
- Modify `src/ESAPI.RunnerHub/ViewModels/MainViewModel.cs`: UI-thread exit transition, stale-history reconciliation, and select-patient command.
- Modify `src/ESAPI.RunnerHub/MainWindow.xaml`: compact second history action.
- Modify `README.md`, `CHANGELOG.md`, `versionInfo.json`, `src/*/Properties/AssemblyInfo.cs`: v0.3.4 release documentation and metadata.
- Modify `docs/FINAL_VERIFICATION.md` and `docs/RELEASE_VERIFICATION.md`: verified test and artifact evidence.

### Task 1: Dispatch Child Exit To The UI Context

**Files:**
- Modify: `tests/RunnerFixture/Program.cs`
- Modify: `tests/ESAPI.RunnerHub.Tests/LaunchHistoryViewModelTests.cs`
- Modify: `src/ESAPI.RunnerHub/ViewModels/MainViewModel.cs:20-63,579-597`

- [ ] **Step 1: Add a deterministic delayed fixture mode**

Add `using System.Threading;` and this branch before `capture` handling:

```csharp
if (string.Equals(mode, "delay", StringComparison.OrdinalIgnoreCase))
{
    int milliseconds;
    if (!int.TryParse(ValueAfter(args, "--milliseconds"), out milliseconds)) milliseconds = 200;
    Thread.Sleep(Math.Max(1, milliseconds));
}
```

- [ ] **Step 2: Write the failing background-exit test**

Register `child exit returns to captured UI context` and add a queueing synchronization context. Construct the view model while it is current, restore the prior context, start `RunnerFixture.exe --mode delay --milliseconds 250`, wait for a posted callback, execute it on the owner thread, then assert `Exited`, persisted terminal state, and `RunAgainCommand.CanExecute(row) == true`.

```csharp
private sealed class QueueingSynchronizationContext : SynchronizationContext
{
    private readonly Queue<SendOrPostCallback> callbacks = new Queue<SendOrPostCallback>();
    public int PendingCount { get { lock (callbacks) return callbacks.Count; } }
    public override void Post(SendOrPostCallback callback, object state)
    {
        lock (callbacks) callbacks.Enqueue(_ => callback(state));
    }
    public void Drain()
    {
        while (true)
        {
            SendOrPostCallback callback;
            lock (callbacks)
            {
                if (callbacks.Count == 0) return;
                callback = callbacks.Dequeue();
            }
            callback(null);
        }
    }
}
```

- [ ] **Step 3: Run the suite and verify RED**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\build-release.ps1
```

Expected: the new test fails because the process exit is handled directly on the process thread and no callback is posted to the captured context.

- [ ] **Step 4: Implement the minimal UI-context transition**

Capture `SynchronizationContext.Current` in the constructor and add:

```csharp
private readonly SynchronizationContext uiSynchronizationContext;

private void RunOnUi(Action action)
{
    if (uiSynchronizationContext == null || ReferenceEquals(SynchronizationContext.Current, uiSynchronizationContext))
    {
        action();
        return;
    }
    uiSynchronizationContext.Post(_ => action(), null);
}
```

Change only the asynchronous subscription:

```csharp
process.Exited += (sender, args) => RunOnUi(handleExit);
if (!process.IsRunning) handleExit();
```

The existing `Interlocked.Exchange` remains inside `handleExit`.

- [ ] **Step 5: Run the suite and verify GREEN**

Run the release build command from Step 3. Expected: all existing tests plus the new exit-context test pass; Citrix launcher and vendor-free checks pass.

- [ ] **Step 6: Commit**

```powershell
git add tests/RunnerFixture/Program.cs tests/ESAPI.RunnerHub.Tests/LaunchHistoryViewModelTests.cs src/ESAPI.RunnerHub/ViewModels/MainViewModel.cs
git commit -m "fix: keep hub alive after child exit"
```

### Task 2: Recover Interrupted History

**Files:**
- Modify: `tests/ESAPI.RunnerHub.Tests/LaunchHistoryViewModelTests.cs`
- Modify: `src/ESAPI.RunnerHub/History/LaunchHistoryEntry.cs:13-20`
- Modify: `src/ESAPI.RunnerHub/ViewModels/ActivityRowViewModel.cs:33-42`
- Modify: `src/ESAPI.RunnerHub/ViewModels/MainViewModel.cs:652-684`

- [ ] **Step 1: Write failing recovery tests**

Persist one `Starting` and one `Running` entry, construct a new view model, mark its application ready, and assert both rows are `Interrupted`, persisted as `Interrupted`, show status `Interrupted`, and allow replay. Also assert an already `Exited` row is unchanged.

- [ ] **Step 2: Run the tests and verify RED**

Run the release build. Expected: compile or assertion failure because `LaunchHistoryState.Interrupted` does not exist and nonterminal history is left unchanged.

- [ ] **Step 3: Append and apply the terminal state**

Append the enum member so existing serialized numeric values remain stable:

```csharp
public enum LaunchHistoryState
{
    Starting,
    Running,
    Exited,
    FailedToStart,
    Unavailable,
    Interrupted
}
```

In `LoadHistory`, before catalogue availability is applied:

```csharp
if (entry.State == LaunchHistoryState.Starting || entry.State == LaunchHistoryState.Running)
{
    entry.State = LaunchHistoryState.Interrupted;
    entry.FinishedUtc = entry.FinishedUtc ?? DateTime.UtcNow;
    historyChanged = true;
}
```

Return `Interrupted` explicitly from `ActivityRowViewModel.Status`. Persist once after the load loop when any entry changed.

- [ ] **Step 4: Run the suite and verify GREEN**

Expected: interrupted entries are terminal and replayable; all lifecycle tests pass.

- [ ] **Step 5: Commit**

```powershell
git add tests/ESAPI.RunnerHub.Tests/LaunchHistoryViewModelTests.cs src/ESAPI.RunnerHub/History/LaunchHistoryEntry.cs src/ESAPI.RunnerHub/ViewModels/ActivityRowViewModel.cs src/ESAPI.RunnerHub/ViewModels/MainViewModel.cs
git commit -m "fix: recover interrupted launch history"
```

### Task 3: Select A Patient Without Replaying

**Files:**
- Modify: `tests/ESAPI.RunnerHub.Tests/PatientSearchTests.cs`
- Modify: `tests/ESAPI.RunnerHub.Tests/LaunchHistoryViewModelTests.cs`
- Modify: `src/ESAPI.RunnerHub/Patients/PatientSearchIndex.cs`
- Modify: `src/ESAPI.RunnerHub/ViewModels/ActivityRowViewModel.cs`
- Modify: `src/ESAPI.RunnerHub/ViewModels/MainViewModel.cs`

- [ ] **Step 1: Write failing exact-ID and command tests**

Test `PatientSearchIndex.FindById` case-insensitively. Add history tests that protect a `ContextSelection { PatientId = "SYN-4242" }`, execute `SelectHistoryPatientCommand`, and assert:

```csharp
TestHarness.AssertEqual("SYN-4242", viewModel.SelectedPatientId);
TestHarness.AssertEqual(1, selectionEvents);
TestHarness.AssertEqual(0, viewModel.Activities.Count(item => item.State == LaunchHistoryState.Running));
```

Add disabled cases for `WithoutPatient`, an invalid DPAPI envelope, and a protected patient absent from the current index.

- [ ] **Step 2: Run the suite and verify RED**

Expected: compile failures for the missing `FindById`, command, and row availability members.

- [ ] **Step 3: Implement exact lookup and row state**

Add to `PatientSearchIndex`:

```csharp
public PatientRecord FindById(string id)
{
    var normalized = Normalize(id);
    return patients.Where(item => item.Id == normalized).Select(item => item.Patient).FirstOrDefault();
}
```

Add `CanSelectPatient`, `PatientSelectionAvailabilityText`, and `SetPatientSelectionAvailability(bool, string)` to `ActivityRowViewModel`, using its existing UI-safe notification helper.

- [ ] **Step 4: Implement the separate command**

Create a `RelayCommand selectHistoryPatientCommand` and public `ICommand SelectHistoryPatientCommand`. Resolve availability without storing plaintext patient IDs in the row:

```csharp
private bool TryResolveHistoryPatient(ActivityRowViewModel row, out PatientRecord patient, out string reason)
{
    patient = null;
    if (row.Entry.LaunchMode == LaunchMode.WithoutPatient)
    {
        reason = "No patient stored for this run";
        return false;
    }
    ContextSelection selection;
    try { selection = contextProtector.Unprotect(row.Entry.ProtectedContext); }
    catch { reason = "Protected context is unavailable"; return false; }
    patient = patientIndex == null ? null : patientIndex.FindById(selection.PatientId);
    reason = patient == null ? "Patient is unavailable in the current directory" : "Select patient without running the application";
    return patient != null;
}
```

The execute path calls only `SelectPatient(patient)`, sets a neutral notification, and never calls `Launch` or `RunAgain`. Refresh availability after `SetPatients` and `LoadHistory` and raise the command once per refresh.

- [ ] **Step 5: Run the suite and verify GREEN**

Expected: patient selection updates the current context and emits one selection event without creating a new activity row or child process.

- [ ] **Step 6: Commit**

```powershell
git add tests/ESAPI.RunnerHub.Tests/PatientSearchTests.cs tests/ESAPI.RunnerHub.Tests/LaunchHistoryViewModelTests.cs src/ESAPI.RunnerHub/Patients/PatientSearchIndex.cs src/ESAPI.RunnerHub/ViewModels/ActivityRowViewModel.cs src/ESAPI.RunnerHub/ViewModels/MainViewModel.cs
git commit -m "feat: select patient from launch history"
```

### Task 4: Add The Compact History Action

**Files:**
- Modify: `tests/ESAPI.RunnerHub.Tests/ProjectShapeTests.cs`
- Modify: `src/ESAPI.RunnerHub/MainWindow.xaml:298-329`

- [ ] **Step 1: Write the failing XAML contract test**

Assert that `MainWindow.xaml` contains `SelectHistoryPatientCommand`, `PatientSelectionAvailabilityText`, `Content="Select patient"`, and `HorizontalScrollBarVisibility="Disabled"`.

- [ ] **Step 2: Run the suite and verify RED**

Expected: the new shape assertions fail because the second action is absent.

- [ ] **Step 3: Add the compact action group**

Change the header to **Actions** and replace the single button with:

```xml
<StackPanel Grid.Column="5" Orientation="Horizontal">
    <Button Content="Select patient" ToolTip="{Binding PatientSelectionAvailabilityText}"
            Command="{Binding DataContext.SelectHistoryPatientCommand, RelativeSource={RelativeSource AncestorType=Window}}"
            CommandParameter="{Binding}" Style="{StaticResource SecondaryButton}" Padding="8,5" FontSize="10.5" />
    <Button Content="Run again" ToolTip="{Binding ReplayAvailabilityText}" Margin="6,0,0,0"
            Command="{Binding DataContext.RunAgainCommand, RelativeSource={RelativeSource AncestorType=Window}}"
            CommandParameter="{Binding}" Style="{StaticResource SecondaryButton}" Padding="8,5" FontSize="10.5" />
</StackPanel>
```

- [ ] **Step 4: Run the suite and offline UI smoke**

Run the release build, then launch the worktree artifact with `--offline-ui-smoke`; verify it stays open for at least five seconds, the history has two actions without horizontal scrolling, and close it cleanly.

- [ ] **Step 5: Commit**

```powershell
git add tests/ESAPI.RunnerHub.Tests/ProjectShapeTests.cs src/ESAPI.RunnerHub/MainWindow.xaml
git commit -m "feat: add patient action to recent activity"
```

### Task 5: Release And Productive Verification

**Files:**
- Modify: `README.md`
- Modify: `CHANGELOG.md`
- Modify: `versionInfo.json`
- Modify: `src/ESAPI.RunnerHub/Properties/AssemblyInfo.cs`
- Modify: `src/ESAPI.CitrixLauncher/Properties/AssemblyInfo.cs`
- Modify: `src/ESAPI.ScriptHost/Properties/AssemblyInfo.cs`
- Modify: `src/ESAPI.WriteScriptHost/Properties/AssemblyInfo.cs`
- Modify: `docs/FINAL_VERIFICATION.md`
- Modify: `docs/RELEASE_VERIFICATION.md`
- Modify: `citrix/current.txt`

- [ ] **Step 1: Set release metadata**

Set version `0.3.4`, build `23`, date `2026-08-08`, assembly/file version `0.3.4.0`, and `citrix/current.txt` to `ESAPI-Runner-Hub.v0.3.4.exe`. Document the crash fix, interrupted recovery, and patient-only history selection in English.

- [ ] **Step 2: Run the complete release pipeline**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\build-release.ps1
```

Expected: all automated tests, both Citrix launcher contracts, vendor-free validation, and deterministic package creation pass.

- [ ] **Step 3: Verify artifacts and smoke behavior**

Verify file version and SHA-256 for the versioned Runner, both script hosts, and ZIP. Start `dist/versions/ESAPI-Runner-Hub.v0.3.4.exe --offline-ui-smoke`, confirm it remains alive, then close it. Record exact results in both verification documents.

- [ ] **Step 4: Commit and integrate**

```powershell
git add README.md CHANGELOG.md versionInfo.json src/*/Properties/AssemblyInfo.cs docs/FINAL_VERIFICATION.md docs/RELEASE_VERIFICATION.md citrix/current.txt
git commit -m "release: publish resilient history actions in v0.3.4"
```

Merge the isolated branch into `main` only with a clean worktree and green verification.

- [ ] **Step 5: Publish and update InHouse visibility**

Push `main` and annotated tag `v0.3.4` to GitUKL and GitHub, create the GitHub release with ZIP and checksums, update InHouse script ID 62 README/version wording without touching unrelated Hub files, and verify database-backed version resolution reports `0.3.4` build `23`.

- [ ] **Step 6: Perform final operational checks**

Confirm the productive stable Citrix launcher still points through `citrix/current.txt`, the live `settings.ini` retains both internal script-host paths, the live versioned Runner hash matches the release manifest, and the main repository is clean.
