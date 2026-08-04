using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using EsapiRunnerHub.Privacy;

namespace EsapiRunnerHub.Tests
{
    internal static class PrivacyDiagnosticsTests
    {
        public static void Register()
        {
            TestHarness.Test("technical logs retain categories but redact exception details", RedactsPatientDetails);
            TestHarness.Test("technical log write failures never escape into application flow", ContainsWriteFailures);
            TestHarness.Test("technical logging performs no filesystem work on the caller", AvoidsCallerFilesystemIo);
            TestHarness.Test("crash report contains no patient or expanded arguments", RedactsCrashReport);
        }

        private static void RedactsPatientDetails()
        {
            var directory = Path.Combine(Path.GetTempPath(), "runner-hub-log-" + Guid.NewGuid().ToString("N"));
            try
            {
                var log = new TechnicalLog(directory);
                log.Write("WARN", "network_path_unavailable", "review", new IOException("SYN-1001 Ada Example --patient-id SYN-1001"));
                log.Write("WARN", "esapi_unavailable", string.Empty, null);
                var text = WaitForLog(log.FilePath, "esapi_unavailable");

                TestHarness.AssertContains(text, "network_path_unavailable");
                TestHarness.AssertContains(text, "esapi_unavailable");
                TestHarness.AssertContains(text, "IOException");
                TestHarness.AssertFalse(text.Contains("SYN-1001"));
                TestHarness.AssertFalse(text.Contains("Ada Example"));
                TestHarness.AssertFalse(text.Contains("--patient-id"));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static void RedactsCrashReport()
        {
            var directory = Path.Combine(Path.GetTempPath(), "runner-hub-crash-" + Guid.NewGuid().ToString("N"));
            try
            {
                var report = new CrashReporter(directory).Write(new InvalidOperationException("SYN-1002 Linus Sample"));
                var text = File.ReadAllText(report);

                TestHarness.AssertContains(text, "InvalidOperationException");
                TestHarness.AssertFalse(text.Contains("SYN-1002"));
                TestHarness.AssertFalse(text.Contains("Linus Sample"));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static void ContainsWriteFailures()
        {
            var directory = Path.Combine(Path.GetTempPath(), "runner-hub-log-blocked-" + Guid.NewGuid().ToString("N"));
            var log = new TechnicalLog(directory);
            File.WriteAllText(directory, "This file deliberately blocks recreation of the log directory.");
            try
            {
                log.Write("INFO", "child_started", "fixture", null);
            }
            finally
            {
                File.Delete(directory);
            }
        }

        private static void AvoidsCallerFilesystemIo()
        {
            var directory = Path.Combine(Path.GetTempPath(), "runner-hub-log-lazy-" + Guid.NewGuid().ToString("N"));
            var log = new TechnicalLog(directory);
            TestHarness.AssertFalse(Directory.Exists(directory), "Logger construction unexpectedly touched the filesystem.");

            File.WriteAllText(directory, "This file deliberately blocks recreation of the log directory.");
            try
            {
                var timer = Stopwatch.StartNew();
                log.Write("INFO", "child_started", "fixture", null);
                timer.Stop();
                TestHarness.AssertTrue(timer.ElapsedMilliseconds < 250, "Logging blocked the caller for " + timer.ElapsedMilliseconds + " ms.");
                Thread.Sleep(100);
            }
            finally
            {
                File.Delete(directory);
            }
        }

        private static string WaitForLog(string path, string expected)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        var text = File.ReadAllText(path);
                        if (text.Contains(expected)) return text;
                    }
                    catch (IOException)
                    {
                        // The background writer may still own its short append window.
                    }
                }

                Thread.Sleep(20);
            }

            throw new InvalidOperationException("Timed out waiting for technical log event: " + expected);
        }
    }
}
