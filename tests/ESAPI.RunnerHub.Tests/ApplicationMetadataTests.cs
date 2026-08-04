using EsapiRunnerHub.Catalog;
using EsapiRunnerHub.Configuration;
using EsapiRunnerHub.ViewModels;
using EsapiRunnerHub.Context;
using EsapiRunnerHub.Launching;

namespace EsapiRunnerHub.Tests
{
    internal static class ApplicationMetadataTests
    {
        public static void Register()
        {
            TestHarness.Test("artifact kind is inferred from target extension", InfersArtifactKind);
            TestHarness.Test("explicit catalogue metadata overrides inference", HonorsExplicitMetadata);
            TestHarness.Test("institutional paths are displayed below Physik-Skripte", CompactsInstitutionalPath);
            TestHarness.Test("STR Hub README links require valid configured values", BuildsReadmeLinkSafely);
            TestHarness.Test("application cards expose type access path and README", CardExposesMetadata);
            TestHarness.Test("Eclipse plug-in cards also offer direct context launch", PluginOffersContextLaunch);
        }

        private static void InfersArtifactKind()
        {
            TestHarness.AssertEqual(ApplicationArtifactKind.Standalone,
                ApplicationMetadata.ArtifactFor(Definition("Tool.exe")));
            TestHarness.AssertEqual(ApplicationArtifactKind.SingleFile,
                ApplicationMetadata.ArtifactFor(Definition("ColorCode.cs")));
            TestHarness.AssertEqual(ApplicationArtifactKind.Binary,
                ApplicationMetadata.ArtifactFor(Definition("ClearPlan.esapi.dll")));
        }

        private static void HonorsExplicitMetadata()
        {
            var definition = Definition("unusual.target");
            definition.ArtifactKind = ApplicationArtifactKind.Binary;
            definition.AccessMode = ApplicationAccessMode.WriteEnabled;

            TestHarness.AssertEqual(ApplicationArtifactKind.Binary, ApplicationMetadata.ArtifactFor(definition));
            TestHarness.AssertEqual(ApplicationAccessMode.WriteEnabled, ApplicationMetadata.AccessFor(definition));

            definition.AccessMode = ApplicationAccessMode.Auto;
            definition.LaunchKind = LaunchKind.EsapiContextScript;
            definition.WriteMode = WriteMode.ConfirmSave;
            TestHarness.AssertEqual(ApplicationAccessMode.WriteEnabled, ApplicationMetadata.AccessFor(definition));
            definition.WriteMode = WriteMode.ReadOnly;
            TestHarness.AssertEqual(ApplicationAccessMode.ReadOnly, ApplicationMetadata.AccessFor(definition));
        }

        private static void CompactsInstitutionalPath()
        {
            TestHarness.AssertEqual("Physik-Skripte\\ESAPI-MG\\plugins\\ColorCode.cs",
                ApplicationMetadata.CompactPath(@"\\server\data\Archiv\STR\STR-Physik\11. Scripting\ESAPI-MG\plugins\ColorCode.cs"));
            TestHarness.AssertEqual("Tool.exe", ApplicationMetadata.CompactPath(@"C:\Tools\Tool.exe"));
        }

        private static void BuildsReadmeLinkSafely()
        {
            var valid = ApplicationMetadata.BuildReadmeUri("https://str-hub.example/", 62);
            TestHarness.AssertEqual("https://str-hub.example/#/inhouse/62", valid.AbsoluteUri);
            TestHarness.AssertEqual(null, ApplicationMetadata.BuildReadmeUri(string.Empty, 62));
            TestHarness.AssertEqual(null, ApplicationMetadata.BuildReadmeUri("not-a-url", 62));
            TestHarness.AssertEqual(null, ApplicationMetadata.BuildReadmeUri("https://str-hub.example/", 0));
        }

        private static void CardExposesMetadata()
        {
            var definition = Definition(@"\\server\data\Archiv\STR\STR-Physik\11. Scripting\ESAPI-MG\plugins\ColorCode.cs");
            definition.AccessMode = ApplicationAccessMode.ReadOnly;
            definition.HubScriptId = 59;
            var card = new ApplicationCardViewModel(definition, "https://str-hub.example/");

            TestHarness.AssertEqual("Single-file (.cs)", card.ArtifactLabel);
            TestHarness.AssertEqual("Read-only", card.AccessLabel);
            TestHarness.AssertTrue(card.CompactPath.StartsWith("Physik-Skripte\\"));
            TestHarness.AssertTrue(card.HasHubReadme);
            TestHarness.AssertEqual(System.Windows.Visibility.Collapsed, card.ContextVisibility);
        }

        private static void PluginOffersContextLaunch()
        {
            var definition = Definition(@"C:\Scripts\Tool.esapi.dll");
            definition.LaunchKind = LaunchKind.EclipsePlugin;
            definition.ContextRequirement = ContextRequirement.Plan;
            definition.PatientMode = PatientMode.Required;
            var card = new ApplicationCardViewModel(definition);
            card.SetReadiness(PathReadiness.Ready, "Ready");
            card.SetContext(new ContextSelection { PatientId = "SYN-1001", PlanId = "P1" });

            TestHarness.AssertEqual(System.Windows.Visibility.Visible, card.ReferenceVisibility);
            TestHarness.AssertEqual(System.Windows.Visibility.Visible, card.ContextVisibility);
            TestHarness.AssertTrue(card.CanStartContext);
        }

        private static ApplicationDefinition Definition(string executable)
        {
            return new ApplicationDefinition { Id = "tool", Name = "Tool", Executable = executable };
        }
    }
}
