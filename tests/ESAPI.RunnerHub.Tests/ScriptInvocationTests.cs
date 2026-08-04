using System;
using System.IO;
using EsapiScriptHost.Contracts;
using EsapiScriptHost.Host;

namespace EsapiRunnerHub.Tests
{
    internal static class ScriptInvocationTests
    {
        public static void Register()
        {
            TestHarness.Test("EsapiEssentials script receives selected context", InvokesEssentials);
            TestHarness.Test("classic Eclipse script receives verified Eclipse 18 context", InvokesEclipse);
            TestHarness.Test("write metadata must agree with configured write mode", ValidatesWriteMetadata);
            TestHarness.Test("successful writable host run saves exactly once", SuccessfulRunSavesOnce);
            TestHarness.Test("failed writable host run never saves", FailedRunNeverSaves);
        }

        private static void InvokesEssentials()
        {
            WithResolvedContext((script, context) =>
            {
                new EsapiEssentialsInvoker().Invoke(script, "VMS.TPS.EssentialsScript", context);
                TestHarness.AssertEqual("essentials|SYN-1001|P1|SS1", ReadMarker());
            });
        }

        private static void InvokesEclipse()
        {
            WithResolvedContext((script, context) =>
            {
                new EclipseScriptInvoker().Invoke(script, "VMS.TPS.EclipseScript", context);
                TestHarness.AssertEqual("eclipse|SYN-1001|P1|SS1", ReadMarker());
            });
        }

        private static void ValidatesWriteMetadata()
        {
            var script = FixtureScriptPath();
            TestHarness.AssertTrue(ScriptMetadataInspector.IsWriteable(script));
            TestHarness.AssertThrows<InvalidOperationException>(() =>
                ScriptMetadataInspector.ValidateWriteMode(script, WriteMode.ReadOnly));
            ScriptMetadataInspector.ValidateWriteMode(script, WriteMode.ConfirmSave);
        }

        private static void SuccessfulRunSavesOnce()
        {
            var saveMarker = NewMarker("save");
            var scriptMarker = NewMarker("script");
            Environment.SetEnvironmentVariable("FAKE_VMS_SAVE_MARKER", saveMarker);
            Environment.SetEnvironmentVariable("SCRIPT_FIXTURE_MARKER", scriptMarker);
            try
            {
                var payload = ScriptHostCoreTests.CreatePayloadForInvocation();
                payload.ScriptPath = FixtureScriptPath();
                payload.EntryType = "VMS.TPS.EclipseScript";
                payload.ScriptEngine = ScriptEngine.Eclipse;
                payload.WriteMode = WriteMode.ConfirmSave;

                new ScriptHostApplication().Run(payload, () => SaveChoice.Save);

                TestHarness.AssertEqual(1, File.ReadAllLines(saveMarker).Length);
            }
            finally
            {
                ClearMarker("FAKE_VMS_SAVE_MARKER", saveMarker);
                ClearMarker("SCRIPT_FIXTURE_MARKER", scriptMarker);
            }
        }

        private static void FailedRunNeverSaves()
        {
            var saveMarker = NewMarker("save-failure");
            Environment.SetEnvironmentVariable("FAKE_VMS_SAVE_MARKER", saveMarker);
            try
            {
                var payload = ScriptHostCoreTests.CreatePayloadForInvocation();
                payload.ScriptPath = FixtureScriptPath();
                payload.EntryType = "VMS.TPS.FailingEclipseScript";
                payload.ScriptEngine = ScriptEngine.Eclipse;
                payload.WriteMode = WriteMode.ConfirmSave;

                TestHarness.AssertThrows<System.Reflection.TargetInvocationException>(() =>
                    new ScriptHostApplication().Run(payload, () => SaveChoice.Save));

                TestHarness.AssertFalse(File.Exists(saveMarker));
            }
            finally
            {
                ClearMarker("FAKE_VMS_SAVE_MARKER", saveMarker);
            }
        }

        private static void WithResolvedContext(Action<string, ResolvedContext> action)
        {
            var marker = Path.Combine(Path.GetTempPath(), "runner-hub-script-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("SCRIPT_FIXTURE_MARKER", marker);
            var payload = ScriptHostCoreTests.CreatePayloadForInvocation();
            try
            {
                using (var session = EsapiSession.Open(payload))
                {
                    var context = new ContextResolver().Resolve(session.Patient, session.CurrentUser, payload);
                    action(FixtureScriptPath(), context);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("SCRIPT_FIXTURE_MARKER", null);
                if (File.Exists(marker)) File.Delete(marker);
            }
        }

        private static string ReadMarker()
        {
            return File.ReadAllText(Environment.GetEnvironmentVariable("SCRIPT_FIXTURE_MARKER"));
        }

        private static string FixtureScriptPath()
        {
            var adjacent = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SyntheticScripts.esapi.dll");
            return File.Exists(adjacent)
                ? adjacent
                : TestHarness.PathFromRoot("tests/ScriptFixtures/bin/x64/Debug/SyntheticScripts.esapi.dll");
        }

        private static string NewMarker(string suffix)
        {
            return Path.Combine(Path.GetTempPath(), "runner-hub-" + suffix + "-" + Guid.NewGuid().ToString("N"));
        }

        private static void ClearMarker(string environmentKey, string path)
        {
            Environment.SetEnvironmentVariable(environmentKey, null);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
