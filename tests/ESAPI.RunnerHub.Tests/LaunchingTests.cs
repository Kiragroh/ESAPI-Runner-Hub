using System;
using System.IO;
using System.Threading;
using EsapiRunnerHub.Configuration;
using EsapiRunnerHub.Launching;
using EsapiRunnerHub.Patients;

namespace EsapiRunnerHub.Tests
{
    internal static class LaunchingTests
    {
        public static void Register()
        {
            TestHarness.Test("argument composer transfers only a safe selected patient", ComposesSafeArguments);
            TestHarness.Test("environment transport keeps patient out of arguments and logs", UsesEnvironmentOnly);
            TestHarness.Test("required patient and unsafe IDs are rejected", RejectsInvalidPatientContext);
            TestHarness.Test("path probe reports a missing local executable", ReportsMissingPath);
            TestHarness.Test("child exit and crash stay isolated from the hub", KeepsChildFailuresIsolated);
        }

        private static void ComposesSafeArguments()
        {
            var application = Application(PatientMode.Optional, PatientTransport.Argument);
            application.Arguments = "--mode success";
            application.PatientArgumentTemplate = "--patient-id {PatientId}";

            var request = ArgumentComposer.Compose(application, new PatientRecord("TEST-001", "Ada", "Example", 0), true);

            TestHarness.AssertContains(request.Arguments, "--patient-id TEST-001");
            TestHarness.AssertFalse(request.LogSummary.Contains("TEST-001"));
        }

        private static void UsesEnvironmentOnly()
        {
            var application = Application(PatientMode.Required, PatientTransport.Environment);
            application.PatientEnvironmentKey = "RUNNER_PATIENT_ID";

            var request = ArgumentComposer.Compose(application, new PatientRecord("SYN-200", "Linus", "Sample", 0), true);

            TestHarness.AssertFalse(request.Arguments.Contains("SYN-200"));
            TestHarness.AssertEqual("SYN-200", request.EnvironmentVariables["RUNNER_PATIENT_ID"]);
            TestHarness.AssertFalse(request.LogSummary.Contains("SYN-200"));
        }

        private static void RejectsInvalidPatientContext()
        {
            var application = Application(PatientMode.Required, PatientTransport.Argument);
            application.PatientArgumentTemplate = "--patient-id {PatientId}";

            TestHarness.AssertThrows<InvalidOperationException>(() => ArgumentComposer.Compose(application, null, false));
            TestHarness.AssertThrows<InvalidOperationException>(() =>
                ArgumentComposer.Compose(application, new PatientRecord("BAD ID&", "A", "B", 0), true));
        }

        private static void ReportsMissingPath()
        {
            var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.exe");
            var result = new PathProbe().ProbeAsync(missing, 500).GetAwaiter().GetResult();
            TestHarness.AssertEqual(PathReadiness.Missing, result.Readiness);
        }

        private static void KeepsChildFailuresIsolated()
        {
            var fixture = TestHarness.PathFromRoot("tests/RunnerFixture/bin/x64/Release/RunnerFixture.exe");
            var launcher = new ChildProcessLauncher();

            var exit7 = launcher.Start(new LaunchRequest("fixture", fixture, Path.GetDirectoryName(fixture), "--mode exit7"));
            WaitForExit(exit7);
            TestHarness.AssertEqual(7, exit7.ExitCode.Value);

            var crash = launcher.Start(new LaunchRequest("fixture", fixture, Path.GetDirectoryName(fixture), "--mode crash"));
            WaitForExit(crash);
            TestHarness.AssertTrue(crash.ExitCode.HasValue && crash.ExitCode.Value != 0);

            var success = launcher.Start(new LaunchRequest("fixture", fixture, Path.GetDirectoryName(fixture), "--mode success"));
            WaitForExit(success);
            TestHarness.AssertEqual(0, success.ExitCode.Value);
        }

        private static void WaitForExit(RunningProcessInfo process)
        {
            if (!process.WaitForExit(10000))
            {
                throw new InvalidOperationException("Fixture process did not exit.");
            }
        }

        private static ApplicationDefinition Application(PatientMode mode, PatientTransport transport)
        {
            var fixture = TestHarness.PathFromRoot("tests/RunnerFixture/bin/x64/Release/RunnerFixture.exe");
            return new ApplicationDefinition
            {
                Id = "fixture",
                Name = "Fixture",
                Executable = fixture,
                PatientMode = mode,
                PatientTransport = transport,
                Enabled = true
            };
        }
    }
}
