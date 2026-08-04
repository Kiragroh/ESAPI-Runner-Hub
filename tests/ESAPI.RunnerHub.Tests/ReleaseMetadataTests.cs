using System;
using System.IO;
using System.Linq;

namespace EsapiRunnerHub.Tests
{
    internal static class ReleaseMetadataTests
    {
        public static void Register()
        {
            TestHarness.Test("release metadata identifies version 0.1.3 build 4", HasReleaseMetadata);
            TestHarness.Test("release build never deletes the portable settings directory", PreservesPortableSettingsDirectory);
            TestHarness.Test("release build publishes immutable versioned Citrix binaries", PublishesImmutableCitrixBinary);
            TestHarness.Test("public documentation and example settings contain no clinical paths", PublicFilesArePortable);
            TestHarness.Test("repository contains no vendor assemblies", HasNoVendorBinaries);
        }

        private static void HasReleaseMetadata()
        {
            var version = File.ReadAllText(TestHarness.PathFromRoot("versionInfo.json"));
            var changelog = File.ReadAllText(TestHarness.PathFromRoot("CHANGELOG.md"));
            var license = File.ReadAllText(TestHarness.PathFromRoot("LICENSE"));
            var assemblyInfo = File.ReadAllText(TestHarness.PathFromRoot("src/ESAPI.RunnerHub/Properties/AssemblyInfo.cs"));

            TestHarness.AssertContains(version, "\"version\": \"0.1.3\"");
            TestHarness.AssertContains(version, "\"build\": 4");
            TestHarness.AssertContains(changelog, "## [0.1.3] - 2026-08-04");
            TestHarness.AssertContains(assemblyInfo, "AssemblyVersion(\"0.1.3.0\")");
            TestHarness.AssertContains(assemblyInfo, "AssemblyFileVersion(\"0.1.3.0\")");
            TestHarness.AssertContains(license, "MIT License");
        }

        private static void PreservesPortableSettingsDirectory()
        {
            var script = File.ReadAllText(TestHarness.PathFromRoot("tools/build-release.ps1"));
            TestHarness.AssertFalse(script.Contains("Remove-Item -LiteralPath $distRoot -Recurse"),
                "The release build must not recursively delete dist because it owns the live settings.ini.");
        }

        private static void PublishesImmutableCitrixBinary()
        {
            var script = File.ReadAllText(TestHarness.PathFromRoot("tools/build-release.ps1"));
            TestHarness.AssertContains(script, "'dist\\versions'");
            TestHarness.AssertContains(script, "ESAPI-Runner-Hub.v$version.exe");
            TestHarness.AssertContains(script, "Existing versioned binary has a different SHA-256");
            TestHarness.AssertFalse(
                script.Contains("-Destination (Join-Path $distRoot 'ESAPI-Runner-Hub.exe')"),
                "The release build must not overwrite the Citrix-published legacy path.");
        }

        private static void PublicFilesArePortable()
        {
            var publicText = File.ReadAllText(TestHarness.PathFromRoot("README.md")) +
                             File.ReadAllText(TestHarness.PathFromRoot("settings.example.ini"));
            TestHarness.AssertFalse(publicText.IndexOf(@"C:\Users\", StringComparison.OrdinalIgnoreCase) >= 0);
            TestHarness.AssertFalse(publicText.IndexOf(@"\\medizin.uni-leipzig.de", StringComparison.OrdinalIgnoreCase) >= 0);
            TestHarness.AssertFalse(publicText.IndexOf("SYN-100", StringComparison.OrdinalIgnoreCase) >= 0,
                "Synthetic patient identifiers belong only in tests and UI smoke data.");
        }

        private static void HasNoVendorBinaries()
        {
            var root = Path.GetDirectoryName(TestHarness.PathFromRoot("ESAPI-Runner-Hub.sln"));
            var forbidden = Directory.EnumerateFiles(root, "*.dll", SearchOption.AllDirectories)
                .Where(path => path.IndexOf("\\bin\\", StringComparison.OrdinalIgnoreCase) < 0 &&
                               path.IndexOf("\\obj\\", StringComparison.OrdinalIgnoreCase) < 0)
                .Where(path => Path.GetFileName(path).StartsWith("VMS.TPS.", StringComparison.OrdinalIgnoreCase))
                .ToList();
            TestHarness.AssertEqual(0, forbidden.Count);
        }
    }
}
