using System;
using System.IO;
using System.Threading.Tasks;
using EsapiRunnerHub.Catalog;

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
        public PathProbeResult(PathReadiness readiness, string message, string fileVersion = null, DateTime? fileLastWriteTimeUtc = null)
        {
            Readiness = readiness;
            Message = message ?? string.Empty;
            FileVersion = fileVersion;
            FileLastWriteTimeUtc = fileLastWriteTimeUtc;
        }

        public PathReadiness Readiness { get; private set; }

        public string Message { get; private set; }
        public string FileVersion { get; private set; }
        public DateTime? FileLastWriteTimeUtc { get; private set; }
    }

    public sealed class PathProbe
    {
        private readonly Func<string, string> readFileVersion;

        public PathProbe(Func<string, string> readFileVersion = null)
        {
            this.readFileVersion = readFileVersion ?? ApplicationMetadata.FileVersionFor;
        }

        public async Task<PathProbeResult> ProbeAsync(string path, int timeoutMs)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return new PathProbeResult(PathReadiness.Missing, "No executable path is configured.");
            }

            var confirmedReady = new TaskCompletionSource<PathProbeResult>();
            var probe = Task.Run(() =>
            {
                try
                {
                    var file = new FileInfo(path);
                    if (file.Exists)
                    {
                        var ready = new PathProbeResult(PathReadiness.Ready, "Ready", null, file.LastWriteTimeUtc);
                        confirmedReady.SetResult(ready);
                        return new PathProbeResult(PathReadiness.Ready, "Ready", readFileVersion(path), ready.FileLastWriteTimeUtc);
                    }
                    var isNetworkPath = path.StartsWith(@"\\", StringComparison.Ordinal);
                    return new PathProbeResult(
                        isNetworkPath ? PathReadiness.Unavailable : PathReadiness.Missing,
                        isNetworkPath ? "Network path is unavailable." : "Executable was not found.");
                }
                catch (Exception)
                {
                    return confirmedReady.Task.IsCompleted ? confirmedReady.Task.Result
                        : new PathProbeResult(PathReadiness.Unavailable, "Path could not be checked.");
                }
            });
            var completed = await Task.WhenAny(probe, Task.Delay(Math.Max(100, timeoutMs))).ConfigureAwait(false);
            if (completed != probe)
            {
                return confirmedReady.Task.IsCompleted ? confirmedReady.Task.Result
                    : new PathProbeResult(PathReadiness.Unavailable, "Path check timed out.");
            }

            try
            {
                return await probe.ConfigureAwait(false);
            }
            catch (Exception)
            {
                return new PathProbeResult(PathReadiness.Unavailable, "Path could not be checked.");
            }
        }
    }
}
