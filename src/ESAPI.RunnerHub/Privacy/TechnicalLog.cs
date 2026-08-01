using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace EsapiRunnerHub.Privacy
{
    public sealed class TechnicalLog
    {
        private static readonly object StaticSync = new object();
        private static TechnicalLog current = new TechnicalLog(DefaultDirectory());
        private readonly object sync = new object();

        public TechnicalLog(string directory)
        {
            var resolved = string.IsNullOrWhiteSpace(directory) ? DefaultDirectory() : Path.GetFullPath(directory);
            Directory.CreateDirectory(resolved);
            FilePath = Path.Combine(resolved, "runner-hub.log");
        }

        public string FilePath { get; private set; }

        public static TechnicalLog Current
        {
            get { lock (StaticSync) return current; }
        }

        public static void Configure(string directory)
        {
            lock (StaticSync)
            {
                try
                {
                    current = new TechnicalLog(directory);
                }
                catch (Exception)
                {
                    current = new TechnicalLog(DefaultDirectory());
                }
            }
        }

        public void Write(string level, string eventCode, string applicationId, Exception exception)
        {
            var line = string.Join("\t", new[]
            {
                DateTime.UtcNow.ToString("o"),
                SafeToken(level, "INFO"),
                SafeToken(eventCode, "technical_event"),
                "app=" + SafeToken(applicationId, "-"),
                "exception=" + (exception == null ? "-" : SafeToken(exception.GetType().Name, "Exception"))
            }) + Environment.NewLine;
            lock (sync)
            {
                File.AppendAllText(FilePath, line, new UTF8Encoding(false));
            }
        }

        private static string SafeToken(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            var sanitized = Regex.Replace(value, "[^A-Za-z0-9._-]", "_");
            return sanitized.Length > 80 ? sanitized.Substring(0, 80) : sanitized;
        }

        private static string DefaultDirectory()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ESAPI Runner Hub", "Logs");
        }
    }
}
