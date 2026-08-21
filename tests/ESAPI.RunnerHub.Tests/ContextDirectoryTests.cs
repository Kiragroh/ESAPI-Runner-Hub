using System;
using System.IO;
using System.Linq;
using EsapiRunnerHub.Esapi;

namespace EsapiRunnerHub.Tests
{
    internal static class ContextDirectoryTests
    {
        public static void Register()
        {
            TestHarness.Test("context loader copies plan and standalone structure set", CopiesDetachedContext);
        }

        private static void CopiesDetachedContext()
        {
            var api = TestHarness.BuiltArtifact("VMS.TPS.Common.Model.API.dll",
                "tests/FakeVms.Api/bin/x64/Debug/VMS.TPS.Common.Model.API.dll");
            var closeMarker = Path.Combine(Path.GetTempPath(), "runner-hub-close-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("FAKE_VMS_CLOSE_MARKER", closeMarker);
            try
            {
                var result = new ReflectionContextDirectoryLoader().Load(api, string.Empty, "SYN-1001");

                TestHarness.AssertTrue(result.IsAvailable);
                TestHarness.AssertEqual("C1", result.Courses.Single().Id);
                TestHarness.AssertEqual("P1", result.Plans.Single().Id);
                TestHarness.AssertEqual("SS1", result.Plans.Single().StructureSetId);
                TestHarness.AssertEqual("IMG1", result.Plans.Single().ImageId);
                TestHarness.AssertEqual("SUM1", result.PlanSums.Single().Id);
                TestHarness.AssertTrue(result.StructureSets.Any(item => item.Id == "SS-ONLY"));
                TestHarness.AssertTrue(File.Exists(closeMarker), "The patient was not closed.");
            }
            finally
            {
                Environment.SetEnvironmentVariable("FAKE_VMS_CLOSE_MARKER", null);
                if (File.Exists(closeMarker)) File.Delete(closeMarker);
            }
        }
    }
}
