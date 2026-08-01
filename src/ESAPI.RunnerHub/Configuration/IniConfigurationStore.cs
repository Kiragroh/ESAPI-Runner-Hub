using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace EsapiRunnerHub.Configuration
{
    public static class IniConfigurationStore
    {
        public static HubConfiguration Load(string path)
        {
            return ParseText(File.ReadAllText(path, Encoding.UTF8), path);
        }

        public static HubConfiguration ParseText(string text, string sourcePath)
        {
            if (sourcePath == null)
            {
                throw new ArgumentNullException(nameof(sourcePath));
            }

            var sections = ParseSections(text ?? string.Empty);
            var configuration = new HubConfiguration { SourcePath = Path.GetFullPath(sourcePath) };
            Dictionary<string, string> hubValues;
            if (sections.TryGetValue("Hub", out hubValues))
            {
                ApplyHubValues(configuration.Hub, hubValues);
            }

            foreach (var section in sections.Where(pair => pair.Key.StartsWith("Application.", StringComparison.OrdinalIgnoreCase)))
            {
                var application = new ApplicationDefinition { Id = section.Key.Substring("Application.".Length) };
                ApplyApplicationValues(application, section.Value);
                configuration.Applications.Add(application);
            }

            configuration.ResolvePaths();
            return configuration;
        }

        public static void SaveAtomic(HubConfiguration configuration, string path)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            Directory.CreateDirectory(directory);
            var temporaryPath = fullPath + ".tmp";
            var backupPath = fullPath + ".bak";
            try
            {
                File.WriteAllText(temporaryPath, Serialize(configuration), new UTF8Encoding(false));
                if (File.Exists(fullPath))
                {
                    File.Replace(temporaryPath, fullPath, backupPath, true);
                    if (File.Exists(backupPath))
                    {
                        File.Delete(backupPath);
                    }
                }
                else
                {
                    File.Move(temporaryPath, fullPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        public static string Serialize(HubConfiguration configuration)
        {
            var builder = new StringBuilder();
            builder.AppendLine("[Hub]");
            Append(builder, "EsapiApiAssembly", configuration.Hub.EsapiApiAssembly);
            Append(builder, "EsapiTypesAssembly", configuration.Hub.EsapiTypesAssembly);
            Append(builder, "SearchMaxResults", configuration.Hub.SearchMaxResults.ToString(CultureInfo.InvariantCulture));
            Append(builder, "SearchDebounceMs", configuration.Hub.SearchDebounceMs.ToString(CultureInfo.InvariantCulture));
            Append(builder, "PathProbeTimeoutMs", configuration.Hub.PathProbeTimeoutMs.ToString(CultureInfo.InvariantCulture));
            Append(builder, "LogDirectory", configuration.Hub.LogDirectory);
            AppendExtras(builder, configuration.Hub.ExtraValues);

            foreach (var application in configuration.Applications.OrderBy(app => app.SortOrder).ThenBy(app => app.Name, StringComparer.OrdinalIgnoreCase))
            {
                builder.AppendLine();
                builder.AppendLine("[Application." + application.Id + "]");
                Append(builder, "Name", application.Name);
                Append(builder, "Category", application.Category);
                Append(builder, "Description", application.Description);
                Append(builder, "Executable", application.Executable);
                Append(builder, "WorkingDirectory", application.WorkingDirectory);
                Append(builder, "Arguments", application.Arguments);
                Append(builder, "PatientMode", application.PatientMode.ToString());
                Append(builder, "PatientTransport", application.PatientTransport.ToString());
                Append(builder, "PatientArgumentTemplate", application.PatientArgumentTemplate);
                Append(builder, "PatientEnvironmentKey", application.PatientEnvironmentKey);
                Append(builder, "Enabled", application.Enabled ? "true" : "false");
                Append(builder, "SortOrder", application.SortOrder.ToString(CultureInfo.InvariantCulture));
                AppendExtras(builder, application.ExtraValues);
            }

            return builder.ToString();
        }

        private static Dictionary<string, Dictionary<string, string>> ParseSections(string text)
        {
            var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> current = null;
            using (var reader = new StringReader(text))
            {
                string line;
                var lineNumber = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    var trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith(";", StringComparison.Ordinal) || trimmed.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
                    {
                        var name = trimmed.Substring(1, trimmed.Length - 2).Trim();
                        if (name.Length == 0 || sections.ContainsKey(name))
                        {
                            throw new FormatException("Invalid or duplicate section at line " + lineNumber + ".");
                        }

                        current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        sections.Add(name, current);
                        continue;
                    }

                    var separator = trimmed.IndexOf('=');
                    if (current == null || separator <= 0)
                    {
                        throw new FormatException("Invalid INI value at line " + lineNumber + ".");
                    }

                    var key = trimmed.Substring(0, separator).Trim();
                    current[key] = trimmed.Substring(separator + 1).Trim();
                }
            }

            return sections;
        }

        private static void ApplyHubValues(HubSettings hub, IDictionary<string, string> values)
        {
            foreach (var pair in values)
            {
                switch (pair.Key.ToLowerInvariant())
                {
                    case "esapiapiassembly": hub.EsapiApiAssembly = pair.Value; break;
                    case "esapitypesassembly": hub.EsapiTypesAssembly = pair.Value; break;
                    case "searchmaxresults": hub.SearchMaxResults = ParseInt(pair.Key, pair.Value); break;
                    case "searchdebouncems": hub.SearchDebounceMs = ParseInt(pair.Key, pair.Value); break;
                    case "pathprobetimeoutms": hub.PathProbeTimeoutMs = ParseInt(pair.Key, pair.Value); break;
                    case "logdirectory": hub.LogDirectory = pair.Value; break;
                    default: hub.ExtraValues[pair.Key] = pair.Value; break;
                }
            }
        }

        private static void ApplyApplicationValues(ApplicationDefinition application, IDictionary<string, string> values)
        {
            foreach (var pair in values)
            {
                switch (pair.Key.ToLowerInvariant())
                {
                    case "name": application.Name = pair.Value; break;
                    case "category": application.Category = pair.Value; break;
                    case "description": application.Description = pair.Value; break;
                    case "executable": application.Executable = pair.Value; break;
                    case "workingdirectory": application.WorkingDirectory = pair.Value; break;
                    case "arguments": application.Arguments = pair.Value; break;
                    case "patientmode": application.PatientMode = ParseEnum<PatientMode>(pair.Key, pair.Value); break;
                    case "patienttransport": application.PatientTransport = ParseEnum<PatientTransport>(pair.Key, pair.Value); break;
                    case "patientargumenttemplate": application.PatientArgumentTemplate = pair.Value; break;
                    case "patientenvironmentkey": application.PatientEnvironmentKey = pair.Value; break;
                    case "enabled": application.Enabled = ParseBool(pair.Key, pair.Value); break;
                    case "sortorder": application.SortOrder = ParseInt(pair.Key, pair.Value); break;
                    default: application.ExtraValues[pair.Key] = pair.Value; break;
                }
            }
        }

        private static int ParseInt(string key, string value)
        {
            int parsed;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                throw new FormatException(key + " must be an integer.");
            }

            return parsed;
        }

        private static bool ParseBool(string key, string value)
        {
            bool parsed;
            if (!bool.TryParse(value, out parsed))
            {
                throw new FormatException(key + " must be true or false.");
            }

            return parsed;
        }

        private static T ParseEnum<T>(string key, string value)
            where T : struct
        {
            T parsed;
            if (!Enum.TryParse(value, true, out parsed) || !Enum.IsDefined(typeof(T), parsed))
            {
                throw new FormatException(key + " has an unsupported value.");
            }

            return parsed;
        }

        private static void Append(StringBuilder builder, string key, string value)
        {
            builder.Append(key).Append('=').AppendLine(value ?? string.Empty);
        }

        private static void AppendExtras(StringBuilder builder, IDictionary<string, string> extras)
        {
            foreach (var pair in extras.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                Append(builder, pair.Key, pair.Value);
            }
        }
    }
}
