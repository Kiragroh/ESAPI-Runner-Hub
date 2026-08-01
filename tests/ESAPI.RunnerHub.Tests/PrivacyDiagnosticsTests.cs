using System;
using System.IO;
using EsapiRunnerHub.Privacy;

namespace EsapiRunnerHub.Tests
{
    internal static class PrivacyDiagnosticsTests
    {
        public static void Register()
        {
            TestHarness.Test("technical logs retain categories but redact exception details", RedactsPatientDetails);
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
                var text = File.ReadAllText(log.FilePath);

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
    }
}
