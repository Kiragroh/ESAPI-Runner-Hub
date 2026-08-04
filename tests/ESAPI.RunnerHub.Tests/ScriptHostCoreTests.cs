using System;
using System.IO;
using System.Reflection;
using EsapiRunnerHub.Configuration;
using EsapiRunnerHub.Launching;
using EsapiScriptHost.Host;

namespace EsapiRunnerHub.Tests
{
    internal static class ScriptHostCoreTests
    {
        public static void Register()
        {
            TestHarness.Test("host resolves exact plan structure set and scopes", ResolvesExactContext);
            TestHarness.Test("host resolves image without a structure set", ResolvesImageOnly);
            TestHarness.Test("host permits an empty planning context", PermitsEmptyContext);
            TestHarness.Test("host rejects ambiguous plan identifiers", RejectsAmbiguousPlan);
            TestHarness.Test("host session saves at most once", SavesAtMostOnce);
            TestHarness.Test("failed host outcome always discards", FailureAlwaysDiscards);
        }

        private static void ResolvesExactContext()
        {
            var api = LoadFakeApi();
            var application = CreateApplication(api);
            var patient = application.GetType().GetMethod("OpenPatientById").Invoke(application, new object[] { "SYN-1001" });
            try
            {
                var payload = CreatePayloadForInvocation();
                var context = new ContextResolver().Resolve(patient, ReadProperty(application, "CurrentUser"), payload);

                TestHarness.AssertEqual("P1", ReadString(context.Plan, "Id"));
                TestHarness.AssertEqual("SS1", ReadString(context.StructureSet, "Id"));
                TestHarness.AssertEqual("IMG1", ReadString(context.Image, "Id"));
                TestHarness.AssertEqual(1, context.PlansInScope.Count);
            }
            finally
            {
                application.GetType().GetMethod("ClosePatient").Invoke(application, null);
                ((IDisposable)application).Dispose();
            }
        }

        private static void RejectsAmbiguousPlan()
        {
            Environment.SetEnvironmentVariable("FAKE_VMS_DUPLICATE_PLAN", "1");
            var api = LoadFakeApi();
            var application = CreateApplication(api);
            var patient = application.GetType().GetMethod("OpenPatientById").Invoke(application, new object[] { "SYN-1001" });
            try
            {
                TestHarness.AssertThrows<InvalidOperationException>(() =>
                    new ContextResolver().Resolve(patient, ReadProperty(application, "CurrentUser"), CreatePayloadForInvocation()));
            }
            finally
            {
                Environment.SetEnvironmentVariable("FAKE_VMS_DUPLICATE_PLAN", null);
                application.GetType().GetMethod("ClosePatient").Invoke(application, null);
                ((IDisposable)application).Dispose();
            }
        }

        private static void ResolvesImageOnly()
        {
            var api = LoadFakeApi();
            var application = CreateApplication(api);
            var patient = application.GetType().GetMethod("OpenPatientById").Invoke(application, new object[] { "SYN-1001" });
            try
            {
                var context = new ContextResolver().Resolve(patient, ReadProperty(application, "CurrentUser"),
                    new ContextLaunchPayload { PatientId = "SYN-1001", ImageId = "IMG2" }, api);

                TestHarness.AssertEqual("IMG2", ReadString(context.Image, "Id"));
                TestHarness.AssertEqual(null, context.StructureSet);
            }
            finally
            {
                application.GetType().GetMethod("ClosePatient").Invoke(application, null);
                ((IDisposable)application).Dispose();
            }
        }

        private static void PermitsEmptyContext()
        {
            var api = LoadFakeApi();
            var context = new ContextResolver().Resolve(null, null, new ContextLaunchPayload(), api);

            TestHarness.AssertEqual(null, context.Patient);
            TestHarness.AssertEqual(api, context.ApiAssembly);
        }

        private static void SavesAtMostOnce()
        {
            var marker = Path.Combine(Path.GetTempPath(), "runner-hub-save-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("FAKE_VMS_SAVE_MARKER", marker);
            try
            {
                using (var session = EsapiSession.Open(CreatePayloadForInvocation()))
                {
                    session.SaveModifications();
                    TestHarness.AssertThrows<InvalidOperationException>(() => session.SaveModifications());
                }

                TestHarness.AssertEqual(1, File.ReadAllLines(marker).Length);
            }
            finally
            {
                Environment.SetEnvironmentVariable("FAKE_VMS_SAVE_MARKER", null);
                if (File.Exists(marker)) File.Delete(marker);
            }
        }

        private static void FailureAlwaysDiscards()
        {
            TestHarness.AssertEqual(SaveAction.CloseWithoutSave,
                SaveDecision.Decide(false, WriteMode.ConfirmSave, SaveChoice.Save));
            TestHarness.AssertEqual(SaveAction.SaveOnce,
                SaveDecision.Decide(true, WriteMode.ConfirmSave, SaveChoice.Save));
            TestHarness.AssertEqual(SaveAction.CloseWithoutSave,
                SaveDecision.Decide(true, WriteMode.ConfirmSave, SaveChoice.Discard));
            TestHarness.AssertEqual(SaveAction.CloseWithoutSave,
                SaveDecision.Decide(true, WriteMode.ReadOnly, SaveChoice.Save));
        }

        internal static ContextLaunchPayload CreatePayloadForInvocation()
        {
            var api = TestHarness.PathFromRoot("tests/FakeVms.Api/bin/x64/Debug/VMS.TPS.Common.Model.API.dll");
            return new ContextLaunchPayload
            {
                LaunchToken = Guid.NewGuid().ToString("N"), ApplicationId = "fixture", ScriptPath = api,
                ApiAssemblyPath = api, ScriptEngine = ScriptEngine.Eclipse, WriteMode = WriteMode.ReadOnly,
                PatientId = "SYN-1001", CourseId = "C1", PlanId = "P1", StructureSetId = "SS1", ImageId = "IMG1",
                PlanIdsInScope = new System.Collections.Generic.List<string> { "P1" }
            };
        }

        private static Assembly LoadFakeApi()
        {
            return Assembly.LoadFrom(TestHarness.PathFromRoot("tests/FakeVms.Api/bin/x64/Debug/VMS.TPS.Common.Model.API.dll"));
        }

        private static object CreateApplication(Assembly api)
        {
            return api.GetType("VMS.TPS.Common.Model.API.Application", true)
                .GetMethod("CreateApplication", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, null);
        }

        private static object ReadProperty(object instance, string name)
        {
            return instance.GetType().GetProperty(name).GetValue(instance, null);
        }

        private static string ReadString(object instance, string name)
        {
            var value = ReadProperty(instance, name);
            return value == null ? string.Empty : Convert.ToString(value);
        }
    }
}
