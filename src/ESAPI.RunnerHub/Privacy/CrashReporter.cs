using System;
using System.IO;
using System.Text;

namespace EsapiRunnerHub.Privacy
{
    public sealed class CrashReporter
    {
        private readonly string directory;

        public CrashReporter(string directory)
        {
            this.directory = Path.GetFullPath(directory);
            Directory.CreateDirectory(this.directory);
        }

        public string Write(Exception exception)
        {
            var path = Path.Combine(directory, "crash-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".txt");
            var builder = new StringBuilder();
            builder.AppendLine("ESAPI Runner Hub privacy-safe crash report");
            builder.AppendLine("UTC=" + DateTime.UtcNow.ToString("o"));
            builder.AppendLine("ExceptionType=" + (exception == null ? "Unknown" : exception.GetType().FullName));
            builder.AppendLine("HResult=" + (exception == null ? "0" : exception.HResult.ToString()));
            builder.AppendLine("Is64BitProcess=" + Environment.Is64BitProcess);
            builder.AppendLine("Note=Exception messages, command lines, environment values, patient data, and search text are intentionally omitted.");
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
            return path;
        }
    }
}
