using System;
using System.Collections.Generic;
using System.IO;

namespace EsapiRunnerHub.Configuration
{
    public sealed class HubSettings
    {
        public HubSettings()
        {
            SearchMaxResults = 10;
            SearchDebounceMs = 150;
            PathProbeTimeoutMs = 1500;
            LogDirectory = @"%LOCALAPPDATA%\ESAPI Runner Hub\Logs";
            ExtraValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public string EsapiApiAssembly { get; set; }
        public string EsapiTypesAssembly { get; set; }
        public string ResolvedEsapiApiAssembly { get; internal set; }
        public string ResolvedEsapiTypesAssembly { get; internal set; }
        public int SearchMaxResults { get; set; }
        public int SearchDebounceMs { get; set; }
        public int PathProbeTimeoutMs { get; set; }
        public string LogDirectory { get; set; }
        public string ResolvedLogDirectory { get; internal set; }
        public IDictionary<string, string> ExtraValues { get; private set; }
    }

    public sealed class HubConfiguration
    {
        public HubConfiguration()
        {
            Hub = new HubSettings();
            Applications = new List<ApplicationDefinition>();
        }

        public string SourcePath { get; internal set; }
        public HubSettings Hub { get; private set; }
        public IList<ApplicationDefinition> Applications { get; private set; }

        public ConfigurationValidationResult Validate()
        {
            return ConfigurationValidator.Validate(this);
        }

        internal void ResolvePaths()
        {
            var fullSourcePath = Path.GetFullPath(SourcePath);
            var baseDirectory = Path.GetDirectoryName(fullSourcePath);
            Hub.ResolvedEsapiApiAssembly = ResolvePath(baseDirectory, Hub.EsapiApiAssembly);
            Hub.ResolvedEsapiTypesAssembly = ResolvePath(baseDirectory, Hub.EsapiTypesAssembly);
            Hub.ResolvedLogDirectory = ResolvePath(baseDirectory, Hub.LogDirectory);
            foreach (var application in Applications)
            {
                application.ResolvePaths(baseDirectory);
            }
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

    public sealed class ConfigurationValidationResult
    {
        public ConfigurationValidationResult(IList<string> errors)
        {
            Errors = errors ?? throw new ArgumentNullException(nameof(errors));
        }

        public IList<string> Errors { get; private set; }
        public bool IsValid { get { return Errors.Count == 0; } }
    }
}
