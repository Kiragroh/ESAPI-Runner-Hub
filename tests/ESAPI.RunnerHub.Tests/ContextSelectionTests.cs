using EsapiRunnerHub.Configuration;
using EsapiRunnerHub.Context;

namespace EsapiRunnerHub.Tests
{
    internal static class ContextSelectionTests
    {
        public static void Register()
        {
            TestHarness.Test("plan selection derives course structure set and image", DerivesPlanContext);
            TestHarness.Test("standalone structure set satisfies plan-or-structure-set", AcceptsStandaloneStructureSet);
            TestHarness.Test("missing required context has explicit reason", ReportsMissingContext);
        }

        private static void DerivesPlanContext()
        {
            var directory = CreateDirectory();
            var selection = new ContextSelection { PatientId = "SYN-1001" };

            selection.SelectPlan(directory, "P1");

            TestHarness.AssertEqual("C1", selection.CourseId);
            TestHarness.AssertEqual("SS1", selection.StructureSetId);
            TestHarness.AssertEqual("IMG1", selection.ImageId);
            TestHarness.AssertEqual(string.Empty, selection.MissingFor(ContextRequirement.Plan));
        }

        private static void AcceptsStandaloneStructureSet()
        {
            var directory = CreateDirectory();
            var selection = new ContextSelection { PatientId = "SYN-1001" };

            selection.SelectStructureSet(directory, "SS-ONLY");

            TestHarness.AssertEqual("IMG2", selection.ImageId);
            TestHarness.AssertEqual(string.Empty, selection.MissingFor(ContextRequirement.PlanOrStructureSet));
            TestHarness.AssertEqual("Plan required", selection.MissingFor(ContextRequirement.Plan));
        }

        private static void ReportsMissingContext()
        {
            var selection = new ContextSelection { PatientId = "SYN-1001" };

            TestHarness.AssertEqual("Structure set required", selection.MissingFor(ContextRequirement.StructureSet));
            TestHarness.AssertEqual("Plan or plan sum required", selection.MissingFor(ContextRequirement.PlanningItem));
        }

        private static ContextDirectory CreateDirectory()
        {
            var directory = new ContextDirectory();
            directory.Plans.Add(new PlanDescriptor
            {
                Id = "P1", CourseId = "C1", StructureSetId = "SS1", ImageId = "IMG1", Kind = "ExternalPlanSetup"
            });
            directory.StructureSets.Add(new StructureSetDescriptor { Id = "SS1", ImageId = "IMG1" });
            directory.StructureSets.Add(new StructureSetDescriptor { Id = "SS-ONLY", ImageId = "IMG2" });
            return directory;
        }
    }
}
