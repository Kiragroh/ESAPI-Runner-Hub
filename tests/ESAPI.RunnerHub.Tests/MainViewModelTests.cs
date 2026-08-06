using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using EsapiRunnerHub.Catalog;
using EsapiRunnerHub.Configuration;
using EsapiRunnerHub.History;
using EsapiRunnerHub.Launching;
using EsapiRunnerHub.Patients;
using EsapiRunnerHub.Privacy;
using EsapiRunnerHub.ViewModels;

namespace EsapiRunnerHub.Tests
{
    internal static class MainViewModelTests
    {
        public static void Register()
        {
            TestHarness.Test("main view model retains and clears selected patient", RetainsPatientContext);
            TestHarness.Test("optional and required cards follow patient context", AppliesPatientModes);
            TestHarness.Test("catalogue filters by category and text", FiltersCatalogue);
            TestHarness.Test("catalogue combines artifact category and text filters", CombinesCatalogueFilters);
            TestHarness.Test("STR Hub README command copies the reachable link", CopiesReadmeLink);
            TestHarness.Test("offline ESAPI state stays explicit", ShowsOfflineState);
            TestHarness.Test("Eclipse plug-in cards are visible but never launched externally", KeepsPluginInsideEclipse);
            TestHarness.Test("successful child launches and exits are logged technically", LogsChildLifecycle);
            TestHarness.Test("privacy display mode is explicit and temporary", TogglesPrivacyDisplay);
            TestHarness.Test("catalogue filters reset together", ResetsCatalogueFilters);
        }

        private static void RetainsPatientContext()
        {
            var patient = new PatientRecord("SYN-1001", "Ada", "Example", 0);
            var viewModel = CreateViewModel();

            viewModel.SelectPatient(patient);
            TestHarness.AssertEqual("SYN-1001", viewModel.SelectedPatient.Id);
            viewModel.ClearPatient();
            TestHarness.AssertEqual<PatientRecord>(null, viewModel.SelectedPatient);
        }

        private static void AppliesPatientModes()
        {
            var viewModel = CreateViewModel();
            foreach (var card in viewModel.Applications)
            {
                card.SetReadiness(PathReadiness.Ready, "Ready");
            }

            var optional = viewModel.Applications.Single(card => card.Id == "optional");
            var required = viewModel.Applications.Single(card => card.Id == "required");
            TestHarness.AssertTrue(optional.CanStartWithoutPatient);
            TestHarness.AssertFalse(optional.CanStartWithPatient);
            TestHarness.AssertFalse(required.CanStartWithPatient);

            viewModel.SelectPatient(new PatientRecord("SYN-1001", "Ada", "Example", 0));
            TestHarness.AssertTrue(optional.CanStartWithPatient);
            TestHarness.AssertTrue(optional.CanStartWithoutPatient);
            TestHarness.AssertTrue(required.CanStartWithPatient);
        }

        private static void FiltersCatalogue()
        {
            var viewModel = CreateViewModel();
            viewModel.SelectedCategory = "Review";
            TestHarness.AssertEqual(1, viewModel.VisibleApplications.Count);
            TestHarness.AssertEqual("required", viewModel.VisibleApplications[0].Id);

            viewModel.SelectedCategory = "All categories";
            viewModel.ApplicationFilter = "document";
            TestHarness.AssertEqual(1, viewModel.VisibleApplications.Count);
            TestHarness.AssertEqual("optional", viewModel.VisibleApplications[0].Id);
        }

        private static void CombinesCatalogueFilters()
        {
            var configuration = new HubConfiguration();
            configuration.Applications.Add(new ApplicationDefinition
            {
                Id = "source-discovery",
                Name = "Patient Data Discovery",
                Category = "Scripts",
                Executable = @"plugins\PatientDataDiscovery.cs",
                Enabled = true
            });
            configuration.Applications.Add(new ApplicationDefinition
            {
                Id = "binary-discovery",
                Name = "Plan Discovery",
                Category = "Scripts",
                Executable = @"plugins\PlanDiscovery.esapi.dll",
                Enabled = true
            });
            configuration.Applications.Add(new ApplicationDefinition
            {
                Id = "source-review",
                Name = "Plan Review",
                Category = "Review",
                Executable = @"plugins\PlanReview.cs",
                Enabled = true
            });

            var viewModel = new MainViewModel(configuration, new List<PatientRecord>());
            viewModel.SelectedArtifactFilter = viewModel.ArtifactFilters.Single(item => item.Kind == ApplicationArtifactFilter.SingleFile);
            viewModel.SelectedCategory = "Scripts";
            viewModel.ApplicationFilter = "discovery";

            TestHarness.AssertEqual(1, viewModel.VisibleApplications.Count);
            TestHarness.AssertEqual("source-discovery", viewModel.VisibleApplications[0].Id);
        }

        private static void CopiesReadmeLink()
        {
            var configuration = new HubConfiguration();
            configuration.Hub.StrHubBaseUrl = "http://10.100.86.9:5173/";
            configuration.Applications.Add(new ApplicationDefinition
            {
                Id = "linked",
                Name = "Linked",
                Executable = "linked.exe",
                HubScriptId = 62,
                Enabled = true
            });
            configuration.Applications.Add(new ApplicationDefinition
            {
                Id = "unlinked",
                Name = "Unlinked",
                Executable = "unlinked.exe",
                Enabled = true
            });
            string clipboardText = null;
            var viewModel = new MainViewModel(configuration, new List<PatientRecord>(), null,
                new ProtectedContextEnvelope(), text => clipboardText = text);
            var linked = viewModel.Applications.Single(item => item.Id == "linked");
            var unlinked = viewModel.Applications.Single(item => item.Id == "unlinked");

            TestHarness.AssertTrue(viewModel.CopyReadmeLinkCommand.CanExecute(linked));
            TestHarness.AssertFalse(viewModel.CopyReadmeLinkCommand.CanExecute(unlinked));
            viewModel.CopyReadmeLinkCommand.Execute(linked);
            TestHarness.AssertEqual("http://10.100.86.9:5173/#/inhouse/62", clipboardText);
            TestHarness.AssertContains(viewModel.NotificationText, "copied");
        }

        private static void ShowsOfflineState()
        {
            var viewModel = CreateViewModel();
            viewModel.SetEsapiStatus(false, "Offline · application catalogue remains available");
            TestHarness.AssertFalse(viewModel.IsEsapiAvailable);
            TestHarness.AssertContains(viewModel.EsapiStatusText, "Offline");
        }

        private static void KeepsPluginInsideEclipse()
        {
            var configuration = IniConfigurationStore.ParseText(@"
[Application.plugin]
Name=Plan FieldNamer
Category=Eclipse plug-ins
Description=Runs inside Eclipse.
Executable=plugins\Plan_FieldNamer.esapi.dll
LaunchKind=EclipsePlugin
PatientMode=None
PatientTransport=None
Enabled=true
", @"C:\portable\settings.ini");
            var viewModel = new MainViewModel(configuration, new List<PatientRecord>());
            var card = viewModel.Applications.Single();
            card.SetReadiness(PathReadiness.Ready, "Ready");
            var referenceVisibility = card.GetType().GetProperty("ReferenceVisibility");
            var withoutPatientVisibility = card.GetType().GetProperty("WithoutPatientVisibility");

            TestHarness.AssertFalse(card.CanStartWithoutPatient);
            TestHarness.AssertFalse(card.CanStartWithPatient);
            TestHarness.AssertEqual("Collapsed", withoutPatientVisibility.GetValue(card, null).ToString());
            TestHarness.AssertTrue(referenceVisibility != null, "ApplicationCardViewModel.ReferenceVisibility is missing.");
            TestHarness.AssertEqual("Visible", referenceVisibility.GetValue(card, null).ToString());
            TestHarness.AssertContains(card.ModeLabel, "Eclipse");

            viewModel.StartWithoutPatientCommand.Execute(card);
            TestHarness.AssertEqual(0, viewModel.Activities.Count);
            TestHarness.AssertContains(viewModel.NotificationText, "inside Eclipse");
        }

        private static void LogsChildLifecycle()
        {
            var directory = Path.Combine(Path.GetTempPath(), "runner-hub-lifecycle-" + Guid.NewGuid().ToString("N"));
            try
            {
                TechnicalLog.Configure(directory);
                var fixture = TestHarness.PathFromRoot("tests/RunnerFixture/bin/x64/Release/RunnerFixture.exe");
                var configuration = new HubConfiguration();
                configuration.Applications.Add(new ApplicationDefinition
                {
                    Id = "lifecycle",
                    Name = "Lifecycle Fixture",
                    Executable = fixture,
                    Arguments = "--mode success",
                    PatientMode = PatientMode.None,
                    PatientTransport = PatientTransport.None,
                    Enabled = true
                });
                var viewModel = new MainViewModel(configuration, new List<PatientRecord>());
                var card = viewModel.Applications.Single();
                card.SetReadiness(PathReadiness.Ready, "Ready");

                viewModel.StartWithoutPatientCommand.Execute(card);
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while ((viewModel.Activities.Single().State == EsapiRunnerHub.History.LaunchHistoryState.Starting ||
                        viewModel.Activities.Single().State == EsapiRunnerHub.History.LaunchHistoryState.Running) && DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(20);
                }

                var log = WaitForSharedLog(TechnicalLog.Current.FilePath, "child_exit_ok", deadline);
                TestHarness.AssertContains(log, "child_started");
                TestHarness.AssertContains(log, "child_exit_ok");
                TestHarness.AssertContains(log, "app=lifecycle");
            }
            finally
            {
                TechnicalLog.Configure(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ESAPI Runner Hub", "Logs"));
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static void TogglesPrivacyDisplay()
        {
            var viewModel = CreateViewModel();
            var enabledProperty = typeof(MainViewModel).GetProperty("IsPrivacyBlurEnabled");
            var labelProperty = typeof(MainViewModel).GetProperty("PrivacyActionLabel");
            var commandProperty = typeof(MainViewModel).GetProperty("TogglePrivacyBlurCommand");
            TestHarness.AssertTrue(enabledProperty != null, "MainViewModel.IsPrivacyBlurEnabled is missing.");
            TestHarness.AssertTrue(labelProperty != null, "MainViewModel.PrivacyActionLabel is missing.");
            TestHarness.AssertTrue(commandProperty != null, "MainViewModel.TogglePrivacyBlurCommand is missing.");

            TestHarness.AssertFalse((bool)enabledProperty.GetValue(viewModel, null));
            TestHarness.AssertEqual("Privacy blur", (string)labelProperty.GetValue(viewModel, null));
            ((System.Windows.Input.ICommand)commandProperty.GetValue(viewModel, null)).Execute(null);
            TestHarness.AssertTrue((bool)enabledProperty.GetValue(viewModel, null));
            TestHarness.AssertEqual("Show details", (string)labelProperty.GetValue(viewModel, null));
        }

        private static void ResetsCatalogueFilters()
        {
            var viewModel = CreateViewModel();
            var activeProperty = typeof(MainViewModel).GetProperty("HasActiveFilters");
            var commandProperty = typeof(MainViewModel).GetProperty("ResetFiltersCommand");
            TestHarness.AssertTrue(activeProperty != null, "MainViewModel.HasActiveFilters is missing.");
            TestHarness.AssertTrue(commandProperty != null, "MainViewModel.ResetFiltersCommand is missing.");

            viewModel.ApplicationFilter = "plan";
            viewModel.SelectedCategory = "Review";
            viewModel.SelectedArtifactFilter = viewModel.ArtifactFilters.Single(item => item.Kind == ApplicationArtifactFilter.Binary);
            TestHarness.AssertTrue((bool)activeProperty.GetValue(viewModel, null));

            ((System.Windows.Input.ICommand)commandProperty.GetValue(viewModel, null)).Execute(null);

            TestHarness.AssertEqual(string.Empty, viewModel.ApplicationFilter);
            TestHarness.AssertEqual("All categories", viewModel.SelectedCategory);
            TestHarness.AssertEqual(ApplicationArtifactFilter.All, viewModel.SelectedArtifactFilter.Kind);
            TestHarness.AssertFalse((bool)activeProperty.GetValue(viewModel, null));
        }

        private static string WaitForSharedLog(string path, string expected, DateTime deadline)
        {
            while (DateTime.UtcNow < deadline)
            {
                if (File.Exists(path))
                {
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var reader = new StreamReader(stream))
                    {
                        var text = reader.ReadToEnd();
                        if (text.Contains(expected))
                        {
                            return text;
                        }
                    }
                }

                Thread.Sleep(20);
            }

            throw new InvalidOperationException("Timed out waiting for technical log event: " + expected);
        }

        private static MainViewModel CreateViewModel()
        {
            var configuration = new HubConfiguration();
            configuration.Applications.Add(new ApplicationDefinition
            {
                Id = "optional",
                Name = "Document Runner",
                Category = "Documents",
                PatientMode = PatientMode.Optional,
                PatientTransport = PatientTransport.Argument,
                PatientArgumentTemplate = "--patient-id {PatientId}",
                Executable = "document.exe",
                Enabled = true
            });
            configuration.Applications.Add(new ApplicationDefinition
            {
                Id = "required",
                Name = "Plan Review",
                Category = "Review",
                PatientMode = PatientMode.Required,
                PatientTransport = PatientTransport.Environment,
                PatientEnvironmentKey = "RUNNER_PATIENT_ID",
                Executable = "review.exe",
                Enabled = true
            });

            return new MainViewModel(configuration, new List<PatientRecord>());
        }
    }
}
