using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
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
            TestHarness.Test("shared plugin paths are displayed below plugins", CompactsSharedPluginPath);
            TestHarness.Test("documentation Hub links require valid configured values", BuildsReadmeLinkSafely);
            TestHarness.Test("application cards expose type access path and README", CardExposesMetadata);
            TestHarness.Test("Eclipse plug-in cards also offer direct context launch", PluginOffersContextLaunch);
            TestHarness.Test("every application card visibly binds release metadata", CardsDisplayReleaseMetadata);
            TestHarness.Test("unavailable application metadata is shown honestly", MissingMetadataIsExplicit);
            TestHarness.Test("catalogue version overrides file version and survives settings round trip", CatalogueVersionTakesPrecedence);
            TestHarness.Test("path probe reads target version and file modification time without loading it", ProbeReadsTargetMetadata);
            TestHarness.Test("unversioned source still exposes its file modification time", SourceHasTimestampWithoutInventedVersion);
            TestHarness.Test("optional version timeout never disables a confirmed existing target", MetadataTimeoutPreservesReadiness);
            TestHarness.Test("optional version failure never disables a confirmed existing target", MetadataFailurePreservesReadiness);
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
            definition.WriteMode = WriteMode.ExecuteAndDiscard;
            TestHarness.AssertEqual(ApplicationAccessMode.WriteEnabled, ApplicationMetadata.AccessFor(definition));
            definition.WriteMode = WriteMode.ReadOnly;
            TestHarness.AssertEqual(ApplicationAccessMode.ReadOnly, ApplicationMetadata.AccessFor(definition));
        }

        private static void CompactsSharedPluginPath()
        {
            TestHarness.AssertEqual("plugins\\ColorCode.cs",
                ApplicationMetadata.CompactPath(@"\\fileserver\clinical-tools\plugins\ColorCode.cs"));
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
            var definition = Definition(@"\\fileserver\clinical-tools\plugins\ColorCode.cs");
            definition.AccessMode = ApplicationAccessMode.ReadOnly;
            definition.HubScriptId = 59;
            var card = new ApplicationCardViewModel(definition, "https://str-hub.example/");

            TestHarness.AssertEqual("Single-file (.cs)", card.ArtifactLabel);
            TestHarness.AssertEqual("Read-only", card.AccessLabel);
            TestHarness.AssertTrue(card.CompactPath.StartsWith("plugins\\"));
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

        private static void CardsDisplayReleaseMetadata()
        {
            var xaml = File.ReadAllText(TestHarness.PathFromRoot("src/ESAPI.RunnerHub/MainWindow.xaml"));
            TestHarness.AssertContains(xaml, "Text=\"{Binding VersionLabel}\"");
            TestHarness.AssertContains(xaml, "Text=\"{Binding LastChangedLabel}\"");
            TestHarness.AssertContains(xaml, "not the source commit");
        }

        private static void MissingMetadataIsExplicit()
        {
            var card = new ApplicationCardViewModel(Definition("missing-tool.dll"));
            TestHarness.AssertEqual("Version: unavailable", card.VersionLabel);
            TestHarness.AssertEqual("File changed: unavailable", card.LastChangedLabel);
            var probe = new PathProbe().ProbeAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".dll"), 2500).GetAwaiter().GetResult();
            TestHarness.AssertEqual(null, probe.FileVersion);
            TestHarness.AssertEqual(null, probe.FileLastWriteTimeUtc);
        }

        private static void CatalogueVersionTakesPrecedence()
        {
            var path = Path.Combine(Path.GetTempPath(), "runner-version-settings.ini");
            var configuration = IniConfigurationStore.ParseText("[Application.tool]\nName=Tool\nExecutable=Tool.dll\nVersion=dev-20260908\n", path);
            var roundTrip = IniConfigurationStore.ParseText(IniConfigurationStore.Serialize(configuration), path);
            var card = new ApplicationCardViewModel(roundTrip.Applications[0]);
            TestHarness.AssertEqual("Version: dev-20260908 (catalogue)", card.VersionLabel);
            var probe = new PathProbe().ProbeAsync(typeof(ApplicationCardViewModel).Assembly.Location, 2500).GetAwaiter().GetResult();
            card.SetReadiness(probe);
            TestHarness.AssertEqual("Version: dev-20260908 (catalogue)", card.VersionLabel);
        }

        private static void ProbeReadsTargetMetadata()
        {
            var target = typeof(ApplicationCardViewModel).Assembly.Location;
            var expected = FileVersionInfo.GetVersionInfo(target).ProductVersion;
            var result = new PathProbe().ProbeAsync(target, 2500).GetAwaiter().GetResult();
            TestHarness.AssertEqual(PathReadiness.Ready, result.Readiness);
            TestHarness.AssertEqual(expected, result.FileVersion);
            TestHarness.AssertEqual(File.GetLastWriteTimeUtc(target), result.FileLastWriteTimeUtc);
            var card = new ApplicationCardViewModel(Definition(target));
            card.SetReadiness(result);
            TestHarness.AssertEqual("Version: " + expected + " (file)", card.VersionLabel);
        }

        private static void SourceHasTimestampWithoutInventedVersion()
        {
            var target = Path.Combine(Path.GetTempPath(), "runner-source-" + Guid.NewGuid().ToString("N") + ".cs");
            try
            {
                File.WriteAllText(target, "// synthetic metadata fixture only");
                var changed = new DateTime(2026, 9, 8, 10, 11, 12, DateTimeKind.Utc);
                File.SetLastWriteTimeUtc(target, changed);
                var result = new PathProbe().ProbeAsync(target, 2500).GetAwaiter().GetResult();
                TestHarness.AssertEqual(PathReadiness.Ready, result.Readiness);
                TestHarness.AssertEqual(null, result.FileVersion);
                TestHarness.AssertEqual(changed, result.FileLastWriteTimeUtc);
                var card = new ApplicationCardViewModel(Definition(target));
                card.SetReadiness(result);
                TestHarness.AssertEqual("Version: unavailable", card.VersionLabel);
                TestHarness.AssertEqual("File changed: " + changed.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture), card.LastChangedLabel);
                card.SetReadiness(new PathProbeResult(PathReadiness.Unavailable, "Unavailable"));
                TestHarness.AssertEqual("File changed: unavailable", card.LastChangedLabel);
            }
            finally
            {
                File.Delete(target);
            }
        }

        private static void MetadataTimeoutPreservesReadiness()
        {
            var release = new ManualResetEventSlim(false);
            var entered = new ManualResetEventSlim(false);
            try
            {
                var probe = new PathProbe(path => { entered.Set(); release.Wait(); return "late-version"; });
                var pending = probe.ProbeAsync(typeof(ApplicationCardViewModel).Assembly.Location, 300);
                TestHarness.AssertTrue(entered.Wait(2000), "Version reader was not reached.");
                var result = pending.GetAwaiter().GetResult();
                TestHarness.AssertEqual(PathReadiness.Ready, result.Readiness);
                TestHarness.AssertEqual(null, result.FileVersion);
                TestHarness.AssertTrue(result.FileLastWriteTimeUtc.HasValue);
            }
            finally { release.Set(); }
        }

        private static void MetadataFailurePreservesReadiness()
        {
            var probe = new PathProbe(path => { throw new IOException("Synthetic version read failure"); });
            var result = probe.ProbeAsync(typeof(ApplicationCardViewModel).Assembly.Location, 2500).GetAwaiter().GetResult();
            TestHarness.AssertEqual(PathReadiness.Ready, result.Readiness);
            TestHarness.AssertEqual(null, result.FileVersion);
            TestHarness.AssertTrue(result.FileLastWriteTimeUtc.HasValue);
        }

    }
}
