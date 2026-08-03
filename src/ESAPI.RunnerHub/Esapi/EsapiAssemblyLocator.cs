using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EsapiRunnerHub.Esapi
{
    public sealed class EsapiAssemblyLocation
    {
        internal EsapiAssemblyLocation(string apiAssemblyPath, string typesAssemblyPath, string source, string rtmVersion)
        {
            ApiAssemblyPath = apiAssemblyPath ?? string.Empty;
            TypesAssemblyPath = typesAssemblyPath ?? string.Empty;
            Source = source ?? string.Empty;
            RtmVersion = rtmVersion ?? string.Empty;
        }

        public bool IsAvailable { get { return File.Exists(ApiAssemblyPath); } }
        public string ApiAssemblyPath { get; private set; }
        public string TypesAssemblyPath { get; private set; }
        public string Source { get; private set; }
        public string RtmVersion { get; private set; }
    }

    public sealed class EsapiAssemblyLocator
    {
        private const string ApiFileName = "VMS.TPS.Common.Model.API.dll";
        private const string TypesFileName = "VMS.TPS.Common.Model.Types.dll";

        public EsapiAssemblyLocation Locate(string configuredApiPath, string configuredTypesPath)
        {
            return Locate(configuredApiPath, configuredTypesPath, GetDefaultRtmRoots());
        }

        public EsapiAssemblyLocation Locate(string configuredApiPath, string configuredTypesPath, IEnumerable<string> rtmRoots)
        {
            if (IsCompleteConfiguredPair(configuredApiPath, configuredTypesPath))
            {
                return new EsapiAssemblyLocation(
                    Path.GetFullPath(configuredApiPath),
                    string.IsNullOrWhiteSpace(configuredTypesPath) ? string.Empty : Path.GetFullPath(configuredTypesPath),
                    "configured",
                    string.Empty);
            }

            foreach (var candidate in FindCandidates(rtmRoots).OrderByDescending(item => item.Version))
            {
                var apiPath = Path.Combine(candidate.Directory, "esapi", "API", ApiFileName);
                var typesPath = Path.Combine(candidate.Directory, "esapi", "API", TypesFileName);
                if (File.Exists(apiPath) && File.Exists(typesPath))
                {
                    return new EsapiAssemblyLocation(apiPath, typesPath, "auto", candidate.Name);
                }
            }

            return new EsapiAssemblyLocation(configuredApiPath, configuredTypesPath, string.Empty, string.Empty);
        }

        private static bool IsCompleteConfiguredPair(string apiPath, string typesPath)
        {
            return !string.IsNullOrWhiteSpace(apiPath) &&
                   File.Exists(apiPath) &&
                   (string.IsNullOrWhiteSpace(typesPath) || File.Exists(typesPath));
        }

        private static IEnumerable<string> GetDefaultRtmRoots()
        {
            return new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
                }
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => Path.Combine(path, "Varian", "RTM"))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static IEnumerable<VersionDirectory> FindCandidates(IEnumerable<string> rtmRoots)
        {
            var candidates = new List<VersionDirectory>();
            foreach (var root in rtmRoots ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                {
                    continue;
                }

                try
                {
                    foreach (var directory in Directory.GetDirectories(root))
                    {
                        Version version;
                        var name = Path.GetFileName(directory);
                        if (Version.TryParse(name, out version))
                        {
                            candidates.Add(new VersionDirectory(directory, name, version));
                        }
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            return candidates;
        }

        private sealed class VersionDirectory
        {
            public VersionDirectory(string directory, string name, Version version)
            {
                Directory = directory;
                Name = name;
                Version = version;
            }

            public string Directory { get; private set; }
            public string Name { get; private set; }
            public Version Version { get; private set; }
        }
    }
}
