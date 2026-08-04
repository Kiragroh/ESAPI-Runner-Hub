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
            TestHarness.Test("patient relaunch uses protected patient and current definition", RelaunchesPatient);
            TestHarness.Test("context relaunch retains exact protected planning selection", RelaunchesContext);
            TestHarness.Test("removed application history is unavailable", MarksRemovedApplicationsUnavailable);
            TestHarness.Test("failed start remains retryable when target returns", RetriesFailedStart);
            TestHarness.Test("replay command refreshes when asynchronous readiness arrives", RefreshesReplayCommand);
            TestHarness.Test("replay rows explain every unavailable state", ExplainsReplayAvailability);
            TestHarness.Test("running activity cannot be replayed until terminal", DisablesReplayWhileRunning);
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
                entry.State = LaunchHistoryState.Running;
                store.Save(new[] { entry });
                var viewModel = CreateStandaloneViewModel(store, Fixture(), "--mode success");
                var row = viewModel.Activities.Single();

                viewModel.UpdateApplicationReadiness("fixture", new PathProbeResult(PathReadiness.Ready, "Ready"));
                TestHarness.AssertFalse(viewModel.RunAgainCommand.CanExecute(row));
                TestHarness.AssertEqual("The application is still running", ReplayText(row));

                row.Entry.State = LaunchHistoryState.Exited;
                row.Refresh();
                viewModel.UpdateApplicationReadiness("fixture", new PathProbeResult(PathReadiness.Ready, "Ready"));

                TestHarness.AssertTrue(viewModel.RunAgainCommand.CanExecute(row));
                TestHarness.AssertEqual("Ready to run again", ReplayText(row));
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

        private static ApplicationCardViewModel Ready(MainViewModel viewModel)
        {
            var card = viewModel.Applications.Single();
            viewModel.UpdateApplicationReadiness(card.Id, new PathProbeResult(PathReadiness.Ready, "Ready"));
            return card;
        }

        private static string Fixture()
        {
            return TestHarness.PathFromRoot("tests/RunnerFixture/bin/x64/Debug/RunnerFixture.exe");
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
