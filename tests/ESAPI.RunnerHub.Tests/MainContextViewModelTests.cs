using System.IO;
using System.Linq;
using EsapiRunnerHub.Configuration;
using EsapiRunnerHub.Context;
using EsapiRunnerHub.Launching;
using EsapiRunnerHub.Patients;
using EsapiRunnerHub.ViewModels;

namespace EsapiRunnerHub.Tests
{
    internal static class MainContextViewModelTests
    {
        public static void Register()
        {
            TestHarness.Test("main view model derives context from selected plan", DerivesSelectedPlanContext);
            TestHarness.Test("standalone structure set enables plan-or-structure-set tool", EnablesStandaloneStructureSet);
            TestHarness.Test("image can be selected explicitly", SelectsImageExplicitly);
            TestHarness.Test("direct card explains missing plan", ExplainsMissingPlan);
        }

        private static void DerivesSelectedPlanContext()
        {
            var viewModel = CreateViewModel(ContextRequirement.PlanOrStructureSet);
            viewModel.SelectPatient(new PatientRecord("SYN-1001", "Ada", "Example", 0));
            viewModel.SetContextDirectory(CreateDirectory());

            viewModel.SelectedPlan = viewModel.Plans.Single();

            TestHarness.AssertEqual("C1", viewModel.ContextSelection.CourseId);
            TestHarness.AssertEqual("SS1", viewModel.ContextSelection.StructureSetId);
            TestHarness.AssertEqual("IMG1", viewModel.ContextSelection.ImageId);
            TestHarness.AssertEqual("IMG1", viewModel.SelectedImage.Id);
            TestHarness.AssertTrue(viewModel.Applications.Single().CanStartContext);
            TestHarness.AssertEqual("Start with plan", viewModel.Applications.Single().ContextActionLabel);
        }

        private static void EnablesStandaloneStructureSet()
        {
            var viewModel = CreateViewModel(ContextRequirement.PlanOrStructureSet);
            viewModel.SelectPatient(new PatientRecord("SYN-1001", "Ada", "Example", 0));
            viewModel.SetContextDirectory(CreateDirectory());

            viewModel.SelectedStructureSet = viewModel.StructureSets.Single(item => item.Id == "SS-ONLY");

            TestHarness.AssertTrue(viewModel.Applications.Single().CanStartContext);
            TestHarness.AssertEqual("Start with structure set", viewModel.Applications.Single().ContextActionLabel);
        }

        private static void SelectsImageExplicitly()
        {
            var viewModel = CreateViewModel(ContextRequirement.Patient);
            viewModel.SelectPatient(new PatientRecord("SYN-1001", "Ada", "Example", 0));
            viewModel.SetContextDirectory(CreateDirectory());

            viewModel.SelectedImage = viewModel.Images.Single(item => item.Id == "IMG2");

            TestHarness.AssertEqual("IMG2", viewModel.ContextSelection.ImageId);
            TestHarness.AssertEqual(2, viewModel.Images.Count);
        }

        private static void ExplainsMissingPlan()
        {
            var viewModel = CreateViewModel(ContextRequirement.Plan);
            viewModel.SelectPatient(new PatientRecord("SYN-1001", "Ada", "Example", 0));
            viewModel.SetContextDirectory(CreateDirectory());

            TestHarness.AssertFalse(viewModel.Applications.Single().CanStartContext);
            TestHarness.AssertEqual("Plan required", viewModel.Applications.Single().ContextHint);
        }

        private static MainViewModel CreateViewModel(ContextRequirement requirement)
        {
            var root = Path.GetTempPath();
            var configuration = IniConfigurationStore.ParseText(@"
[Hub]
ScriptHostExecutable=ESAPI-Script-Host.exe
[Application.direct]
Name=Direct
Category=Scripts
Executable=Tool.esapi.dll
LaunchKind=EsapiContextScript
ScriptEngine=Eclipse
ContextRequirement=" + requirement + @"
ScopeMode=Single
WriteMode=ReadOnly
PatientMode=Required
PatientTransport=None
", Path.Combine(root, "runner-hub-context-settings.ini"));
            var viewModel = new MainViewModel(configuration, new PatientRecord[0]);
            viewModel.UpdateApplicationReadiness("direct", new PathProbeResult(PathReadiness.Ready, "Ready"));
            return viewModel;
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
