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
            TestHarness.Test("course and image can be required independently", AcceptsCourseAndImage);
            TestHarness.Test("missing required context has explicit reason", ReportsMissingContext);
            TestHarness.Test("plan labels include course plan and kind", LabelsPlanWithCourse);
        }

        private static void LabelsPlanWithCourse()
        {
            var plan = new PlanDescriptor { CourseId = "C1", Id = "P1", Kind = "ExternalPlanSetup" };
            var planSum = new PlanSumDescriptor { CourseId = "C2", Id = "SUM1" };

            TestHarness.AssertEqual("C1 · P1 · ExternalPlanSetup", plan.Display);
            TestHarness.AssertEqual("C2 · SUM1 · PlanSum", planSum.Display);
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

        private static void AcceptsCourseAndImage()
        {
            var directory = CreateDirectory();
            var selection = new ContextSelection { PatientId = "SYN-1001", CourseId = "C1" };
            selection.SelectImage(directory, "IMG2");

            TestHarness.AssertEqual(string.Empty, selection.MissingFor(ContextRequirement.Course));
            TestHarness.AssertEqual(string.Empty, selection.MissingFor(ContextRequirement.Image));
        }

        private static ContextDirectory CreateDirectory()
        {
            var directory = new ContextDirectory();
            directory.Courses.Add(new CourseDescriptor { Id = "C1" });
            directory.Plans.Add(new PlanDescriptor
            {
                Id = "P1", CourseId = "C1", StructureSetId = "SS1", ImageId = "IMG1", Kind = "ExternalPlanSetup"
            });
            directory.StructureSets.Add(new StructureSetDescriptor { Id = "SS1", ImageId = "IMG1" });
            directory.StructureSets.Add(new StructureSetDescriptor { Id = "SS-ONLY", ImageId = "IMG2" });
            directory.Images.Add(new ImageDescriptor { Id = "IMG1" });
            directory.Images.Add(new ImageDescriptor { Id = "IMG2" });
            return directory;
        }
    }
}
