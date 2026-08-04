using System;
using System.IO;
using System.Linq;

namespace RunnerFixture
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var sequencePath = Environment.GetEnvironmentVariable("RUNNER_FIXTURE_SEQUENCE");
            if (!string.IsNullOrWhiteSpace(sequencePath))
            {
                File.AppendAllText(sequencePath, "run" + Environment.NewLine);
            }
            var contextLeakPath = Environment.GetEnvironmentVariable("RUNNER_FIXTURE_CONTEXT_LEAK");
            if (!string.IsNullOrWhiteSpace(contextLeakPath))
            {
                var value = Environment.GetEnvironmentVariable("ESAPI_RUNNER_CONTEXTS");
                File.AppendAllText(contextLeakPath, (string.IsNullOrEmpty(value) ? "cleared" : "present") + Environment.NewLine);
            }

            var mode = ValueAfter(args, "--mode") ?? "success";
            if (string.Equals(mode, "exit7", StringComparison.OrdinalIgnoreCase))
            {
                return 7;
            }

            if (string.Equals(mode, "crash", StringComparison.OrdinalIgnoreCase))
            {
                Environment.FailFast("Synthetic fixture crash.");
            }

            if (string.Equals(mode, "capture", StringComparison.OrdinalIgnoreCase))
            {
                var path = ValueAfter(args, "--capture");
                File.WriteAllLines(path, new[]
                {
                    "args=" + string.Join("|", args),
                    "patient=" + (Environment.GetEnvironmentVariable("RUNNER_PATIENT_ID") ?? string.Empty)
                });
            }

            return 0;
        }

        private static string ValueAfter(string[] args, string key)
        {
            var index = Array.FindIndex(args, value => string.Equals(value, key, StringComparison.OrdinalIgnoreCase));
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
