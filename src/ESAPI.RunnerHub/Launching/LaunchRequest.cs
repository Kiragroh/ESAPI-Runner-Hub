using System;
using System.Collections.Generic;

namespace EsapiRunnerHub.Launching
{
    public sealed class LaunchRequest
    {
        public LaunchRequest(string applicationId, string executablePath, string workingDirectory, string arguments)
        {
            ApplicationId = applicationId ?? string.Empty;
            ExecutablePath = executablePath ?? string.Empty;
            WorkingDirectory = workingDirectory ?? string.Empty;
            Arguments = arguments ?? string.Empty;
            EnvironmentVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            LogSummary = "start app=" + ApplicationId + " patient=no";
        }

        public string ApplicationId { get; private set; }

        public string ExecutablePath { get; private set; }

        public string WorkingDirectory { get; private set; }

        public string Arguments { get; internal set; }

        public IDictionary<string, string> EnvironmentVariables { get; private set; }

        public string LogSummary { get; internal set; }
    }
}
