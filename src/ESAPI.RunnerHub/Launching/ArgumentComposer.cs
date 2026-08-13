using System;
using System.Text.RegularExpressions;
using EsapiRunnerHub.Configuration;
using EsapiRunnerHub.Patients;

namespace EsapiRunnerHub.Launching
{
    public static class ArgumentComposer
    {
        private static readonly Regex SafePatientId = new Regex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant);

        public static LaunchRequest Compose(ApplicationDefinition application, PatientRecord patient, bool usePatient)
        {
            if (application == null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (application.PatientMode == PatientMode.Required && !usePatient)
            {
                throw new InvalidOperationException("This application requires a selected patient.");
            }

            if (application.PatientMode == PatientMode.None)
            {
                usePatient = false;
            }

            if (usePatient && (patient == null || !SafePatientId.IsMatch(patient.Id ?? string.Empty)))
            {
                throw new InvalidOperationException("The selected patient ID cannot be transferred safely.");
            }

            var executable = string.IsNullOrWhiteSpace(application.ResolvedExecutable)
                ? application.Executable
                : application.ResolvedExecutable;
            var workingDirectory = string.IsNullOrWhiteSpace(application.ResolvedWorkingDirectory)
                ? System.IO.Path.GetDirectoryName(executable ?? string.Empty)
                : application.ResolvedWorkingDirectory;
            var request = new LaunchRequest(application.Id, executable, workingDirectory, application.Arguments);
            if (!usePatient)
            {
                return request;
            }

            switch (application.PatientTransport)
            {
                case PatientTransport.Argument:
                    var patientArguments = (application.PatientArgumentTemplate ?? string.Empty)
                        .Replace("{PatientId}", patient.Id);
                    request.Arguments = JoinArguments(request.Arguments, patientArguments);
                    break;
                case PatientTransport.Environment:
                    request.EnvironmentVariables[application.PatientEnvironmentKey] = patient.Id;
                    break;
                default:
                    if (application.PatientMode == PatientMode.Required)
                    {
                        throw new InvalidOperationException("No patient transport is configured.");
                    }
                    break;
            }

            request.LogSummary = "start app=" + request.ApplicationId + " patient=yes transport=" + application.PatientTransport.ToString().ToLowerInvariant();
            return request;
        }

        private static string JoinArguments(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first))
            {
                return (second ?? string.Empty).Trim();
            }

            if (string.IsNullOrWhiteSpace(second))
            {
                return first.Trim();
            }

            return first.Trim() + " " + second.Trim();
        }
    }
}
