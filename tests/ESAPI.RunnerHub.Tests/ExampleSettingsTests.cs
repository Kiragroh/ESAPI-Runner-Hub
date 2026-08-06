using System.Linq;
using EsapiRunnerHub.Configuration;

namespace EsapiRunnerHub.Tests
{
    internal static class ExampleSettingsTests
    {
        public static void Register()
        {
            TestHarness.Test("example settings cover standalone binary and source launches", CoversAllArtifactKinds);
            TestHarness.Test("example settings include direct UKL export tools", IncludesDirectExportTools);
        }

        private static void IncludesDirectExportTools()
        {
            var configuration = IniConfigurationStore.Load(TestHarness.PathFromRoot("settings.example.ini"));
            var source = configuration.Applications.Single(item => item.Id == "dicom-collection-ukl-direct");
            var binary = configuration.Applications.Single(item => item.Id == "plans-quicker-direct");

            TestHarness.AssertEqual(LaunchKind.EsapiContextScript, source.LaunchKind);
            TestHarness.AssertEqual(ApplicationArtifactKind.SingleFile, source.ArtifactKind);
            TestHarness.AssertEqual(ContextRequirement.Patient, source.ContextRequirement);
            TestHarness.AssertEqual(LaunchKind.EsapiContextScript, binary.LaunchKind);
            TestHarness.AssertEqual(ApplicationArtifactKind.Binary, binary.ArtifactKind);
            TestHarness.AssertEqual(ContextRequirement.Patient, binary.ContextRequirement);
            TestHarness.AssertEqual(ScriptEngine.Eclipse, binary.ScriptEngine);
        }

        private static void CoversAllArtifactKinds()
        {
            var path = TestHarness.PathFromRoot("settings.example.ini");
            var configuration = IniConfigurationStore.Load(path);
            var standalone = configuration.Applications.Single(item => item.Id == "classic-runner");
            var binary = configuration.Applications.Single(item => item.Id == "direct-binary");
            var source = configuration.Applications.Single(item => item.Id == "direct-source");

            TestHarness.AssertEqual(ApplicationArtifactKind.Standalone, standalone.ArtifactKind);
            TestHarness.AssertEqual(LaunchKind.EsapiContextScript, binary.LaunchKind);
            TestHarness.AssertEqual(ApplicationArtifactKind.Binary, binary.ArtifactKind);
            TestHarness.AssertEqual(ApplicationAccessMode.ReadOnly, binary.AccessMode);
            TestHarness.AssertEqual(ContextRequirement.PlanningItem, binary.ContextRequirement);
            TestHarness.AssertEqual(LaunchKind.EsapiContextScript, source.LaunchKind);
            TestHarness.AssertEqual(ApplicationArtifactKind.SingleFile, source.ArtifactKind);
            TestHarness.AssertEqual(ApplicationAccessMode.ReadOnly, source.AccessMode);
            TestHarness.AssertTrue(configuration.Validate().IsValid, string.Join("; ", configuration.Validate().Errors));
        }
    }
}
