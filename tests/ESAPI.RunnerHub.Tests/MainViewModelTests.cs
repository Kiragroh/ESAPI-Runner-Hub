using System.Collections.Generic;
using System.Linq;
using EsapiRunnerHub.Configuration;
using EsapiRunnerHub.Launching;
using EsapiRunnerHub.Patients;
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
            TestHarness.Test("offline ESAPI state stays explicit", ShowsOfflineState);
            TestHarness.Test("Eclipse plug-in cards are visible but never launched externally", KeepsPluginInsideEclipse);
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

            viewModel.SelectedCategory = "All tools";
            viewModel.ApplicationFilter = "document";
            TestHarness.AssertEqual(1, viewModel.VisibleApplications.Count);
            TestHarness.AssertEqual("optional", viewModel.VisibleApplications[0].Id);
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
