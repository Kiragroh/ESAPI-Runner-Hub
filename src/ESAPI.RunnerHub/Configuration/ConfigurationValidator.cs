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

                if (application.PatientMode == PatientMode.Required && application.PatientTransport == PatientTransport.None)
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
    }
}

