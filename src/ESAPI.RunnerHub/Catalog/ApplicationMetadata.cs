using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using EsapiRunnerHub.Configuration;

namespace EsapiRunnerHub.Catalog
{
    public static class ApplicationMetadata
    {
        private const string PluginMarker = "\\plugins\\";

        // Reads only the Windows version resource; never loads or executes the target assembly.
        public static string FileVersionFor(string path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                 !path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))) return null;
            try
            {
                var metadata = FileVersionInfo.GetVersionInfo(path);
                var version = string.IsNullOrWhiteSpace(metadata.ProductVersion) ? metadata.FileVersion : metadata.ProductVersion;
                return string.IsNullOrWhiteSpace(version) ? null : version.Trim();
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (SecurityException) { return null; }
            catch (ArgumentException) { return null; }
        }

        public static ApplicationArtifactKind ArtifactFor(ApplicationDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (definition.ArtifactKind != ApplicationArtifactKind.Auto) return definition.ArtifactKind;
            var target = TargetPath(definition);
            if (target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return ApplicationArtifactKind.Standalone;
            if (target.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return ApplicationArtifactKind.SingleFile;
            if (target.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) return ApplicationArtifactKind.Binary;
            return ApplicationArtifactKind.Auto;
        }

        public static ApplicationAccessMode AccessFor(ApplicationDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (definition.AccessMode != ApplicationAccessMode.Auto) return definition.AccessMode;
            if (definition.LaunchKind == LaunchKind.EsapiContextScript)
                return definition.WriteMode != WriteMode.ReadOnly
                    ? ApplicationAccessMode.WriteEnabled
                    : ApplicationAccessMode.ReadOnly;
            return ApplicationAccessMode.Unknown;
        }

        public static string ArtifactLabel(ApplicationArtifactKind kind)
        {
            if (kind == ApplicationArtifactKind.Standalone) return "Standalone";
            if (kind == ApplicationArtifactKind.SingleFile) return "Single-file (.cs)";
            if (kind == ApplicationArtifactKind.Binary) return "Binary (.dll)";
            return "Other";
        }

        public static string AccessLabel(ApplicationAccessMode mode)
        {
            if (mode == ApplicationAccessMode.ReadOnly) return "Read-only";
            if (mode == ApplicationAccessMode.WriteEnabled) return "Write-enabled";
            return "Access unknown";
        }

        public static string CompactPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            var normalized = path.Trim().Replace('/', '\\');
            var markerIndex = normalized.IndexOf(PluginMarker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex >= 0)
                return "plugins\\" + normalized.Substring(markerIndex + PluginMarker.Length);
            try
            {
                return Path.GetFileName(normalized);
            }
            catch
            {
                return normalized;
            }
        }

        public static Uri BuildReadmeUri(string baseUrl, int scriptId)
        {
            Uri baseUri;
            if (scriptId <= 0 || !Uri.TryCreate(baseUrl, UriKind.Absolute, out baseUri) ||
                (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
                return null;
            return new Uri(baseUri, "#/inhouse/" + scriptId);
        }

        private static string TargetPath(ApplicationDefinition definition)
        {
            return string.IsNullOrWhiteSpace(definition.ResolvedExecutable)
                ? definition.Executable ?? string.Empty
                : definition.ResolvedExecutable;
        }
    }
}
