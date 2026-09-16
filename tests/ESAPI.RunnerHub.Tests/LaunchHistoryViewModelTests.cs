using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using EsapiRunnerHub.Configuration;
using EsapiRunnerHub.Context;
using EsapiRunnerHub.History;
using EsapiRunnerHub.Launching;
using EsapiRunnerHub.Patients;
using EsapiRunnerHub.ViewModels;

namespace EsapiRunnerHub.Tests
{
    internal static class LaunchHistoryViewModelTests
    {
        public static void Register()
        {
            TestHarness.Test("standalone lifecycle is persisted and restartable", PersistsStandaloneLifecycle);
            TestHarness.Test("child exit returns to captured UI context", ChildExitReturnsToCapturedUiContext);
            TestHarness.Test("patient relaunch uses protected patient and current definition", RelaunchesPatient);
            TestHarness.Test("context relaunch retains exact protected planning selection", RelaunchesContext);
            TestHarness.Test("removed application history is unavailable", MarksRemovedApplicationsUnavailable);
            TestHarness.Test("failed start remains retryable when target returns", RetriesFailedStart);
            TestHarness.Test("replay command refreshes when asynchronous readiness arrives", RefreshesReplayCommand);
            TestHarness.Test("replay rows explain every unavailable state", ExplainsReplayAvailability);
            TestHarness.Test("running activity cannot be replayed until terminal", DisablesReplayWhileRunning);
            TestHarness.Test("stale running history stays explicitly unmonitored", RecoversInterruptedHistory);
            TestHarness.Test("history patient selection never replays the application", SelectsHistoryPatientWithoutReplay);
            TestHarness.Test("history patient selection explains unavailable patients", ExplainsUnavailableHistoryPatients);
            TestHarness.Test("history default view model imports configured local fallback", ImportsConfiguredFallback);
            TestHarness.Test("history storage failure is visible independently of launch messages", ShowsStorageFailure);
            TestHarness.Test("history unavailable share does not block UI construction", DoesNotBlockUiConstruction);
            TestHarness.Test("history closing drains queued local writes before shutdown", DrainsHistoryOnClose);
            TestHarness.Test("history delayed load preserves new launches and stages them locally", PreservesLaunchDuringLoad);
            TestHarness.Test("history opening another runner never persists inferred interruption", DoesNotPersistInferredInterruption);
            TestHarness.Test("history prior-session live state is unknown and never replayed blindly", DisablesUnmonitoredReplay);
            TestHarness.Test("history shutdown drains owners retained across settings reload", DrainsAllHistoryOwners);
        }

        private static void PersistsStandaloneLifecycle()
        {
            WithHistory((store, directory) =>
            {
                var viewModel = CreateStandaloneViewModel(store, Fixture(), "--mode success");
                var card = Ready(viewModel);
                viewModel.StartWithoutPatientCommand.Execute(card);
                WaitForTerminal(viewModel.Activities[0]);

                TestHarness.AssertEqual(LaunchHistoryState.Exited, viewModel.Activities[0].State);
                TestHarness.AssertEqual(0, viewModel.Activities[0].ExitCode.GetValueOrDefault());
                TestHarness.AssertEqual(LaunchHistoryState.Exited, store.Load().Single().State);
                TestHarness.AssertTrue(viewModel.Activities[0].CanRunAgain);
            });
        }

        private static void ChildExitReturnsToCapturedUiContext()
        {
            WithHistory((store, directory) =>
            {
                var previous = SynchronizationContext.Current;
                var uiContext = new QueueingSynchronizationContext();
                MainViewModel viewModel;
                try
                {
                    SynchronizationContext.SetSynchronizationContext(uiContext);
                    viewModel = CreateStandaloneViewModel(store, Fixture(), "--mode delay --milliseconds 250");
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previous);
                }

                var card = Ready(viewModel);
                viewModel.StartWithoutPatientCommand.Execute(card);
                var row = viewModel.Activities[0];
                WaitForCondition(() => uiContext.PendingCount > 0, "Child exit was not posted to the captured UI context.");

                TestHarness.AssertEqual(LaunchHistoryState.Running, row.State);
                uiContext.Drain();

                TestHarness.AssertEqual(LaunchHistoryState.Exited, row.State);
                TestHarness.AssertTrue(viewModel.RunAgainCommand.CanExecute(row));
                TestHarness.AssertEqual(LaunchHistoryState.Exited, store.Load().Single().State);
            });
        }

        private static void RelaunchesPatient()
        {
            WithHistory((store, directory) =>
            {
                var capture = Path.Combine(directory, "capture.txt");
                var arguments = "--mode capture --capture \"" + capture + "\"";
                var first = CreateStandaloneViewModel(store, Fixture(), arguments, PatientMode.Required, PatientTransport.Environment);
                first.SelectPatient(new PatientRecord("SYN-4242", "Ada", "Example", 0));
                var card = Ready(first);
                first.StartWithPatientCommand.Execute(card);
                WaitForFile(capture);
                WaitForTerminal(first.Activities[0]);
                File.Delete(capture);

                var restored = CreateStandaloneViewModel(store, Fixture(), arguments, PatientMode.Required, PatientTransport.Environment);
                Ready(restored);
                var row = restored.Activities[0];
                restored.RunAgainCommand.Execute(row);
                WaitForFile(capture);
                WaitForTerminal(restored.Activities[0]);

                TestHarness.AssertContains(File.ReadAllText(capture), "patient=SYN-4242");
            });
        }

        private static void RelaunchesContext()
        {
            WithHistory((store, directory) =>
            {
                var fixture = Fixture();
                var script = Path.Combine(directory, "Synthetic.esapi.dll");
                File.WriteAllBytes(script, new byte[] { 0 });
                var ini = @"
[Hub]
ScriptHostExecutable=" + fixture + @"
[Application.context]
Name=Context fixture
Executable=" + script + @"
LaunchKind=EsapiContextScript
ScriptEngine=Eclipse
ContextRequirement=Plan
ScopeMode=Single
WriteMode=ReadOnly
PatientMode=Required
PatientTransport=None
Enabled=true
";
                var configuration = IniConfigurationStore.ParseText(ini, Path.Combine(directory, "settings.ini"));
                var first = new MainViewModel(configuration, new List<PatientRecord>(), store, new ProtectedContextEnvelope());
                first.SelectPatient(new PatientRecord("SYN-1001", "Ada", "Example", 0));
                var context = new ContextDirectory();
                context.Courses.Add(new CourseDescriptor { Id = "C1" });
                context.Plans.Add(new PlanDescriptor { Id = "P1", CourseId = "C1", StructureSetId = "SS1", ImageId = "IMG1" });
                context.StructureSets.Add(new StructureSetDescriptor { Id = "SS1", ImageId = "IMG1" });
                context.Images.Add(new ImageDescriptor { Id = "IMG1" });
                first.SetContextDirectory(context);
                first.SelectedPlan = context.Plans[0];
                var card = Ready(first);
                first.StartContextCommand.Execute(card);
                WaitForTerminal(first.Activities[0]);

                var restored = new MainViewModel(configuration, new List<PatientRecord>(), store, new ProtectedContextEnvelope());
                Ready(restored);
                restored.RunAgainCommand.Execute(restored.Activities[0]);
                WaitForTerminal(restored.Activities[0]);

                var latest = store.Load().OrderByDescending(item => item.StartedUtc).First();
                var selection = new ProtectedContextEnvelope().Unprotect(latest.ProtectedContext);
                TestHarness.AssertEqual("SYN-1001", selection.PatientId);
                TestHarness.AssertEqual("P1", selection.PlanId);
                TestHarness.AssertEqual("SS1", selection.StructureSetId);
                TestHarness.AssertEqual("IMG1", selection.ImageId);
            });
        }

        private static void MarksRemovedApplicationsUnavailable()
        {
            WithHistory((store, directory) =>
            {
                store.Save(new[]
                {
                    new LaunchHistoryEntry
                    {
                        HistoryId = "gone", ApplicationId = "removed", ApplicationName = "Removed",
                        ArtifactLabel = "Binary", AccessLabel = "Read-only", StartedUtc = DateTime.UtcNow,
                        State = LaunchHistoryState.Exited, LaunchMode = LaunchMode.WithoutPatient
                    }
                });
                var viewModel = new MainViewModel(new HubConfiguration(), new List<PatientRecord>(), store, new ProtectedContextEnvelope());

                TestHarness.AssertEqual(LaunchHistoryState.Unavailable, viewModel.Activities.Single().State);
                TestHarness.AssertFalse(viewModel.Activities.Single().CanRunAgain);
                TestHarness.AssertFalse(viewModel.RunAgainCommand.CanExecute(viewModel.Activities.Single()));
            });
        }

        private static void RetriesFailedStart()
        {
            WithHistory((store, directory) =>
            {
                var missing = Path.Combine(directory, "fixture.exe");
                var viewModel = CreateStandaloneViewModel(store, missing, "--mode success");
                var card = Ready(viewModel);
                viewModel.StartWithoutPatientCommand.Execute(card);
                TestHarness.AssertEqual(LaunchHistoryState.FailedToStart, viewModel.Activities[0].State);

                File.Copy(Fixture(), missing);
                viewModel.UpdateApplicationReadiness(card.Id, new PathProbeResult(PathReadiness.Ready, "Ready"));
                viewModel.RunAgainCommand.Execute(viewModel.Activities[0]);
                WaitForTerminal(viewModel.Activities[0]);
                TestHarness.AssertEqual(LaunchHistoryState.Exited, viewModel.Activities[0].State);
            });
        }

        private static void RefreshesReplayCommand()
        {
            WithHistory((store, directory) =>
            {
                store.Save(new[] { ExitedEntry("ready-later", "fixture", LaunchMode.WithoutPatient, null) });
                var viewModel = CreateStandaloneViewModel(store, Fixture(), "--mode success");
                var row = viewModel.Activities.Single();
                var notifications = 0;
                viewModel.RunAgainCommand.CanExecuteChanged += (sender, args) => notifications++;

                TestHarness.AssertFalse(viewModel.RunAgainCommand.CanExecute(row));
                viewModel.UpdateApplicationReadiness("fixture", new PathProbeResult(PathReadiness.Ready, "Ready"));

                TestHarness.AssertTrue(notifications > 0, "RunAgainCommand did not notify WPF after readiness changed.");
                TestHarness.AssertTrue(viewModel.RunAgainCommand.CanExecute(row));
            });
        }

        private static void ExplainsReplayAvailability()
        {
            WithHistory((store, directory) =>
            {
                store.Save(new[]
                {
                    ExitedEntry("available", "fixture", LaunchMode.WithoutPatient, null),
                    ExitedEntry("protected", "fixture", LaunchMode.WithPatient, "not-a-dpapi-envelope"),
                    ExitedEntry("removed", "removed", LaunchMode.WithoutPatient, null)
                });
                var viewModel = CreateStandaloneViewModel(store, Fixture(), "--mode success");
                var available = viewModel.Activities.Single(item => item.Entry.HistoryId == "available");
                var protectedRow = viewModel.Activities.Single(item => item.Entry.HistoryId == "protected");
                var removed = viewModel.Activities.Single(item => item.Entry.HistoryId == "removed");

                TestHarness.AssertEqual("Application path is unavailable", ReplayText(available));
                TestHarness.AssertEqual("Application was removed from the catalogue", ReplayText(removed));

                viewModel.UpdateApplicationReadiness("fixture", new PathProbeResult(PathReadiness.Ready, "Ready"));

                TestHarness.AssertEqual("Ready to run again", ReplayText(available));
                TestHarness.AssertEqual("Protected context is unavailable", ReplayText(protectedRow));
            });
        }

        private static void DisablesReplayWhileRunning()
        {
            WithHistory((store, directory) =>
            {
                var entry = ExitedEntry("running", "fixture", LaunchMode.WithoutPatient, null);
                store.Save(new[] { entry });
                var viewModel = CreateStandaloneViewModel(store, Fixture(), "--mode success");
                var row = viewModel.Activities.Single();
                row.Entry.State = LaunchHistoryState.Running;
                row.Refresh();

                viewModel.UpdateApplicationReadiness("fixture", new PathProbeResult(PathReadiness.Ready, "Ready"));
                TestHarness.AssertFalse(viewModel.RunAgainCommand.CanExecute(row));
                TestHarness.AssertContains(ReplayText(row), "Previous session is not monitored");

                row.Entry.State = LaunchHistoryState.Exited;
                row.Refresh();
                viewModel.UpdateApplicationReadiness("fixture", new PathProbeResult(PathReadiness.Ready, "Ready"));

                TestHarness.AssertTrue(viewModel.RunAgainCommand.CanExecute(row));
                TestHarness.AssertEqual("Ready to run again", ReplayText(row));
            });
        }

        private static void ImportsConfiguredFallback()
        {
            WithHistory((store, directory) =>
            {
                var local = Path.Combine(directory, "previous-local.json");
                new LaunchHistoryStore(local, 30, 100).Save(new[] { ExitedEntry("migrated", "fixture", LaunchMode.WithoutPatient, null) });
                var configuration = IniConfigurationStore.ParseText("[Hub]\nHistoryFile=shared.json\nHistoryFallbackFile=previous-local.json\n",
                    Path.Combine(directory, "settings.ini"));
                var viewModel = new MainViewModel(configuration, new PatientRecord[0]);
                TestHarness.AssertTrue(viewModel.WaitForHistorySynchronizationAsync().Wait(5000));
                TestHarness.AssertTrue(SpinWait.SpinUntil(() => !viewModel.HistoryStorageStatus.Contains("Loading"), 5000));
                TestHarness.AssertEqual("migrated", viewModel.Activities.Single().Entry.HistoryId);
                TestHarness.AssertTrue(viewModel.WaitForHistorySynchronizationAsync().Wait(5000));
            });
        }

        private static void ShowsStorageFailure()
        {
            WithHistory((store, directory) =>
            {
                var path = Path.Combine(directory, "corrupt.json");
                File.WriteAllText(path, "{synthetic corruption");
                var viewModel = CreateStandaloneViewModel(new LaunchHistoryStore(path, 30, 100), Fixture(), "--mode success");
                TestHarness.AssertContains(viewModel.HistoryStorageStatus, "could not be loaded");
                TestHarness.AssertTrue(viewModel.HasHistoryStorageWarning);
            });
        }

        private static void DoesNotBlockUiConstruction()
        {
            WithHistory((store, directory) =>
            {
                var path = Path.Combine(directory, "locked.json");
                var configuration = IniConfigurationStore.ParseText("[Hub]\nHistoryFile=locked.json\nHistoryFallbackFile=local.json\n",
                    Path.Combine(directory, "settings.ini"));
                MainViewModel viewModel;
                using (new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    viewModel = new MainViewModel(configuration, new PatientRecord[0]);
                    TestHarness.AssertTrue(watch.Elapsed < TimeSpan.FromSeconds(1), "History I/O blocked UI construction.");
                }
                TestHarness.AssertTrue(SpinWait.SpinUntil(() => !viewModel.HistoryStorageStatus.Contains("Loading"), 5000));
            });
        }

        private static void DrainsHistoryOnClose()
        {
            var code = File.ReadAllText(TestHarness.PathFromRoot("src/ESAPI.RunnerHub/MainWindow.xaml.cs"));
            TestHarness.AssertContains(code, "Closing += WindowClosing");
            TestHarness.AssertContains(code, "FlushLocalHistoryAsync()");
            TestHarness.AssertContains(code, "Task.Delay(5000)");
        }

        private static void PreservesLaunchDuringLoad()
        {
            WithHistory((store, directory) =>
            {
                var path = Path.Combine(directory, "shared.json");
                var old = ExitedEntry("previous", "fixture", LaunchMode.WithoutPatient, null);
                old.StartedUtc = DateTime.UtcNow.AddMinutes(-1);
                new LaunchHistoryStore(path, 30, 100).Save(new[] { old });
                var configuration = IniConfigurationStore.ParseText("[Hub]\nHistoryFile=shared.json\nHistoryFallbackFile=local.json\n" +
                    "[Application.fixture]\nName=Fixture\nExecutable=" + Fixture() + "\nArguments=--mode success\nPatientMode=Optional\n",
                    Path.Combine(directory, "settings.ini"));
                MainViewModel viewModel;
                using (new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    viewModel = new MainViewModel(configuration, new PatientRecord[0]);
                    Ready(viewModel);
                    viewModel.StartWithoutPatientCommand.Execute(viewModel.Applications.Single());
                    TestHarness.AssertEqual(1, viewModel.Activities.Count);
                    TestHarness.AssertTrue(viewModel.FlushLocalHistoryAsync().Wait(2000), "Local recovery staging did not complete within 2 seconds.");
                    TestHarness.AssertTrue(viewModel.FlushLocalHistoryAsync().Result, "Local recovery staging failed.");
                    TestHarness.AssertEqual(1, new LaunchHistoryStore(Path.Combine(directory, "local.json"), 30, 100).Load().Count);
                }
                TestHarness.AssertTrue(SpinWait.SpinUntil(() => !viewModel.HistoryStorageStatus.Contains("Loading"), 8000), "Delayed history load did not complete within 8 seconds.");
                TestHarness.AssertTrue(viewModel.WaitForHistorySynchronizationAsync().Wait(8000), "Shared history synchronization did not complete within 8 seconds.");
                TestHarness.AssertEqual(2, viewModel.Activities.Count);
                TestHarness.AssertEqual(2, viewModel.Activities.Select(row => row.Entry.HistoryId).Distinct().Count());
            });
        }

        private static void DoesNotPersistInferredInterruption()
        {
            WithHistory((store, directory) =>
            {
                var running = ExitedEntry("live-elsewhere", "fixture", LaunchMode.WithoutPatient, null);
                running.State = LaunchHistoryState.Running;
                running.FinishedUtc = null;
                store.Save(new[] { running });
                CreateStandaloneViewModel(store, Fixture(), "--mode success");
                TestHarness.AssertEqual(LaunchHistoryState.Running, store.Load().Single().State);
            });
        }

        private static void DisablesUnmonitoredReplay()
        {
            WithHistory((store, directory) =>
            {
                var running = ExitedEntry("other-runner", "fixture", LaunchMode.WithoutPatient, null);
                running.State = LaunchHistoryState.Running;
                running.FinishedUtc = null;
                store.Save(new[] { running });
                var viewModel = CreateStandaloneViewModel(store, Fixture(), "--mode success");
                Ready(viewModel);
                TestHarness.AssertContains(viewModel.Activities.Single().Status, "status unknown");
                TestHarness.AssertFalse(viewModel.RunAgainCommand.CanExecute(viewModel.Activities.Single()));
            });
        }

        private static void DrainsAllHistoryOwners()
        {
            var code = File.ReadAllText(TestHarness.PathFromRoot("src/ESAPI.RunnerHub/MainWindow.xaml.cs"));
            TestHarness.AssertContains(code, "historyOwners.Add(viewModel)");
            TestHarness.AssertContains(code, "historyOwners.Select(owner => owner.FlushLocalHistoryAsync())");
            TestHarness.AssertContains(code, "historyOwners.Select(owner => owner.WaitForHistorySynchronizationAsync())");
        }

        private static void RecoversInterruptedHistory()
        {
            WithHistory((store, directory) =>
            {
                var starting = ExitedEntry("starting", "fixture", LaunchMode.WithoutPatient, null);
                starting.State = LaunchHistoryState.Starting;
                starting.ExitCode = null;
                starting.FinishedUtc = null;
                var running = ExitedEntry("running", "fixture", LaunchMode.WithoutPatient, null);
                running.State = LaunchHistoryState.Running;
                running.ExitCode = null;
                running.FinishedUtc = null;
                var exited = ExitedEntry("exited", "fixture", LaunchMode.WithoutPatient, null);
                store.Save(new[] { starting, running, exited });

                var viewModel = CreateStandaloneViewModel(store, Fixture(), "--mode success");
                Ready(viewModel);

                foreach (var historyId in new[] { "starting", "running" })
                {
                    var row = viewModel.Activities.Single(item => item.Entry.HistoryId == historyId);
                    TestHarness.AssertEqual(historyId == "starting" ? LaunchHistoryState.Starting : LaunchHistoryState.Running, row.State);
                    TestHarness.AssertEqual("Previous session · status unknown", row.Status);
                    TestHarness.AssertFalse(viewModel.RunAgainCommand.CanExecute(row));
                }
                TestHarness.AssertEqual(LaunchHistoryState.Exited,
                    viewModel.Activities.Single(item => item.Entry.HistoryId == "exited").State);

                var persisted = store.Load().ToDictionary(item => item.HistoryId);
                TestHarness.AssertEqual(LaunchHistoryState.Starting, persisted["starting"].State);
                TestHarness.AssertEqual(LaunchHistoryState.Running, persisted["running"].State);
                TestHarness.AssertEqual(LaunchHistoryState.Exited, persisted["exited"].State);
            });
        }

        private static void SelectsHistoryPatientWithoutReplay()
        {
            WithHistory((store, directory) =>
            {
                var protector = new ProtectedContextEnvelope();
                store.Save(new[]
                {
                    ExitedEntry("patient", "fixture", LaunchMode.Context,
                        protector.Protect(new ContextSelection { PatientId = "SYN-4242" }))
                });
                var patient = new PatientRecord("SYN-4242", "Ada", "Example", 0);
                var viewModel = CreateHistoryPatientViewModel(store, protector, new[] { patient });
                var row = viewModel.Activities.Single();
                var selectionEvents = 0;
                viewModel.PatientSelectionChanged += selected => selectionEvents++;
                var activityCount = viewModel.Activities.Count;

                TestHarness.AssertTrue(row.CanSelectPatient);
                TestHarness.AssertTrue(viewModel.SelectHistoryPatientCommand.CanExecute(row));
                viewModel.SelectHistoryPatientCommand.Execute(row);

                TestHarness.AssertEqual("SYN-4242", viewModel.SelectedPatientId);
                TestHarness.AssertEqual(1, selectionEvents);
                TestHarness.AssertEqual(activityCount, viewModel.Activities.Count);
                TestHarness.AssertEqual(LaunchHistoryState.Exited, row.State);
            });
        }

        private static void ExplainsUnavailableHistoryPatients()
        {
            WithHistory((store, directory) =>
            {
                var protector = new ProtectedContextEnvelope();
                store.Save(new[]
                {
                    ExitedEntry("none", "fixture", LaunchMode.WithoutPatient, null),
                    ExitedEntry("protected", "fixture", LaunchMode.Context, "not-a-dpapi-envelope"),
                    ExitedEntry("missing", "fixture", LaunchMode.WithPatient,
                        protector.Protect(new ContextSelection { PatientId = "SYN-MISSING" }))
                });
                var viewModel = CreateHistoryPatientViewModel(store, protector,
                    new[] { new PatientRecord("SYN-4242", "Ada", "Example", 0) });

                var none = viewModel.Activities.Single(item => item.Entry.HistoryId == "none");
                var protectedRow = viewModel.Activities.Single(item => item.Entry.HistoryId == "protected");
                var missing = viewModel.Activities.Single(item => item.Entry.HistoryId == "missing");
                TestHarness.AssertFalse(viewModel.SelectHistoryPatientCommand.CanExecute(none));
                TestHarness.AssertFalse(viewModel.SelectHistoryPatientCommand.CanExecute(protectedRow));
                TestHarness.AssertFalse(viewModel.SelectHistoryPatientCommand.CanExecute(missing));
                TestHarness.AssertEqual("No patient stored for this run", none.PatientSelectionAvailabilityText);
                TestHarness.AssertEqual("Protected context is unavailable", protectedRow.PatientSelectionAvailabilityText);
                TestHarness.AssertEqual("Patient is unavailable in the current directory", missing.PatientSelectionAvailabilityText);
            });
        }

        private static LaunchHistoryEntry ExitedEntry(string historyId, string applicationId, LaunchMode mode, string protectedContext)
        {
            return new LaunchHistoryEntry
            {
                HistoryId = historyId,
                ApplicationId = applicationId,
                ApplicationName = applicationId,
                ArtifactLabel = "Standalone",
                AccessLabel = "Read-only",
                StartedUtc = DateTime.UtcNow,
                State = LaunchHistoryState.Exited,
                ExitCode = 0,
                LaunchMode = mode,
                ProtectedContext = protectedContext
            };
        }

        private static string ReplayText(ActivityRowViewModel row)
        {
            var property = typeof(ActivityRowViewModel).GetProperty("ReplayAvailabilityText");
            TestHarness.AssertTrue(property != null, "ActivityRowViewModel.ReplayAvailabilityText is missing.");
            return (string)property.GetValue(row, null);
        }

        private static MainViewModel CreateStandaloneViewModel(LaunchHistoryStore store, string executable, string arguments,
            PatientMode patientMode = PatientMode.None, PatientTransport transport = PatientTransport.None)
        {
            var configuration = new HubConfiguration();
            configuration.Applications.Add(new ApplicationDefinition
            {
                Id = "fixture", Name = "Fixture", Executable = executable, Arguments = arguments,
                PatientMode = patientMode, PatientTransport = transport,
                PatientEnvironmentKey = "RUNNER_PATIENT_ID", Enabled = true
            });
            return new MainViewModel(configuration, new List<PatientRecord>(), store, new ProtectedContextEnvelope());
        }

        private static MainViewModel CreateHistoryPatientViewModel(LaunchHistoryStore store,
            ProtectedContextEnvelope protector, IEnumerable<PatientRecord> patients)
        {
            var configuration = new HubConfiguration();
            configuration.Applications.Add(new ApplicationDefinition
            {
                Id = "fixture", Name = "Fixture", Executable = Fixture(), Arguments = "--mode success",
                PatientMode = PatientMode.Required, PatientTransport = PatientTransport.Environment,
                PatientEnvironmentKey = "RUNNER_PATIENT_ID", Enabled = true
            });
            return new MainViewModel(configuration, patients, store, protector);
        }

        private static ApplicationCardViewModel Ready(MainViewModel viewModel)
        {
            var card = viewModel.Applications.Single();
            viewModel.UpdateApplicationReadiness(card.Id, new PathProbeResult(PathReadiness.Ready, "Ready"));
            return card;
        }

        private static string Fixture()
        {
            return TestHarness.BuiltArtifact("RunnerFixture.exe",
                "tests/RunnerFixture/bin/x64/Debug/RunnerFixture.exe");
        }

        private static void WaitForTerminal(ActivityRowViewModel row)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while ((row.State == LaunchHistoryState.Starting || row.State == LaunchHistoryState.Running) && DateTime.UtcNow < deadline)
                Thread.Sleep(20);
            TestHarness.AssertFalse(row.State == LaunchHistoryState.Starting || row.State == LaunchHistoryState.Running, "Timed out waiting for launch activity.");
        }

        private static void WaitForFile(string path)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!File.Exists(path) && DateTime.UtcNow < deadline) Thread.Sleep(20);
            TestHarness.AssertTrue(File.Exists(path), "Timed out waiting for fixture capture.");
        }

        private static void WaitForCondition(Func<bool> condition, string failureMessage)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!condition() && DateTime.UtcNow < deadline) Thread.Sleep(20);
            TestHarness.AssertTrue(condition(), failureMessage);
        }

        private sealed class QueueingSynchronizationContext : SynchronizationContext
        {
            private readonly Queue<SendOrPostCallback> callbacks = new Queue<SendOrPostCallback>();

            public int PendingCount
            {
                get { lock (callbacks) return callbacks.Count; }
            }

            public override void Post(SendOrPostCallback callback, object state)
            {
                lock (callbacks) callbacks.Enqueue(ignored => callback(state));
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

        private static void WithHistory(Action<LaunchHistoryStore, string> action)
        {
            var directory = Path.Combine(Path.GetTempPath(), "runner-hub-view-history-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try { action(new LaunchHistoryStore(Path.Combine(directory, "history.json"), 30, 100), directory); }
            finally { DeleteDirectoryWithRetry(directory); }
        }

        private static void DeleteDirectoryWithRetry(string directory)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    Directory.Delete(directory, true);
                    return;
                }
                catch (IOException) when (attempt < 19)
                {
                    Thread.Sleep(25);
                }
                catch (UnauthorizedAccessException) when (attempt < 19)
                {
                    Thread.Sleep(25);
                }
            }
        }
    }
}
