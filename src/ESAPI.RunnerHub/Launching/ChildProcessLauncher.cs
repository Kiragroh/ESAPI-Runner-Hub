using System;
using System.Diagnostics;
using System.IO;

namespace EsapiRunnerHub.Launching
{
    public sealed class ChildProcessLauncher
    {
        public RunningProcessInfo Start(LaunchRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!string.Equals(Path.GetExtension(request.ExecutablePath), ".exe", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only executable files can be launched.");
            }

            if (!File.Exists(request.ExecutablePath))
            {
                throw new FileNotFoundException("Configured executable was not found.", request.ExecutablePath);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = request.ExecutablePath,
                WorkingDirectory = string.IsNullOrWhiteSpace(request.WorkingDirectory)
                    ? Path.GetDirectoryName(request.ExecutablePath)
                    : request.WorkingDirectory,
                Arguments = request.Arguments,
                UseShellExecute = false,
                CreateNoWindow = false
            };
            foreach (var pair in request.EnvironmentVariables)
            {
                startInfo.EnvironmentVariables[pair.Key] = pair.Value;
            }

            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            if (!process.Start())
            {
                process.Dispose();
                throw new InvalidOperationException("The child process could not be started.");
            }

            var information = new RunningProcessInfo(request.ApplicationId, process);
            process.Exited += (sender, args) => information.MarkExited();
            if (process.HasExited)
            {
                information.MarkExited();
            }

            return information;
        }
    }
}
