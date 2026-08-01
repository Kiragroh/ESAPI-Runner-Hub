using System;
using System.Collections.Generic;
using System.IO;

namespace EsapiRunnerHub.Configuration
{
    public enum PatientMode
    {
        None,
        Optional,
        Required
    }

    public enum PatientTransport
    {
        None,
        Argument,
        Environment
    }

    public enum LaunchKind
    {
        Executable,
        EclipsePlugin
    }

    public sealed class ApplicationDefinition
    {
        public ApplicationDefinition()
        {
            ExtraValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Enabled = true;
            LaunchKind = LaunchKind.Executable;
            PatientMode = PatientMode.None;
            PatientTransport = PatientTransport.None;
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Executable { get; set; }
        public string WorkingDirectory { get; set; }
        public string Arguments { get; set; }
        public LaunchKind LaunchKind { get; set; }
        public PatientMode PatientMode { get; set; }
        public PatientTransport PatientTransport { get; set; }
        public string PatientArgumentTemplate { get; set; }
        public string PatientEnvironmentKey { get; set; }
        public bool Enabled { get; set; }
        public int SortOrder { get; set; }
        public string ResolvedExecutable { get; private set; }
        public string ResolvedWorkingDirectory { get; private set; }
        public IDictionary<string, string> ExtraValues { get; private set; }

        internal void ResolvePaths(string baseDirectory)
        {
            ResolvedExecutable = ResolvePath(baseDirectory, Executable);
            ResolvedWorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirectory)
                ? Path.GetDirectoryName(ResolvedExecutable ?? string.Empty)
                : ResolvePath(baseDirectory, WorkingDirectory);
        }

        private static string ResolvePath(string baseDirectory, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
            return Path.GetFullPath(Path.IsPathRooted(expanded)
                ? expanded
                : Path.Combine(baseDirectory, expanded));
        }
    }
}

