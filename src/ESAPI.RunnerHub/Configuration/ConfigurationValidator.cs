using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace EsapiRunnerHub.Configuration
{
    public static class ConfigurationValidator
    {
        private static readonly Regex IdPattern = new Regex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant);
        private static readonly Regex EnvironmentKeyPattern = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

        public static ConfigurationValidationResult Validate(HubConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var errors = new List<string>();
            if (configuration.Hub.SearchMaxResults < 1 || configuration.Hub.SearchMaxResults > 100)
            {
                errors.Add("Hub.SearchMaxResults must be between 1 and 100.");
            }

            if (configuration.Hub.SearchDebounceMs < 0 || configuration.Hub.SearchDebounceMs > 5000)
            {
                errors.Add("Hub.SearchDebounceMs must be between 0 and 5000.");
            }

            if (configuration.Hub.PathProbeTimeoutMs < 100 || configuration.Hub.PathProbeTimeoutMs > 30000)
            {
                errors.Add("Hub.PathProbeTimeoutMs must be between 100 and 30000.");
            }

            foreach (var duplicate in configuration.Applications
                .GroupBy(application => application.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1))
            {
                errors.Add("Duplicate application id: " + duplicate.Key);
            }

            foreach (var application in configuration.Applications)
            {
                var prefix = "Application." + (application.Id ?? string.Empty) + ": ";
                if (string.IsNullOrWhiteSpace(application.Id) || !IdPattern.IsMatch(application.Id))
                {
                    errors.Add(prefix + "invalid section id.");
                }

                if (string.IsNullOrWhiteSpace(application.Name))
                {
                    errors.Add(prefix + "Name is required.");
                }

                if (string.IsNullOrWhiteSpace(application.Executable))
                {
                    errors.Add(prefix + "Executable is required.");
                }
                else if (application.LaunchKind == LaunchKind.Executable &&
                         !ResolvedTarget(application).EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(prefix + "Executable entries must target an .exe file.");
                }
                else if (application.LaunchKind == LaunchKind.EclipsePlugin &&
                         !ResolvedTarget(application).EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
                         !ResolvedTarget(application).EndsWith(".esapi.dll", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(prefix + "EclipsePlugin entries must target a .cs or .esapi.dll file.");
                }
                else if (application.LaunchKind == LaunchKind.EsapiContextScript &&
                         !ResolvedTarget(application).EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
                         !ResolvedTarget(application).EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(prefix + "EsapiContextScript entries must target a .cs or .dll file.");
                }

                if (application.LaunchKind == LaunchKind.EclipsePlugin &&
                    (application.PatientMode != PatientMode.None || application.PatientTransport != PatientTransport.None))
                {
                    errors.Add(prefix + "EclipsePlugin entries must use PatientMode=None and PatientTransport=None because they run inside Eclipse.");
                }

                if (application.LaunchKind == LaunchKind.EsapiContextScript &&
                    application.PatientTransport != PatientTransport.None)
                {
                    errors.Add(prefix + "EsapiContextScript entries must use PatientTransport=None because the private host protocol supplies context.");
                }

                if (application.LaunchKind == LaunchKind.EsapiContextScript &&
                    application.ContextRequirement != ContextRequirement.None &&
                    application.PatientMode != PatientMode.Required)
                {
                    errors.Add(prefix + "EsapiContextScript entries with context must use PatientMode=Required.");
                }

                if (application.LaunchKind == LaunchKind.EsapiContextScript &&
                    application.ContextRequirement == ContextRequirement.None &&
                    application.PatientMode != PatientMode.None)
                {
                    errors.Add(prefix + "EsapiContextScript entries without context must use PatientMode=None.");
                }

                if (application.LaunchKind != LaunchKind.EsapiContextScript &&
                    application.WriteMode != WriteMode.ReadOnly)
                {
                    errors.Add(prefix + "WriteMode=ConfirmSave is supported only for EsapiContextScript entries.");
                }

                if (application.ContextRequirement == ContextRequirement.None &&
                    application.ScopeMode != ScopeMode.None)
                {
                    errors.Add(prefix + "ScopeMode must be None when ContextRequirement is None.");
                }

                if (application.LaunchKind == LaunchKind.Executable &&
                    application.PatientMode == PatientMode.Required && application.PatientTransport == PatientTransport.None)
                {
                    errors.Add(prefix + "PatientTransport is required when PatientMode is Required.");
                }

                if (application.PatientMode == PatientMode.None && application.PatientTransport != PatientTransport.None)
                {
                    errors.Add(prefix + "PatientTransport must be None when PatientMode is None.");
                }

                if (application.PatientTransport == PatientTransport.Argument &&
                    (application.PatientArgumentTemplate ?? string.Empty).IndexOf("{PatientId}", StringComparison.Ordinal) < 0)
                {
                    errors.Add(prefix + "PatientArgumentTemplate must contain {PatientId}.");
                }

                if (application.PatientTransport == PatientTransport.Environment &&
                    !EnvironmentKeyPattern.IsMatch(application.PatientEnvironmentKey ?? string.Empty))
                {
                    errors.Add(prefix + "PatientEnvironmentKey is invalid.");
                }
            }

            return new ConfigurationValidationResult(errors);
        }

        private static string ResolvedTarget(ApplicationDefinition application)
        {
            return string.IsNullOrWhiteSpace(application.ResolvedExecutable)
                ? Environment.ExpandEnvironmentVariables(application.Executable ?? string.Empty)
                : application.ResolvedExecutable;
        }
    }
}

