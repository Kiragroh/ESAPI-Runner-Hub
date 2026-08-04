using System;
using System.IO;
using EsapiRunnerHub.Configuration;
using EsapiRunnerHub.Context;
using EsapiRunnerHub.Launching;
using EsapiRunnerHub.Patients;

namespace EsapiRunnerHub.Tests
{
    internal static class ContextLaunchProtocolTests
    {
        public static void Register()
        {
            TestHarness.Test("context launch payload round trips outside arguments and logs", RoundTripsPrivately);
            TestHarness.Test("context launch validates configured requirement", RejectsMissingContext);
            TestHarness.Test("context payload rejects invalid launch token", RejectsInvalidToken);
            TestHarness.Test("Eclipse plug-ins use the private context host when requested", ComposesPluginContextLaunch);
        }

        private static void RoundTripsPrivately()
        {
            var directory = Path.Combine(Path.GetTempPath(), "runner-hub-protocol");
            var host = Path.Combine(directory, "ESAPI-Script-Host.exe");
            var script = Path.Combine(directory, "Tool.esapi.dll");
            var api = Path.Combine(directory, "VMS.TPS.Common.Model.API.dll");
            var types = Path.Combine(directory, "VMS.TPS.Common.Model.Types.dll");
            var configuration = IniConfigurationStore.ParseText(
                "[Hub]\nEsapiApiAssembly=" + api + "\nEsapiTypesAssembly=" + types +
                "\n[Application.direct]\nName=Direct\nExecutable=" + script +
                "\nLaunchKind=EsapiContextScript\nScriptEngine=Eclipse\nContextRequirement=Plan" +
                "\nScopeMode=Single\nWriteMode=ConfirmSave\nPatientMode=Required\nPatientTransport=None" +
                "\nEntryType=VMS.TPS.Script\nExtraReferences=Helper.dll\n",
                Path.Combine(directory, "settings.ini"));
            var application = configuration.Applications[0];
            var patient = new PatientRecord("SYN-1001", "Ada", "Example", 0);
            var selection = new ContextSelection
            {
                PatientId = patient.Id, CourseId = "C1", PlanId = "P1", StructureSetId = "SS1", ImageId = "IMG1"
            };
            selection.PlanIdsInScope.Add("P1");

            var request = ContextScriptRequestComposer.Compose(application, patient, selection, configuration.Hub, host);

            TestHarness.AssertEqual(string.Empty, request.Arguments);
            TestHarness.AssertFalse(request.LogSummary.Contains(patient.Id));
            TestHarness.AssertFalse(request.LogSummary.Contains("P1"));
            var payload = ContextLaunchPayload.Decode(request.EnvironmentVariables[ContextLaunchPayload.EnvironmentKey]);
            TestHarness.AssertEqual(patient.Id, payload.PatientId);
            TestHarness.AssertEqual("P1", payload.PlanId);
            TestHarness.AssertEqual("SS1", payload.StructureSetId);
            TestHarness.AssertEqual(script, payload.ScriptPath);
            TestHarness.AssertEqual(api, payload.ApiAssemblyPath);
            TestHarness.AssertEqual(WriteMode.ConfirmSave, payload.WriteMode);

            var hostPayload = EsapiScriptHost.Contracts.ContextLaunchPayload.Decode(
                request.EnvironmentVariables[ContextLaunchPayload.EnvironmentKey]);
            TestHarness.AssertEqual(patient.Id, hostPayload.PatientId);
            TestHarness.AssertEqual("P1", hostPayload.PlanId);
            TestHarness.AssertEqual("ConfirmSave", hostPayload.WriteMode.ToString());
        }

        private static void RejectsMissingContext()
        {
            var application = new ApplicationDefinition
            {
                Id = "direct", Executable = @"C:\Tool.esapi.dll",
                LaunchKind = LaunchKind.EsapiContextScript,
                ContextRequirement = ContextRequirement.Plan,
                PatientMode = PatientMode.Required
            };
            var patient = new PatientRecord("SYN-1001", string.Empty, string.Empty, 0);
            var selection = new ContextSelection { PatientId = patient.Id };
            var settings = new HubSettings();

            var exception = TestHarness.AssertThrows<InvalidOperationException>(() =>
                ContextScriptRequestComposer.Compose(application, patient, selection, settings, @"C:\ESAPI-Script-Host.exe"));

            TestHarness.AssertContains(exception.Message, "Plan required");
        }

        private static void RejectsInvalidToken()
        {
            var payload = new ContextLaunchPayload { LaunchToken = "not-a-guid", ApplicationId = "direct" };
            var encoded = payload.Encode();

            TestHarness.AssertThrows<InvalidOperationException>(() => ContextLaunchPayload.Decode(encoded));
        }

        private static void ComposesPluginContextLaunch()
        {
            var directory = Path.Combine(Path.GetTempPath(), "runner-hub-plugin-protocol");
            var configuration = IniConfigurationStore.ParseText(
                "[Hub]\nEsapiApiAssembly=VMS.TPS.Common.Model.API.dll\nEsapiTypesAssembly=VMS.TPS.Common.Model.Types.dll" +
                "\n[Application.plugin]\nName=Plugin\nExecutable=Tool.esapi.dll\nLaunchKind=EclipsePlugin" +
                "\nScriptEngine=Eclipse\nContextRequirement=Plan\nScopeMode=Single\nWriteMode=ReadOnly" +
                "\nPatientMode=Required\nPatientTransport=None\nEntryType=VMS.TPS.Script\n",
                Path.Combine(directory, "settings.ini"));
            var patient = new PatientRecord("SYN-1001", string.Empty, string.Empty, 0);
            var selection = new ContextSelection { PatientId = patient.Id, CourseId = "C1", PlanId = "P1" };

            var request = ContextScriptRequestComposer.Compose(configuration.Applications[0], patient, selection,
                configuration.Hub, Path.Combine(directory, "ESAPI-Script-Host.exe"));

            var payload = ContextLaunchPayload.Decode(request.EnvironmentVariables[ContextLaunchPayload.EnvironmentKey]);
            TestHarness.AssertEqual("plugin", payload.ApplicationId);
            TestHarness.AssertEqual("P1", payload.PlanId);
        }
    }
}
