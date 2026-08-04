using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using EsapiRunnerHub.Configuration;
using EsapiRunnerHub.Context;
using EsapiRunnerHub.History;
using EsapiRunnerHub.Patients;
using EsapiRunnerHub.Privacy;

namespace EsapiRunnerHub.Launching
{
    public static class ContextCommandLineRunner
    {
        public const string RunContextOption = "--run-context";
        public const string ReplayLatestOption = "--replay-latest";
        public const string PatientEnvironmentKey = "ESAPI_RUNNER_CONTEXT_PATIENT";
        public const string CourseEnvironmentKey = "ESAPI_RUNNER_CONTEXT_COURSE";
        public const string PlanEnvironmentKey = "ESAPI_RUNNER_CONTEXT_PLAN";
        public const string PlanSumEnvironmentKey = "ESAPI_RUNNER_CONTEXT_PLAN_SUM";
        public const string StructureSetEnvironmentKey = "ESAPI_RUNNER_CONTEXT_STRUCTURE_SET";
        public const string ImageEnvironmentKey = "ESAPI_RUNNER_CONTEXT_IMAGE";
        public const string PlanScopeEnvironmentKey = "ESAPI_RUNNER_CONTEXT_PLAN_SCOPE";
        public const string PlanSumScopeEnvironmentKey = "ESAPI_RUNNER_CONTEXT_PLAN_SUM_SCOPE";

        public static bool IsContextCommand(string[] arguments)
        {
            return (arguments ?? Array.Empty<string>()).Any(value =>
                string.Equals(value, RunContextOption, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, ReplayLatestOption, StringComparison.OrdinalIgnoreCase));
        }

        public static int Run(string[] arguments)
        {
            var appId = SafeApplicationId(ValueAfter(arguments, RunContextOption) ?? ValueAfter(arguments, ReplayLatestOption));
            try
            {
                var settingsPath = ResolveSettingsPath(arguments);
                if (!File.Exists(settingsPath)) throw new FileNotFoundException("The settings file was not found.", settingsPath);
                var configuration = IniConfigurationStore.Load(settingsPath);
                TechnicalLog.Configure(configuration.Hub.ResolvedLogDirectory);
                var application = configuration.Applications.SingleOrDefault(item => item.Enabled &&
                    string.Equals(item.Id, appId, StringComparison.OrdinalIgnoreCase));
                if (application == null) throw new InvalidOperationException("The requested application is unavailable.");
                if (application.LaunchKind != LaunchKind.EsapiContextScript && application.LaunchKind != LaunchKind.EclipsePlugin)
                    throw new InvalidOperationException("The requested application does not support an ESAPI context.");

                ContextSelection selection;
                if (HasOption(arguments, ReplayLatestOption))
                {
                    selection = ReadLatestContext(configuration, application.Id);
                }
                else
                {
                    selection = ReadEnvironmentContext(application.ScopeMode);
                }

                var patient = string.IsNullOrWhiteSpace(selection.PatientId)
                    ? null
                    : new PatientRecord(selection.PatientId, string.Empty, string.Empty, 0);
                var request = ContextScriptRequestComposer.Compose(application, patient, selection,
                    configuration.Hub, configuration.Hub.ResolvedScriptHostExecutable);
                var process = new ChildProcessLauncher().Start(request);
                process.WaitForExit(Timeout.Infinite);
                var exitCode = process.ExitCode ?? 1;
                TechnicalLog.Current.WriteImmediate(exitCode == 0 ? "INFO" : "WARN",
                    exitCode == 0 ? "cli_context_child_exited" : "cli_context_child_exit_nonzero", application.Id, null);
                return exitCode;
            }
            catch (Exception exception)
            {
                TechnicalLog.Current.WriteImmediate("ERROR", "cli_context_failed", appId, exception);
                return 2;
            }
        }

        private static ContextSelection ReadLatestContext(HubConfiguration configuration, string applicationId)
        {
            var entry = new LaunchHistoryStore(configuration.Hub.ResolvedHistoryFile,
                    configuration.Hub.HistoryRetentionDays, configuration.Hub.HistoryMaxEntries)
                .Load()
                .Where(item => item.LaunchMode == LaunchMode.Context &&
                               string.Equals(item.ApplicationId, applicationId, StringComparison.OrdinalIgnoreCase) &&
                               !string.IsNullOrWhiteSpace(item.ProtectedContext))
                .OrderByDescending(item => item.StartedUtc)
                .FirstOrDefault();
            if (entry == null) throw new InvalidOperationException("No protected context is available for this application.");
            return new ProtectedContextEnvelope().Unprotect(entry.ProtectedContext);
        }

        private static ContextSelection ReadEnvironmentContext(ScopeMode scopeMode)
        {
            var selection = new ContextSelection
            {
                PatientId = Environment.GetEnvironmentVariable(PatientEnvironmentKey),
                CourseId = Environment.GetEnvironmentVariable(CourseEnvironmentKey),
                PlanId = Environment.GetEnvironmentVariable(PlanEnvironmentKey),
                PlanSumId = Environment.GetEnvironmentVariable(PlanSumEnvironmentKey),
                StructureSetId = Environment.GetEnvironmentVariable(StructureSetEnvironmentKey),
                ImageId = Environment.GetEnvironmentVariable(ImageEnvironmentKey)
            };
            AddScope(selection.PlanIdsInScope, Environment.GetEnvironmentVariable(PlanScopeEnvironmentKey));
            AddScope(selection.PlanSumIdsInScope, Environment.GetEnvironmentVariable(PlanSumScopeEnvironmentKey));
            if (scopeMode != ScopeMode.None && selection.PlanIdsInScope.Count == 0 && !string.IsNullOrWhiteSpace(selection.PlanId))
                selection.PlanIdsInScope.Add(selection.PlanId);
            if (scopeMode != ScopeMode.None && selection.PlanSumIdsInScope.Count == 0 && !string.IsNullOrWhiteSpace(selection.PlanSumId))
                selection.PlanSumIdsInScope.Add(selection.PlanSumId);
            return selection;
        }

        private static void AddScope(IList<string> target, string value)
        {
            foreach (var id in (value ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = id.Trim();
                if (trimmed.Length > 0) target.Add(trimmed);
            }
        }

        private static string ResolveSettingsPath(string[] arguments)
        {
            var configured = ValueAfter(arguments, "--settings");
            return string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini")
                : Path.GetFullPath(configured);
        }

        private static string ValueAfter(string[] arguments, string option)
        {
            var values = arguments ?? Array.Empty<string>();
            var index = Array.FindIndex(values, value => string.Equals(value, option, StringComparison.OrdinalIgnoreCase));
            return index >= 0 && index + 1 < values.Length ? values[index + 1] : null;
        }

        private static bool HasOption(string[] arguments, string option)
        {
            return (arguments ?? Array.Empty<string>()).Any(value => string.Equals(value, option, StringComparison.OrdinalIgnoreCase));
        }

        private static string SafeApplicationId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.Ordinal))
                return string.Empty;
            return value;
        }
    }
}
