using System;
using EsapiRunnerHub.Configuration;

namespace EsapiRunnerHub.Launching
{
    public static class ScriptHostSelector
    {
        public static string Select(WriteMode writeMode, HubSettings hub)
        {
            if (hub == null) throw new ArgumentNullException(nameof(hub));
            var path = writeMode == WriteMode.ReadOnly
                ? hub.ResolvedScriptHostExecutable
                : hub.ResolvedWriteScriptHostExecutable;
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException(writeMode == WriteMode.ReadOnly
                    ? "The read-only script host executable is not configured."
                    : "The write-enabled script host executable is not configured.");
            return path;
        }
    }
}
