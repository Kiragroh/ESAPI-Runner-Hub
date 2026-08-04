using System;
using System.IO;
using System.Text;
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
            TestHarness.Test("CLI starts an ordered read-only context series from one private environment value", RunsEnvironmentContextSeries);
            TestHarness.Test("CLI refuses a write-enabled context series", RejectsWriteEnabledContextSeries);
            TestHarness.Test("CLI replays the latest protected context without identifiers in arguments", ReplaysLatestContext);
            TestHarness.Test("CLI runs an exact shared context request and writes a result file", RunsSharedContextRequest);
            TestHarness.Test("CLI accepts a Windows PowerShell UTF-8 request file", RunsBomEncodedSharedContextRequest);
            TestHarness.Test("CLI rejects an unsafe shared context request id", RejectsUnsafeContextRequestId);
            TestHarness.Test("Hub startup consumes the current user's pending context request", RunsPendingContextRequest);
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

        private static void RunsEnvironmentContextSeries()
        {
            WithFixture((settingsPath, historyPath) =>
            {
                var sequencePath = Path.Combine(Path.GetDirectoryName(settingsPath), "sequence.txt");
                var leakPath = Path.Combine(Path.GetDirectoryName(settingsPath), "context-leak.txt");
                Environment.SetEnvironmentVariable("RUNNER_FIXTURE_SEQUENCE", sequencePath);
                Environment.SetEnvironmentVariable("RUNNER_FIXTURE_CONTEXT_LEAK", leakPath);
                Environment.SetEnvironmentVariable(ContextCommandLineRunner.ContextsEnvironmentKey,
                    "{\"Contexts\":[" +
                    "{\"PatientId\":\"SYN-BATCH-1\",\"CourseId\":\"C1\",\"PlanId\":\"P1\"}," +
                    "{\"PatientId\":\"SYN-BATCH-2\",\"CourseId\":\"C2\",\"PlanId\":\"P2\"}]}");
                try
                {
                    var arguments = new[] { "--run-contexts", "direct", "--settings", settingsPath };
                    TestHarness.AssertFalse(string.Join(" ", arguments).Contains("SYN-BATCH"));
                    TestHarness.AssertEqual(0, ContextCommandLineRunner.Run(arguments));
                    TestHarness.AssertEqual(2, File.ReadAllLines(sequencePath).Length);
                    TestHarness.AssertEqual("cleared", File.ReadAllLines(leakPath)[0]);
                    TestHarness.AssertEqual("cleared", File.ReadAllLines(leakPath)[1]);
                }
                finally
                {
                    Environment.SetEnvironmentVariable(ContextCommandLineRunner.ContextsEnvironmentKey, null);
                    Environment.SetEnvironmentVariable("RUNNER_FIXTURE_SEQUENCE", null);
                    Environment.SetEnvironmentVariable("RUNNER_FIXTURE_CONTEXT_LEAK", null);
                }
            });
        }

        private static void RunsSharedContextRequest()
        {
            WithFixture((settingsPath, historyPath) =>
            {
                var directory = Path.GetDirectoryName(settingsPath);
                var requestDirectory = Path.Combine(directory, "requests");
                Directory.CreateDirectory(requestDirectory);
                var requestId = "debug-" + Guid.NewGuid().ToString("N");
                var requestPath = Path.Combine(requestDirectory, requestId + ".request.json");
                var resultPath = Path.Combine(requestDirectory, requestId + ".result.json");
                File.WriteAllText(requestPath,
                    "{\"ApplicationId\":\"direct\",\"Contexts\":[" +
                    "{\"PatientId\":\"SYN-REQUEST\",\"CourseId\":\"C7\",\"PlanId\":\"P7\",\"StructureSetId\":\"SS7\",\"ImageId\":\"IMG7\"}]}");

                var arguments = new[] { ContextCommandLineRunner.RunRequestOption, requestId, "--settings", settingsPath };
                TestHarness.AssertFalse(string.Join(" ", arguments).Contains("SYN-REQUEST"));
                TestHarness.AssertEqual(0, ContextCommandLineRunner.Run(arguments));
                TestHarness.AssertTrue(File.Exists(requestPath));
                TestHarness.AssertTrue(File.Exists(resultPath));
                var result = File.ReadAllText(resultPath);
                TestHarness.AssertContains(result, "direct");
                TestHarness.AssertContains(result, "0");
            });
        }

        private static void RejectsUnsafeContextRequestId()
        {
            WithFixture((settingsPath, historyPath) =>
            {
                var arguments = new[] { ContextCommandLineRunner.RunRequestOption, "..\\outside", "--settings", settingsPath };
                TestHarness.AssertEqual(2, ContextCommandLineRunner.Run(arguments));
            });
        }

        private static void RunsBomEncodedSharedContextRequest()
        {
            WithFixture((settingsPath, historyPath) =>
            {
                var directory = Path.GetDirectoryName(settingsPath);
                var requestDirectory = Path.Combine(directory, "requests");
                Directory.CreateDirectory(requestDirectory);
                var requestId = "debug-bom-" + Guid.NewGuid().ToString("N");
                var requestPath = Path.Combine(requestDirectory, requestId + ".request.json");
                var payload = "{\"ApplicationId\":\"direct\",\"Contexts\":[{\"PatientId\":\"SYN-BOM\",\"CourseId\":\"C7\",\"PlanId\":\"P7\"}]}";
                var preamble = Encoding.UTF8.GetPreamble();
                var content = Encoding.UTF8.GetBytes(payload);
                var bytes = new byte[preamble.Length + content.Length];
                Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
                Buffer.BlockCopy(content, 0, bytes, preamble.Length, content.Length);
                File.WriteAllBytes(requestPath, bytes);

                var arguments = new[] { ContextCommandLineRunner.RunRequestOption, requestId, "--settings", settingsPath };
                TestHarness.AssertEqual(0, ContextCommandLineRunner.Run(arguments));
            });
        }

        private static void RunsPendingContextRequest()
        {
            WithFixture((settingsPath, historyPath) =>
            {
                var requestDirectory = Path.Combine(Path.GetDirectoryName(settingsPath), "requests");
                Directory.CreateDirectory(requestDirectory);
                var requestId = "pending-" + Guid.NewGuid().ToString("N");
                File.WriteAllText(Path.Combine(requestDirectory, requestId + ".request.json"),
                    "{\"ApplicationId\":\"direct\",\"Contexts\":[{\"PatientId\":\"SYN-PENDING\",\"CourseId\":\"C1\",\"PlanId\":\"P1\"}]}");
                var pendingPath = Path.Combine(requestDirectory, Environment.UserName + ".pending");
                File.WriteAllText(pendingPath, requestId);

                int exitCode;
                TestHarness.AssertTrue(ContextCommandLineRunner.TryRunPending(new[] { "--settings", settingsPath }, out exitCode));
                TestHarness.AssertEqual(0, exitCode);
                TestHarness.AssertFalse(File.Exists(pendingPath));
                TestHarness.AssertTrue(File.Exists(Path.Combine(requestDirectory, requestId + ".result.json")));
            });
        }

        private static void RejectsWriteEnabledContextSeries()
        {
            WithFixture((settingsPath, historyPath) =>
            {
                var sequencePath = Path.Combine(Path.GetDirectoryName(settingsPath), "write-sequence.txt");
                Environment.SetEnvironmentVariable("RUNNER_FIXTURE_SEQUENCE", sequencePath);
                Environment.SetEnvironmentVariable(ContextCommandLineRunner.ContextsEnvironmentKey,
                    "{\"Contexts\":[{\"PatientId\":\"SYN-WRITE\",\"CourseId\":\"C1\",\"PlanId\":\"P1\"}]}");
                try
                {
                    var arguments = new[] { "--run-contexts", "write", "--settings", settingsPath };
                    TestHarness.AssertEqual(2, ContextCommandLineRunner.Run(arguments));
                    TestHarness.AssertFalse(File.Exists(sequencePath));
                }
                finally
                {
                    Environment.SetEnvironmentVariable(ContextCommandLineRunner.ContextsEnvironmentKey, null);
                    Environment.SetEnvironmentVariable("RUNNER_FIXTURE_SEQUENCE", null);
                }
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
                    "\nHistoryFile=" + historyPath + "\nContextRequestDirectory=" + Path.Combine(directory, "requests") +
                    "\nHistoryRetentionDays=30\nHistoryMaxEntries=100" +
                    "\n[Application.direct]\nName=Direct\nExecutable=Tool.esapi.dll" +
                    "\nLaunchKind=EsapiContextScript\nScriptEngine=Eclipse\nContextRequirement=Plan" +
                    "\nScopeMode=Single\nWriteMode=ReadOnly\nPatientMode=Required\nPatientTransport=None\nEnabled=true\n");
                File.AppendAllText(settingsPath,
                    "[Application.write]\nName=Write\nExecutable=Tool.esapi.dll" +
                    "\nLaunchKind=EsapiContextScript\nScriptEngine=Eclipse\nContextRequirement=Plan" +
                    "\nScopeMode=Single\nWriteMode=ConfirmSave\nPatientMode=Required\nPatientTransport=None\nEnabled=true\n");
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
