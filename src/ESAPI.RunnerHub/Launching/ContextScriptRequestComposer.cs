using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EsapiRunnerHub.Configuration;
using EsapiRunnerHub.Context;
using EsapiRunnerHub.Patients;

namespace EsapiRunnerHub.Launching
{
    public static class ContextScriptRequestComposer
    {
        public static LaunchRequest Compose(
            ApplicationDefinition application,
            PatientRecord patient,
            ContextSelection selection,
            HubSettings hub,
            string hostExecutablePath)
        {
            if (application == null) throw new ArgumentNullException(nameof(application));
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            if (hub == null) throw new ArgumentNullException(nameof(hub));
            if (application.LaunchKind != LaunchKind.EsapiContextScript && application.LaunchKind != LaunchKind.EclipsePlugin)
                throw new InvalidOperationException("The selected application is not a context script.");

            var missing = selection.MissingFor(application.ContextRequirement);
            if (!string.IsNullOrWhiteSpace(missing)) throw new InvalidOperationException(missing + ".");
            if (application.ContextRequirement != ContextRequirement.None &&
                (patient == null || !string.Equals(patient.Id, selection.PatientId, StringComparison.Ordinal)))
                throw new InvalidOperationException("The selected patient context is no longer current.");

            var scriptPath = string.IsNullOrWhiteSpace(application.ResolvedExecutable)
                ? Path.GetFullPath(application.Executable)
                : application.ResolvedExecutable;
            var payload = new ContextLaunchPayload
            {
                LaunchToken = Guid.NewGuid().ToString("N"),
                ApplicationId = application.Id,
                ScriptPath = scriptPath,
                ApiAssemblyPath = hub.ResolvedEsapiApiAssembly,
                TypesAssemblyPath = hub.ResolvedEsapiTypesAssembly,
                ScriptEngine = application.ScriptEngine,
                WriteMode = application.WriteMode,
                EntryType = application.EntryType,
                LogDirectory = hub.ResolvedLogDirectory,
                PatientId = selection.PatientId,
                CourseId = selection.CourseId,
                PlanId = selection.PlanId,
                PlanSumId = selection.PlanSumId,
                StructureSetId = selection.StructureSetId,
                ImageId = selection.ImageId,
                PlanIdsInScope = selection.PlanIdsInScope.ToList(),
                PlanSumIdsInScope = selection.PlanSumIdsInScope.ToList(),
                ExtraReferencePaths = ResolveExtraReferences(application).ToList()
            };
            var hostPath = Path.GetFullPath(hostExecutablePath);
            var request = new LaunchRequest(application.Id, hostPath, Path.GetDirectoryName(hostPath), string.Empty);
            request.EnvironmentVariables[ContextLaunchPayload.EnvironmentKey] = payload.Encode();
            request.LogSummary = "start app=" + application.Id + " context=yes transport=environment";
            return request;
        }

        private static IEnumerable<string> ResolveExtraReferences(ApplicationDefinition application)
        {
            var baseDirectory = string.IsNullOrWhiteSpace(application.ResolvedWorkingDirectory)
                ? Path.GetDirectoryName(application.ResolvedExecutable ?? application.Executable ?? string.Empty)
                : application.ResolvedWorkingDirectory;
            return (application.ExtraReferences ?? string.Empty)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => Environment.ExpandEnvironmentVariables(value.Trim()))
                .Where(value => value.Length > 0)
                .Select(value => Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(baseDirectory, value)));
        }
    }
}
