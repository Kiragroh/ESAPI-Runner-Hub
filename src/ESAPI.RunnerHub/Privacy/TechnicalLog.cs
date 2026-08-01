using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EsapiRunnerHub.Privacy
{
    public sealed class TechnicalLog
    {
        private static readonly object StaticSync = new object();
        private static TechnicalLog current = CreateInitial();
        private readonly BlockingCollection<string> pendingLines = new BlockingCollection<string>(256);
        private int workerStarted;

        private TechnicalLog()
        {
            FilePath = string.Empty;
        }

        public TechnicalLog(string directory)
        {
            var resolved = string.IsNullOrWhiteSpace(directory) ? DefaultDirectory() : Path.GetFullPath(directory);
            FilePath = Path.Combine(resolved, "runner-hub-" + SafeToken(Environment.MachineName, "machine") + "-" + Process.GetCurrentProcess().Id + ".log");
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
                    current = CreateInitial();
                }
            }
        }

        public void Write(string level, string eventCode, string applicationId, Exception exception)
        {
            if (string.IsNullOrWhiteSpace(FilePath))
            {
                return;
            }

            var line = string.Join("\t", new[]
            {
                DateTime.UtcNow.ToString("o"),
                SafeToken(level, "INFO"),
                SafeToken(eventCode, "technical_event"),
                "app=" + SafeToken(applicationId, "-"),
                "exception=" + (exception == null ? "-" : SafeToken(exception.GetType().Name, "Exception"))
            }) + Environment.NewLine;

            if (pendingLines.TryAdd(line))
            {
                EnsureWorker();
            }
        }

        private void EnsureWorker()
        {
            if (Interlocked.CompareExchange(ref workerStarted, 1, 0) != 0)
            {
                return;
            }

            Task.Factory.StartNew(ProcessQueue, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void ProcessQueue()
        {
            foreach (var line in pendingLines.GetConsumingEnumerable())
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                    using (var stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    {
                        writer.Write(line);
                    }
                }
                catch (Exception)
                {
                    // Central logging is best-effort and never runs on the caller/UI thread.
                }
            }
        }

        private static TechnicalLog CreateInitial()
        {
            try
            {
                return new TechnicalLog(DefaultDirectory());
            }
            catch (Exception)
            {
                return new TechnicalLog();
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
