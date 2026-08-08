using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace EsapiRunnerHub.Tests
{
    internal static class ReleaseMetadataTests
    {
        public static void Register()
        {
            TestHarness.Test("release metadata identifies version 0.3.5 build 24", HasReleaseMetadata);
            TestHarness.Test("release build never deletes the portable settings directory", PreservesPortableSettingsDirectory);
            TestHarness.Test("release build publishes immutable versioned Citrix binaries", PublishesImmutableCitrixBinary);
            TestHarness.Test("release build executes the Citrix launcher contract", ExecutesCitrixLauncherContract);
            TestHarness.Test("release build uses an isolated staging output", UsesIsolatedBuildOutput);
            TestHarness.Test("release build leaves byte-identical locked live files untouched", SkipsIdenticalLiveFiles);
            TestHarness.Test("Hub-only releases preserve stable helper binaries", PreservesStableComponents);
            TestHarness.Test("public documentation and example settings contain no clinical paths", PublicFilesArePortable);
            TestHarness.Test("repository contains no vendor assemblies", HasNoVendorBinaries);
            TestHarness.Test("entry assembly declares the ESAPI runtime authorization reference", DeclaresEsapiAuthorizationReference);
            TestHarness.Test("release validator permits API metadata but rejects vendor binaries", AllowsMetadataReferenceOnly);
            TestHarness.Test("release build references Eclipse 18 assets", UsesEclipse18Assets);
            TestHarness.Test("release packages the isolated script host beside the Hub", PackagesScriptHost);
            TestHarness.Test("release packages the context debugging guide", PackagesContextDebuggingGuide);
            TestHarness.Test("isolated script host has no runtime dependency on the Hub executable", ScriptHostIsRuntimeIndependent);
        }

        private static void HasReleaseMetadata()
        {
            var version = File.ReadAllText(TestHarness.PathFromRoot("versionInfo.json"));
            var changelog = File.ReadAllText(TestHarness.PathFromRoot("CHANGELOG.md"));
            var license = File.ReadAllText(TestHarness.PathFromRoot("LICENSE"));
            var assemblyInfo = File.ReadAllText(TestHarness.PathFromRoot("src/ESAPI.RunnerHub/Properties/AssemblyInfo.cs"));
            var hostAssemblyInfo = File.ReadAllText(TestHarness.PathFromRoot("src/ESAPI.ScriptHost/Properties/AssemblyInfo.cs"));

            TestHarness.AssertContains(version, "\"version\": \"0.3.5\"");
            TestHarness.AssertContains(version, "\"build\": 24");
            TestHarness.AssertContains(version, "\"scriptHostVersion\": \"0.3.3\"");
            TestHarness.AssertContains(version, "\"writeScriptHostVersion\": \"0.3.4\"");
            TestHarness.AssertContains(version, "\"citrixLauncherVersion\": \"0.3.3\"");
            TestHarness.AssertContains(version, "\"build\": 22");
            TestHarness.AssertContains(version, "\"build\": 21");
            TestHarness.AssertContains(version, "\"build\": 20");
            TestHarness.AssertContains(version, "\"build\": 19");
            TestHarness.AssertContains(version, "\"build\": 18");
            TestHarness.AssertContains(version, "\"build\": 17");
            TestHarness.AssertContains(version, "\"build\": 16");
            TestHarness.AssertContains(version, "\"build\": 15");
            TestHarness.AssertContains(version, "\"build\": 14");
            TestHarness.AssertContains(version, "\"build\": 11");
            TestHarness.AssertContains(changelog, "## [0.3.5] - 2026-08-08");
            TestHarness.AssertContains(changelog, "compile-time ESAPI calls");
            TestHarness.AssertContains(changelog, "Child process exits");
            TestHarness.AssertContains(changelog, "Select patient");
            TestHarness.AssertContains(changelog, "encrypted launch history");
            TestHarness.AssertContains(changelog, "Direct context scripts");
            TestHarness.AssertContains(assemblyInfo, "AssemblyVersion(\"0.3.5.0\")");
            TestHarness.AssertContains(assemblyInfo, "AssemblyFileVersion(\"0.3.5.0\")");
            TestHarness.AssertContains(assemblyInfo, "AssemblyInformationalVersion(\"0.3.5\")");
            TestHarness.AssertContains(hostAssemblyInfo, "AssemblyVersion(\"0.3.3.0\")");
            TestHarness.AssertContains(hostAssemblyInfo, "AssemblyFileVersion(\"0.3.3.0\")");
            TestHarness.AssertContains(hostAssemblyInfo, "AssemblyInformationalVersion(\"0.3.3\")");
            var currentCitrixBinary = File.ReadAllText(TestHarness.PathFromRoot("citrix/current.txt")).Trim();
            TestHarness.AssertTrue(currentCitrixBinary.StartsWith("ESAPI-Runner-Hub.v", StringComparison.Ordinal));
            TestHarness.AssertTrue(currentCitrixBinary.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
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

        private static void ExecutesCitrixLauncherContract()
        {
            var script = File.ReadAllText(TestHarness.PathFromRoot("tools/build-release.ps1"));
            TestHarness.AssertContains(script, "Test-CitrixLauncher.ps1");
            TestHarness.AssertContains(script, "Citrix launcher tests failed with exit code");
            TestHarness.AssertContains(script, "-FixturePath");
            TestHarness.AssertContains(
                File.ReadAllText(TestHarness.PathFromRoot("tests/Test-CitrixLauncher.ps1")),
                "[string]$FixturePath");
        }

        private static void UsesIsolatedBuildOutput()
        {
            var script = File.ReadAllText(TestHarness.PathFromRoot("tools/build-release.ps1"));
            TestHarness.AssertContains(script, "$buildOutput");
            TestHarness.AssertContains(script, "$testBuildOutput");
            TestHarness.AssertContains(script, "/p:OutputPath=");
            TestHarness.AssertContains(script, "Join-Path $testBuildOutput 'ESAPI.RunnerHub.Tests.exe'");
            TestHarness.AssertContains(script, "Join-Path $buildOutput 'ESAPI-Runner-Hub.exe'");
            TestHarness.AssertFalse(
                script.Contains("src\\ESAPI.RunnerHub\\bin\\x64\\Release\\ESAPI-Runner-Hub.exe"),
                "Release packaging must not depend on a mutable or locked shared bin output.");
        }

        private static void SkipsIdenticalLiveFiles()
        {
            var script = File.ReadAllText(TestHarness.PathFromRoot("tools/build-release.ps1"));
            var start = script.IndexOf("function Copy-FileWithRetry", StringComparison.Ordinal);
            var end = script.IndexOf("function Publish-ImmutableBinary", StringComparison.Ordinal);
            TestHarness.AssertTrue(start >= 0 && end > start);
            var helper = script.Substring(start, end - start);
            TestHarness.AssertContains(helper, "Get-FileHash");
            TestHarness.AssertContains(helper, "destinationHash");
        }

        private static void PreservesStableComponents()
        {
            var script = File.ReadAllText(TestHarness.PathFromRoot("tools/build-release.ps1"));
            var readHostAssembly = File.ReadAllText(TestHarness.PathFromRoot("src/ESAPI.ScriptHost/Properties/AssemblyInfo.cs"));
            var writeHostAssembly = File.ReadAllText(TestHarness.PathFromRoot("src/ESAPI.WriteScriptHost/Properties/AssemblyInfo.cs"));
            var launcherAssembly = File.ReadAllText(TestHarness.PathFromRoot("src/ESAPI.CitrixLauncher/Properties/AssemblyInfo.cs"));

            TestHarness.AssertContains(script, "scriptHostVersion");
            TestHarness.AssertContains(script, "writeScriptHostVersion");
            TestHarness.AssertContains(script, "$expectedReadScriptHostFileVersion");
            TestHarness.AssertContains(script, "$expectedWriteScriptHostFileVersion");
            TestHarness.AssertContains(script, "citrixLauncherVersion");
            TestHarness.AssertContains(script, "Resolve-StableComponentBinary");
            TestHarness.AssertContains(script, "$stableReadHost");
            TestHarness.AssertContains(script, "$stableWriteHost");
            TestHarness.AssertContains(script, "$stableCitrixLauncher");
            TestHarness.AssertContains(script, "Stable component version mismatch");
            TestHarness.AssertContains(readHostAssembly, "AssemblyVersion(\"0.3.3.0\")");
            TestHarness.AssertContains(readHostAssembly, "AssemblyFileVersion(\"0.3.3.0\")");
            TestHarness.AssertContains(writeHostAssembly, "AssemblyVersion(\"0.3.4.0\")");
            TestHarness.AssertContains(writeHostAssembly, "AssemblyFileVersion(\"0.3.4.0\")");
            TestHarness.AssertContains(launcherAssembly, "AssemblyVersion(\"0.3.3.0\")");
            TestHarness.AssertContains(launcherAssembly, "AssemblyFileVersion(\"0.3.3.0\")");
        }

        private static void PublicFilesArePortable()
        {
            var publicText = File.ReadAllText(TestHarness.PathFromRoot("README.md")) +
                             File.ReadAllText(TestHarness.PathFromRoot("settings.example.ini")) +
                             File.ReadAllText(TestHarness.PathFromRoot("docs/CONTEXT_DEBUGGING.md"));
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

        private static void DeclaresEsapiAuthorizationReference()
        {
            var assembly = typeof(EsapiRunnerHub.ViewModels.MainViewModel).Assembly;
            TestHarness.AssertTrue(
                assembly.GetReferencedAssemblies().Any(name =>
                    string.Equals(name.Name, "VMS.TPS.Common.Model.API", StringComparison.OrdinalIgnoreCase)),
                "ESAPI 16.1 rejects reflection-only entry assemblies with UnauthorizedScriptingAPIAccessException.");
        }

        private static void AllowsMetadataReferenceOnly()
        {
            var validator = File.ReadAllText(TestHarness.PathFromRoot("tools/validate-vendor-free.ps1"));
            TestHarness.AssertFalse(validator.Contains("Forbidden assembly references"),
                "A metadata-only VMS reference is required for ESAPI standalone authorization.");
            TestHarness.AssertContains(validator, "VMS\\.TPS");
            TestHarness.AssertContains(validator, "forbiddenExtensions");
            TestHarness.AssertContains(validator, "ESAPI-Script-Host.exe.config");
        }

        private static void UsesEclipse18Assets()
        {
            var project = File.ReadAllText(TestHarness.PathFromRoot("src/ESAPI.RunnerHub/ESAPI.RunnerHub.csproj"));
            var build = File.ReadAllText(TestHarness.PathFromRoot("tools/build-release.ps1"));
            TestHarness.AssertContains(project, @"\..\..\..\_Assets");
            TestHarness.AssertFalse(project.Contains(@"\RTM\16.1\"),
                "The Hub must not silently compile against the retired Eclipse 16.1 API.");
            TestHarness.AssertContains(build, "EsapiReferenceDirectory");
            TestHarness.AssertContains(build, "_Assets");
        }

        private static void PackagesScriptHost()
        {
            var build = File.ReadAllText(TestHarness.PathFromRoot("tools/build-release.ps1"));
            var validator = File.ReadAllText(TestHarness.PathFromRoot("tools/validate-vendor-free.ps1"));
            TestHarness.AssertContains(build, "Join-Path $buildOutput 'ESAPI-Script-Host.exe'");
            TestHarness.AssertContains(build, "Join-Path $packageRoot 'ESAPI-Script-Host.exe'");
            TestHarness.AssertContains(build, "Join-Path $distRoot 'ESAPI-Script-Host.exe'");
            TestHarness.AssertContains(build, "Join-Path $buildOutput 'ESAPI-Script-Host.exe.config'");
            TestHarness.AssertContains(build, "Join-Path $packageRoot 'ESAPI-Script-Host.exe.config'");
            TestHarness.AssertContains(build, "Join-Path $distRoot 'ESAPI-Script-Host.exe.config'");
            TestHarness.AssertContains(build, "Join-Path $buildOutput 'ESAPI-Write-Script-Host.exe'");
            TestHarness.AssertContains(build, "Join-Path $packageRoot 'ESAPI-Write-Script-Host.exe'");
            TestHarness.AssertContains(build, "Join-Path $distRoot 'ESAPI-Write-Script-Host.exe'");
            TestHarness.AssertContains(build, "Join-Path $buildOutput 'ESAPI-Write-Script-Host.exe.config'");
            TestHarness.AssertContains(build, "Join-Path $packageRoot 'ESAPI-Write-Script-Host.exe.config'");
            TestHarness.AssertContains(build, "Join-Path $distRoot 'ESAPI-Write-Script-Host.exe.config'");
            TestHarness.AssertContains(validator, "'ESAPI-Script-Host.exe'");
            TestHarness.AssertContains(validator, "'ESAPI-Write-Script-Host.exe'");
            TestHarness.AssertContains(validator, "VMS\\.TPS");
        }

        private static void PackagesContextDebuggingGuide()
        {
            var build = File.ReadAllText(TestHarness.PathFromRoot("tools/build-release.ps1"));
            TestHarness.AssertContains(build, "docs\\CONTEXT_DEBUGGING.md");
            TestHarness.AssertContains(build, "Join-Path $packageRoot 'docs\\CONTEXT_DEBUGGING.md'");
        }

        private static void ScriptHostIsRuntimeIndependent()
        {
            var project = File.ReadAllText(TestHarness.PathFromRoot("src/ESAPI.ScriptHost/ESAPI.ScriptHost.csproj"));
            TestHarness.AssertFalse(project.Contains("ProjectReference Include=\"..\\ESAPI.RunnerHub\\ESAPI.RunnerHub.csproj\""));
            TestHarness.AssertContains(project, "Compile Include=\"Contracts\\ContextLaunchPayload.cs\"");
        }
    }
}
