using System;
using System.IO;
using EsapiRunnerHub.Esapi;

namespace EsapiRunnerHub.Tests
{
    internal static class EsapiPatientDirectoryTests
    {
        public static void Register()
        {
            TestHarness.Test("ESAPI loader returns an offline result when assemblies are missing", MissingAssemblyIsOffline);
            TestHarness.Test("ESAPI loader copies synthetic patients and disposes application", LoadsAndDisposesApplication);
        }

        private static void MissingAssemblyIsOffline()
        {
            var result = new ReflectionPatientDirectoryLoader().Load("does-not-exist.dll", string.Empty);

            TestHarness.AssertFalse(result.IsAvailable);
            TestHarness.AssertEqual("assembly_missing", result.ErrorCode);
            TestHarness.AssertEqual(0, result.Patients.Count);
        }

        private static void LoadsAndDisposesApplication()
        {
            var apiPath = TestHarness.PathFromRoot("tests/FakeVms.Api/bin/x64/Release/VMS.TPS.Common.Model.API.dll");
            var marker = Path.Combine(Path.GetTempPath(), "esapi-runner-hub-dispose-" + Guid.NewGuid().ToString("N") + ".txt");
            Environment.SetEnvironmentVariable("FAKE_VMS_DISPOSE_MARKER", marker);
            try
            {
                var result = new ReflectionPatientDirectoryLoader().Load(apiPath, string.Empty);

                TestHarness.AssertTrue(result.IsAvailable, result.ErrorMessage);
                TestHarness.AssertEqual(2, result.Patients.Count);
                TestHarness.AssertEqual("SYN-1001", result.Patients[0].Id);
                TestHarness.AssertTrue(File.Exists(marker), "ESAPI application was not disposed.");
            }
            finally
            {
                Environment.SetEnvironmentVariable("FAKE_VMS_DISPOSE_MARKER", null);
                if (File.Exists(marker))
                {
                    File.Delete(marker);
                }
            }
        }
    }
}
