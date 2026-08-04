using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EsapiScriptHost.Host
{
    public static class HostTechnicalLog
    {
        public static string Write(string directory, string stage, string applicationId, string reasonCode, Exception exception)
        {
            var resolved = string.IsNullOrWhiteSpace(directory) ? DefaultDirectory() : Path.GetFullPath(directory);
            Directory.CreateDirectory(resolved);
            var path = Path.Combine(resolved, "script-host-" + SafeToken(Environment.MachineName, "machine") + "-" +
                                              Process.GetCurrentProcess().Id + ".log");
            var root = RootException(exception);
            var line = string.Join("\t", new[]
            {
                DateTime.UtcNow.ToString("o"), "ERROR", "script_host_failed",
                "app=" + SafeToken(applicationId, "-"),
                "stage=" + SafeToken(stage, "unknown"),
                "reason=" + SafeToken(reasonCode, "host_failure"),
                "exception=" + SafeToken(root == null ? null : root.GetType().Name, "Exception")
            }) + Environment.NewLine;
            using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(line);
            }
            return path;
        }

        public static void WriteBestEffort(string configuredDirectory, string stage, string applicationId,
            string reasonCode, Exception exception)
        {
            var local = DefaultDirectory();
            try { Write(local, stage, applicationId, reasonCode, exception); }
            catch { }
            if (string.IsNullOrWhiteSpace(configuredDirectory) || PathsEqual(configuredDirectory, local)) return;
            try
            {
                Task.Run(() =>
                {
                    try { Write(configuredDirectory, stage, applicationId, reasonCode, exception); }
                    catch { }
                }).Wait(1000);
            }
            catch { }
        }

        public static string ReasonCode(Exception exception, string stage)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                var resolution = current as ContextResolutionException;
                if (resolution != null) return resolution.ReasonCode;
                var data = current.Data[ContextResolutionException.ReasonCodeDataKey] as string;
                if (!string.IsNullOrWhiteSpace(data)) return data;
            }
            if ((stage ?? string.Empty).IndexOf("Kontext", StringComparison.OrdinalIgnoreCase) >= 0)
                return "context_resolution_failed";
            if ((stage ?? string.Empty).IndexOf("Skript ausführen", StringComparison.OrdinalIgnoreCase) >= 0)
                return "script_execution_failed";
            if ((stage ?? string.Empty).IndexOf("Sitzung", StringComparison.OrdinalIgnoreCase) >= 0)
                return "esapi_session_failed";
            return "host_failure";
        }

        public static Exception RootException(Exception exception)
        {
            var current = exception;
            while (current is TargetInvocationException && current.InnerException != null) current = current.InnerException;
            return current;
        }

        private static bool PathsEqual(string left, string right)
        {
            try { return string.Equals(Path.GetFullPath(left).TrimEnd('\\'), Path.GetFullPath(right).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        private static string SafeToken(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            var sanitized = Regex.Replace(value, "[^A-Za-z0-9._-]", "_");
            return sanitized.Length > 80 ? sanitized.Substring(0, 80) : sanitized;
        }

        private static string DefaultDirectory()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ESAPI Runner Hub", "Logs");
        }
    }
}
