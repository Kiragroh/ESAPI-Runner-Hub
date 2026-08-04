using System.Linq;
using EsapiRunnerHub.Configuration;

namespace EsapiRunnerHub.Tests
{
    internal static class ContextConfigurationTests
    {
        public static void Register()
        {
            TestHarness.Test("direct ESAPI context settings parse and round trip", ParsesAndRoundTrips);
            TestHarness.Test("direct context scripts reject external patient transport", RejectsExternalPatientTransport);
            TestHarness.Test("direct context script target extension is validated", ValidatesTargetExtension);
            TestHarness.Test("reference-only Eclipse entries cannot confirm saves", RejectsSaveForReferenceOnlyEntry);
            TestHarness.Test("script host path is editable and resolved from settings", ResolvesScriptHostPath);
        }

        private static void ParsesAndRoundTrips()
        {
            var configuration = IniConfigurationStore.ParseText(@"
[Application.direct]
Name=Direct script
Executable=Tool.esapi.dll
LaunchKind=EsapiContextScript
ScriptEngine=Eclipse
ContextRequirement=PlanOrStructureSet
ScopeMode=Multiple
WriteMode=ConfirmSave
EntryType=VMS.TPS.Script
ExtraReferences=Helper.dll;Support.dll
PatientMode=Required
PatientTransport=None
", @"C:\portable\settings.ini");
            var application = configuration.Applications.Single();

            TestHarness.AssertEqual(LaunchKind.EsapiContextScript, application.LaunchKind);
            TestHarness.AssertEqual(ScriptEngine.Eclipse, application.ScriptEngine);
            TestHarness.AssertEqual(ContextRequirement.PlanOrStructureSet, application.ContextRequirement);
            TestHarness.AssertEqual(ScopeMode.Multiple, application.ScopeMode);
            TestHarness.AssertEqual(WriteMode.ConfirmSave, application.WriteMode);
            TestHarness.AssertEqual("VMS.TPS.Script", application.EntryType);
            TestHarness.AssertEqual("Helper.dll;Support.dll", application.ExtraReferences);
            TestHarness.AssertTrue(configuration.Validate().IsValid);

            var serialized = IniConfigurationStore.Serialize(configuration);
            TestHarness.AssertContains(serialized, "LaunchKind=EsapiContextScript");
            TestHarness.AssertContains(serialized, "ScriptEngine=Eclipse");
            TestHarness.AssertContains(serialized, "ContextRequirement=PlanOrStructureSet");
            TestHarness.AssertContains(serialized, "ScopeMode=Multiple");
            TestHarness.AssertContains(serialized, "WriteMode=ConfirmSave");
            TestHarness.AssertContains(serialized, "EntryType=VMS.TPS.Script");
            TestHarness.AssertContains(serialized, "ExtraReferences=Helper.dll;Support.dll");
        }

        private static void RejectsExternalPatientTransport()
        {
            var validation = IniConfigurationStore.ParseText(@"
[Application.direct]
Name=Direct script
Executable=Tool.cs
LaunchKind=EsapiContextScript
ContextRequirement=Patient
PatientMode=Required
PatientTransport=Environment
PatientEnvironmentKey=PATIENT_ID
", @"C:\portable\settings.ini").Validate();

            TestHarness.AssertFalse(validation.IsValid);
            TestHarness.AssertTrue(validation.Errors.Any(error => error.Contains("EsapiContextScript") && error.Contains("PatientTransport")));
        }

        private static void ValidatesTargetExtension()
        {
            var validation = IniConfigurationStore.ParseText(@"
[Application.direct]
Name=Direct script
Executable=Tool.exe
LaunchKind=EsapiContextScript
ContextRequirement=Plan
PatientMode=Required
PatientTransport=None
", @"C:\portable\settings.ini").Validate();

            TestHarness.AssertFalse(validation.IsValid);
            TestHarness.AssertTrue(validation.Errors.Any(error => error.Contains(".cs") && error.Contains(".dll")));
        }

        private static void RejectsSaveForReferenceOnlyEntry()
        {
            var validation = IniConfigurationStore.ParseText(@"
[Application.reference]
Name=Reference
Executable=Tool.esapi.dll
LaunchKind=EclipsePlugin
WriteMode=ConfirmSave
PatientMode=None
PatientTransport=None
", @"C:\portable\settings.ini").Validate();

            TestHarness.AssertFalse(validation.IsValid);
            TestHarness.AssertTrue(validation.Errors.Any(error => error.Contains("WriteMode")));
        }

        private static void ResolvesScriptHostPath()
        {
            var configuration = IniConfigurationStore.ParseText(@"
[Hub]
ScriptHostExecutable=runtime\ESAPI-Script-Host.exe
", @"C:\portable\settings.ini");

            TestHarness.AssertEqual(@"C:\portable\runtime\ESAPI-Script-Host.exe", configuration.Hub.ResolvedScriptHostExecutable);
            TestHarness.AssertContains(IniConfigurationStore.Serialize(configuration), "ScriptHostExecutable=runtime\\ESAPI-Script-Host.exe");
        }
    }
}
