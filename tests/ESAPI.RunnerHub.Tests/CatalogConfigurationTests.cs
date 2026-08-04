using System.IO;
using System.Linq;
using EsapiRunnerHub.Configuration;
using EsapiRunnerHub.ViewModels;

namespace EsapiRunnerHub.Tests
{
    internal static class CatalogConfigurationTests
    {
        public static void Register()
        {
            TestHarness.Test("catalogue metadata and history settings round trip", RoundTripsCatalogueSettings);
            TestHarness.Test("history retention and Hub identifiers are validated", ValidatesHistorySettings);
            TestHarness.Test("settings editor exposes catalogue metadata", SettingsEditorExposesMetadata);
        }

        private static void RoundTripsCatalogueSettings()
        {
            var source = Path.Combine(Path.GetTempPath(), "runner-catalogue-settings.ini");
            var configuration = IniConfigurationStore.ParseText(@"
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
", source);

            var application = configuration.Applications.Single();
            TestHarness.AssertEqual(ApplicationArtifactKind.Binary, application.ArtifactKind);
            TestHarness.AssertEqual(ApplicationAccessMode.ReadOnly, application.AccessMode);
            TestHarness.AssertEqual(62, application.HubScriptId);
            TestHarness.AssertEqual(30, configuration.Hub.HistoryRetentionDays);
            TestHarness.AssertEqual(100, configuration.Hub.HistoryMaxEntries);
            TestHarness.AssertEqual("https://str-hub.example/", configuration.Hub.StrHubBaseUrl);

            var serialized = IniConfigurationStore.Serialize(configuration);
            TestHarness.AssertContains(serialized, "ArtifactKind=Binary");
            TestHarness.AssertContains(serialized, "AccessMode=ReadOnly");
            TestHarness.AssertContains(serialized, "HubScriptId=62");
            TestHarness.AssertContains(serialized, "HistoryRetentionDays=30");
            TestHarness.AssertContains(serialized, "HistoryMaxEntries=100");
        }

        private static void ValidatesHistorySettings()
        {
            var configuration = IniConfigurationStore.ParseText(@"
[Hub]
HistoryRetentionDays=0
HistoryMaxEntries=1001
[Application.bad]
Name=Bad
Executable=Tool.exe
HubScriptId=-1
", Path.Combine(Path.GetTempPath(), "runner-invalid-catalogue.ini"));

            var result = configuration.Validate();
            TestHarness.AssertTrue(result.Errors.Any(item => item.Contains("HistoryRetentionDays")));
            TestHarness.AssertTrue(result.Errors.Any(item => item.Contains("HistoryMaxEntries")));
            TestHarness.AssertTrue(result.Errors.Any(item => item.Contains("HubScriptId")));
        }

        private static void SettingsEditorExposesMetadata()
        {
            var configuration = IniConfigurationStore.ParseText(@"
[Hub]
[Application.tool]
Name=Tool
Executable=Tool.exe
", Path.Combine(Path.GetTempPath(), "runner-settings-editor-catalogue.ini"));
            var viewModel = new SettingsViewModel(configuration, configuration.SourcePath);

            TestHarness.AssertTrue(viewModel.ArtifactKinds.Contains(ApplicationArtifactKind.Binary));
            TestHarness.AssertTrue(viewModel.AccessModes.Contains(ApplicationAccessMode.WriteEnabled));
        }
    }
}
