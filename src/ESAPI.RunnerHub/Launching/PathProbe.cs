using System;
using System.IO;
using System.Threading.Tasks;

namespace EsapiRunnerHub.Launching
{
    public enum PathReadiness
    {
        Ready,
        Missing,
        Unavailable
    }

    public sealed class PathProbeResult
    {
        public PathProbeResult(PathReadiness readiness, string message)
        {
            Readiness = readiness;
            Message = message ?? string.Empty;
        }

        public PathReadiness Readiness { get; private set; }

        public string Message { get; private set; }
    }

    public sealed class PathProbe
    {
        public async Task<PathProbeResult> ProbeAsync(string path, int timeoutMs)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return new PathProbeResult(PathReadiness.Missing, "No executable path is configured.");
            }

            var probe = Task.Run(() => File.Exists(path));
            var completed = await Task.WhenAny(probe, Task.Delay(Math.Max(100, timeoutMs))).ConfigureAwait(false);
            if (completed != probe)
            {
                return new PathProbeResult(PathReadiness.Unavailable, "Path check timed out.");
            }

            try
            {
                if (await probe.ConfigureAwait(false))
                {
                    return new PathProbeResult(PathReadiness.Ready, "Ready");
                }

                var isNetworkPath = path.StartsWith(@"\\", StringComparison.Ordinal);
                return new PathProbeResult(
                    isNetworkPath ? PathReadiness.Unavailable : PathReadiness.Missing,
                    isNetworkPath ? "Network path is unavailable." : "Executable was not found.");
            }
            catch (Exception)
            {
                return new PathProbeResult(PathReadiness.Unavailable, "Path could not be checked.");
            }
        }
    }
}
