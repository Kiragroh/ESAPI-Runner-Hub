using System;
using System.IO;
using EsapiRunnerHub.Context;
using EsapiRunnerHub.History;
using EsapiRunnerHub.Launching;

namespace EsapiRunnerHub.Tests
{
    internal static class ContextCommandLineTests
    {
        public static void Register()
        {
            TestHarness.Test("CLI starts a context script from private environment values", RunsEnvironmentContext);
            TestHarness.Test("CLI replays the latest protected context without identifiers in arguments", ReplaysLatestContext);
        }

        private static void RunsEnvironmentContext()
        {
            WithFixture((settingsPath, historyPath) =>
            {
                SetContextEnvironment("SYN-CLI", "C1", "P1", "SS1", "IMG1");
                try
                {
                    var arguments = new[] { "--run-context", "direct", "--settings", settingsPath };
                    TestHarness.AssertFalse(string.Join(" ", arguments).Contains("SYN-CLI"));
                    TestHarness.AssertFalse(string.Join(" ", arguments).Contains("P1"));
                    TestHarness.AssertEqual(0, ContextCommandLineRunner.Run(arguments));
                }
                finally
                {
                    SetContextEnvironment(null, null, null, null, null);
                }
            });
        }

        private static void ReplaysLatestContext()
        {
            WithFixture((settingsPath, historyPath) =>
            {
                var selection = new ContextSelection
                {
                    PatientId = "SYN-REPLAY", CourseId = "C1", PlanId = "P1",
                    StructureSetId = "SS1", ImageId = "IMG1"
                };
                selection.PlanIdsInScope.Add("P1");
                var entry = new LaunchHistoryEntry
                {
                    HistoryId = "history-1", ApplicationId = "direct", ApplicationName = "Direct",
                    ArtifactLabel = "Binary", AccessLabel = "Read-only", StartedUtc = DateTime.UtcNow,
                    State = LaunchHistoryState.Exited, LaunchMode = LaunchMode.Context,
                    ProtectedContext = new ProtectedContextEnvelope().Protect(selection)
                };
                new LaunchHistoryStore(historyPath, 30, 100).Save(new[] { entry });

                var arguments = new[] { "--replay-latest", "direct", "--settings", settingsPath };
                TestHarness.AssertFalse(string.Join(" ", arguments).Contains("SYN-REPLAY"));
                TestHarness.AssertEqual(0, ContextCommandLineRunner.Run(arguments));
            });
        }

        private static void WithFixture(Action<string, string> action)
        {
            var directory = Path.Combine(Path.GetTempPath(), "runner-hub-cli-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var fixture = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RunnerFixture.exe");
                if (!File.Exists(fixture))
                {
                    var configurationName = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Name;
                    fixture = TestHarness.PathFromRoot("tests/RunnerFixture/bin/x64/" + configurationName + "/RunnerFixture.exe");
                }
                var settingsPath = Path.Combine(directory, "settings.ini");
                var historyPath = Path.Combine(directory, "history.json");
                File.WriteAllText(settingsPath,
                    "[Hub]\nScriptHostExecutable=" + fixture + "\nLogDirectory=" + directory +
                    "\nHistoryFile=" + historyPath + "\nHistoryRetentionDays=30\nHistoryMaxEntries=100" +
                    "\n[Application.direct]\nName=Direct\nExecutable=Tool.esapi.dll" +
                    "\nLaunchKind=EsapiContextScript\nScriptEngine=Eclipse\nContextRequirement=Plan" +
                    "\nScopeMode=Single\nWriteMode=ReadOnly\nPatientMode=Required\nPatientTransport=None\nEnabled=true\n");
                action(settingsPath, historyPath);
            }
            finally
            {
                try { Directory.Delete(directory, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static void SetContextEnvironment(string patient, string course, string plan, string structureSet, string image)
        {
            Environment.SetEnvironmentVariable(ContextCommandLineRunner.PatientEnvironmentKey, patient);
            Environment.SetEnvironmentVariable(ContextCommandLineRunner.CourseEnvironmentKey, course);
            Environment.SetEnvironmentVariable(ContextCommandLineRunner.PlanEnvironmentKey, plan);
            Environment.SetEnvironmentVariable(ContextCommandLineRunner.StructureSetEnvironmentKey, structureSet);
            Environment.SetEnvironmentVariable(ContextCommandLineRunner.ImageEnvironmentKey, image);
        }
    }
}
