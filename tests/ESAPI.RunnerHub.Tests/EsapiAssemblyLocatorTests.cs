using System;
using System.IO;
using EsapiRunnerHub.Esapi;

namespace EsapiRunnerHub.Tests
{
    internal static class EsapiAssemblyLocatorTests
    {
        private const string ApiFileName = "VMS.TPS.Common.Model.API.dll";
        private const string TypesFileName = "VMS.TPS.Common.Model.Types.dll";

        public static void Register()
        {
            TestHarness.Test("configured ESAPI assemblies win over installed versions", ConfiguredAssembliesWin);
            TestHarness.Test("ESAPI locator selects the highest installed RTM version", HighestInstalledVersionWins);
            TestHarness.Test("ESAPI locator skips incomplete RTM installations", IncompleteInstallationsAreSkipped);
        }

        private static void ConfiguredAssembliesWin()
        {
            WithTemporaryDirectory(root =>
            {
                var configured = CreateAssemblyPair(Path.Combine(root, "configured"));
                CreateAssemblyPair(Path.Combine(root, "RTM", "18.0", "esapi", "API"));

                var result = new EsapiAssemblyLocator().Locate(configured.Api, configured.Types, new[] { Path.Combine(root, "RTM") });

                TestHarness.AssertTrue(result.IsAvailable);
                TestHarness.AssertEqual("configured", result.Source);
                TestHarness.AssertEqual(configured.Api, result.ApiAssemblyPath);
                TestHarness.AssertEqual(configured.Types, result.TypesAssemblyPath);
            });
        }

        private static void HighestInstalledVersionWins()
        {
            WithTemporaryDirectory(root =>
            {
                var rtmRoot = Path.Combine(root, "RTM");
                CreateAssemblyPair(Path.Combine(rtmRoot, "16.1", "esapi", "API"));
                var newest = CreateAssemblyPair(Path.Combine(rtmRoot, "18.0", "esapi", "API"));

                var result = new EsapiAssemblyLocator().Locate("missing-api.dll", "missing-types.dll", new[] { rtmRoot });

                TestHarness.AssertTrue(result.IsAvailable);
                TestHarness.AssertEqual("auto", result.Source);
                TestHarness.AssertEqual("18.0", result.RtmVersion);
                TestHarness.AssertEqual(newest.Api, result.ApiAssemblyPath);
                TestHarness.AssertEqual(newest.Types, result.TypesAssemblyPath);
            });
        }

        private static void IncompleteInstallationsAreSkipped()
        {
            WithTemporaryDirectory(root =>
            {
                var rtmRoot = Path.Combine(root, "RTM");
                var incomplete = Path.Combine(rtmRoot, "19.0", "esapi", "API");
                Directory.CreateDirectory(incomplete);
                File.WriteAllBytes(Path.Combine(incomplete, ApiFileName), new byte[0]);
                var complete = CreateAssemblyPair(Path.Combine(rtmRoot, "18.0", "esapi", "API"));

                var result = new EsapiAssemblyLocator().Locate("missing-api.dll", "missing-types.dll", new[] { rtmRoot });

                TestHarness.AssertTrue(result.IsAvailable);
                TestHarness.AssertEqual("18.0", result.RtmVersion);
                TestHarness.AssertEqual(complete.Api, result.ApiAssemblyPath);
            });
        }

        private static AssemblyPair CreateAssemblyPair(string directory)
        {
            Directory.CreateDirectory(directory);
            var api = Path.Combine(directory, ApiFileName);
            var types = Path.Combine(directory, TypesFileName);
            File.WriteAllBytes(api, new byte[0]);
            File.WriteAllBytes(types, new byte[0]);
            return new AssemblyPair(api, types);
        }

        private static void WithTemporaryDirectory(Action<string> action)
        {
            var path = Path.Combine(Path.GetTempPath(), "esapi-runner-hub-locator-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            try
            {
                action(path);
            }
            finally
            {
                Directory.Delete(path, true);
            }
        }

        private sealed class AssemblyPair
        {
            public AssemblyPair(string api, string types)
            {
                Api = api;
                Types = types;
            }

            public string Api { get; private set; }
            public string Types { get; private set; }
        }
    }
}
