using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
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
        public const string RunContextsOption = "--run-contexts";
        public const string ReplayLatestOption = "--replay-latest";
        public const string RunRequestOption = "--run-request";
        public const string ContextsEnvironmentKey = "ESAPI_RUNNER_CONTEXTS";
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
                string.Equals(value, RunContextsOption, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, ReplayLatestOption, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, RunRequestOption, StringComparison.OrdinalIgnoreCase));
        }

        public static bool TryRunPending(string[] arguments, out int exitCode)
        {
            exitCode = 0;
            string claimPath = null;
            try
            {
                var settingsPath = ResolveSettingsPath(arguments);
                if (!File.Exists(settingsPath)) return false;
                var configuration = IniConfigurationStore.Load(settingsPath);
                var directory = configuration.Hub.ResolvedContextRequestDirectory;
                if (string.IsNullOrWhiteSpace(directory)) return false;
                var userSid = CurrentUserSid();
                if (string.IsNullOrWhiteSpace(userSid)) return false;
                var pendingPath = Path.Combine(directory, userSid + ".pending");
                if (!File.Exists(pendingPath)) return false;

                claimPath = Path.Combine(directory, userSid + ".claimed-" + Guid.NewGuid().ToString("N"));
                try
                {
                    File.Move(pendingPath, claimPath);
                }
                catch (IOException)
                {
                    return false;
                }

                var requestId = File.ReadAllText(claimPath).Trim();
                ValidateRequestId(requestId);
                exitCode = Run(new[] { RunRequestOption, requestId, "--settings", settingsPath });
                return true;
            }
            catch (Exception exception)
            {
                TechnicalLog.Current.WriteImmediate("ERROR", "pending_context_failed", string.Empty, exception);
                exitCode = 2;
                return true;
            }
            finally
            {
                if (claimPath != null && File.Exists(claimPath))
                {
                    try { File.Delete(claimPath); }
                    catch { }
                }
            }
        }

        public static int Run(string[] arguments)
        {
            var startedUtc = DateTime.UtcNow;
            var requestId = ValueAfter(arguments, RunRequestOption);
            var appId = SafeApplicationId(ValueAfter(arguments, RunContextOption) ??
                                          ValueAfter(arguments, RunContextsOption) ??
                                          ValueAfter(arguments, ReplayLatestOption));
            HubConfiguration configuration = null;
            try
            {
                var settingsPath = ResolveSettingsPath(arguments);
                if (!File.Exists(settingsPath)) throw new FileNotFoundException("The settings file was not found.", settingsPath);
                configuration = IniConfigurationStore.Load(settingsPath);
                TechnicalLog.Configure(configuration.Hub.ResolvedLogDirectory);
                ContextRunRequest sharedRequest = null;
                if (HasOption(arguments, RunRequestOption))
                {
                    sharedRequest = ReadContextRequest(configuration, requestId);
                    appId = SafeApplicationId(sharedRequest.ApplicationId);
                }
                var application = configuration.Applications.SingleOrDefault(item => item.Enabled &&
                    string.Equals(item.Id, appId, StringComparison.OrdinalIgnoreCase));
                if (application == null) throw new InvalidOperationException("The requested application is unavailable.");
                if (application.LaunchKind != LaunchKind.EsapiContextScript && application.LaunchKind != LaunchKind.EclipsePlugin)
                    throw new InvalidOperationException("The requested application does not support an ESAPI context.");
                if (HasOption(arguments, RunContextsOption) && application.WriteMode != WriteMode.ReadOnly)
                    throw new InvalidOperationException("Context series are restricted to read-only applications.");

                IList<ContextSelection> selections;
                if (sharedRequest != null)
                {
                    if (sharedRequest.Contexts == null || sharedRequest.Contexts.Count == 0)
                        throw new InvalidOperationException("The context request is empty.");
                    if (sharedRequest.Contexts.Count > 100)
                        throw new InvalidOperationException("The context request exceeds the maximum of 100 entries.");
                    if (sharedRequest.Contexts.Count > 1 && application.WriteMode != WriteMode.ReadOnly)
                        throw new InvalidOperationException("Context series are restricted to read-only applications.");
                    selections = sharedRequest.Contexts.Select(item => item.ToSelection(application.ScopeMode)).ToList();
                }
                else if (HasOption(arguments, ReplayLatestOption))
                {
                    selections = new[] { ReadLatestContext(configuration, application.Id) };
                }
                else if (HasOption(arguments, RunContextsOption))
                {
                    selections = ReadEnvironmentContexts(application.ScopeMode);
                }
                else
                {
                    selections = new[] { ReadEnvironmentContext(application.ScopeMode) };
                }

                foreach (var selection in selections)
                {
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
                    if (exitCode != 0)
                    {
                        WriteContextRequestResult(configuration, requestId, application.Id, exitCode, startedUtc, null);
                        return exitCode;
                    }
                }

                WriteContextRequestResult(configuration, requestId, application.Id, 0, startedUtc, null);
                return 0;
            }
            catch (ContextRequestOwnerMismatchException exception)
            {
                TechnicalLog.Current.WriteImmediate("WARN", "context_request_owner_mismatch", string.Empty, exception);
                return 2;
            }
            catch (Exception exception)
            {
                TechnicalLog.Current.WriteImmediate("ERROR", "cli_context_failed", appId, exception);
                WriteContextRequestResult(configuration, requestId, appId, 2, startedUtc, exception);
                return 2;
            }
        }

        private static ContextRunRequest ReadContextRequest(HubConfiguration configuration, string requestId)
        {
            ValidateRequestId(requestId);
            var directory = configuration.Hub.ResolvedContextRequestDirectory;
            if (string.IsNullOrWhiteSpace(directory))
                throw new InvalidOperationException("The context request directory is not configured.");
            var resultPath = Path.Combine(directory, requestId + ".result.json");
            if (File.Exists(resultPath))
                throw new InvalidOperationException("The context request was already processed.");
            var requestPath = Path.Combine(directory, requestId + ".request.json");
            if (!File.Exists(requestPath))
                throw new FileNotFoundException("The context request file was not found.", requestPath);
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(ContextRunRequest));
                string json;
                using (var reader = new StreamReader(requestPath, Encoding.UTF8, true))
                {
                    json = reader.ReadToEnd();
                }
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    var request = serializer.ReadObject(stream) as ContextRunRequest;
                    if (request == null) throw new SerializationException("No context request payload was found.");
                    if (string.IsNullOrWhiteSpace(request.RequestedBySid) ||
                        !string.Equals(request.RequestedBySid, CurrentUserSid(), StringComparison.OrdinalIgnoreCase))
                        throw new ContextRequestOwnerMismatchException();
                    return request;
                }
            }
            catch (SerializationException exception)
            {
                throw new InvalidOperationException("The context request is invalid.", exception);
            }
        }

        private static void WriteContextRequestResult(HubConfiguration configuration, string requestId,
            string applicationId, int exitCode, DateTime startedUtc, Exception exception)
        {
            if (configuration == null || string.IsNullOrWhiteSpace(requestId)) return;
            try
            {
                ValidateRequestId(requestId);
                var directory = configuration.Hub.ResolvedContextRequestDirectory;
                if (string.IsNullOrWhiteSpace(directory)) return;
                Directory.CreateDirectory(directory);
                var result = new ContextRunResult
                {
                    RequestId = requestId,
                    ApplicationId = applicationId,
                    ComputerName = Environment.MachineName,
                    ExitCode = exitCode,
                    Status = exitCode == 0 ? "completed" : "failed",
                    StartedUtc = startedUtc,
                    FinishedUtc = DateTime.UtcNow,
                    ExceptionType = exception == null ? null : exception.GetType().Name
                };
                var resultPath = Path.Combine(directory, requestId + ".result.json");
                if (File.Exists(resultPath)) return;
                var temporaryPath = resultPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    var serializer = new DataContractJsonSerializer(typeof(ContextRunResult));
                    using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        serializer.WriteObject(stream, result);
                        stream.Flush(true);
                    }
                    if (File.Exists(resultPath)) return;
                    File.Move(temporaryPath, resultPath);
                    temporaryPath = null;
                }
                finally
                {
                    if (temporaryPath != null && File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }
            }
            catch (Exception resultException)
            {
                TechnicalLog.Current.WriteImmediate("WARN", "context_request_result_failed", applicationId, resultException);
            }
        }

        private static void ValidateRequestId(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId) ||
                !Regex.IsMatch(requestId, "^[A-Za-z0-9][A-Za-z0-9._-]{0,79}$", RegexOptions.CultureInvariant))
                throw new InvalidOperationException("The context request id is invalid.");
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

        private static IList<ContextSelection> ReadEnvironmentContexts(ScopeMode scopeMode)
        {
            var json = Environment.GetEnvironmentVariable(ContextsEnvironmentKey);
            Environment.SetEnvironmentVariable(ContextsEnvironmentKey, null);
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("The context series is missing.");

            ContextSeriesEnvelope envelope;
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(ContextSeriesEnvelope));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    envelope = serializer.ReadObject(stream) as ContextSeriesEnvelope;
                }
            }
            catch (SerializationException exception)
            {
                throw new InvalidOperationException("The context series is invalid.", exception);
            }

            if (envelope == null || envelope.Contexts == null || envelope.Contexts.Count == 0)
                throw new InvalidOperationException("The context series is empty.");
            if (envelope.Contexts.Count > 100)
                throw new InvalidOperationException("The context series exceeds the maximum of 100 entries.");

            return envelope.Contexts.Select(item => item.ToSelection(scopeMode)).ToList();
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

        private static string CurrentUserSid()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                return identity.User == null ? string.Empty : identity.User.Value;
            }
        }

        [DataContract]
        private sealed class ContextSeriesEnvelope
        {
            [DataMember] public List<ContextSeriesItem> Contexts { get; set; }
        }

        [DataContract]
        private sealed class ContextRunRequest
        {
            [DataMember(Order = 1)] public string RequestedBySid { get; set; }
            [DataMember(Order = 2)] public string RequestedBy { get; set; }
            [DataMember(Order = 3)] public string ApplicationId { get; set; }
            [DataMember(Order = 4)] public List<ContextSeriesItem> Contexts { get; set; }
        }

        private sealed class ContextRequestOwnerMismatchException : InvalidOperationException
        {
            public ContextRequestOwnerMismatchException()
                : base("The context request belongs to another Windows identity.")
            {
            }
        }

        [DataContract]
        private sealed class ContextRunResult
        {
            [DataMember(Order = 1)] public string RequestId { get; set; }
            [DataMember(Order = 2)] public string ApplicationId { get; set; }
            [DataMember(Order = 3)] public string ComputerName { get; set; }
            [DataMember(Order = 4)] public int ExitCode { get; set; }
            [DataMember(Order = 5)] public string Status { get; set; }
            [DataMember(Order = 6)] public DateTime StartedUtc { get; set; }
            [DataMember(Order = 7)] public DateTime FinishedUtc { get; set; }
            [DataMember(Order = 8, EmitDefaultValue = false)] public string ExceptionType { get; set; }
        }

        [DataContract]
        private sealed class ContextSeriesItem
        {
            [DataMember] public string PatientId { get; set; }
            [DataMember] public string CourseId { get; set; }
            [DataMember] public string PlanId { get; set; }
            [DataMember] public string PlanSumId { get; set; }
            [DataMember] public string StructureSetId { get; set; }
            [DataMember] public string ImageId { get; set; }
            [DataMember] public List<string> PlanIdsInScope { get; set; }
            [DataMember] public List<string> PlanSumIdsInScope { get; set; }

            public ContextSelection ToSelection(ScopeMode scopeMode)
            {
                var selection = new ContextSelection
                {
                    PatientId = PatientId,
                    CourseId = CourseId,
                    PlanId = PlanId,
                    PlanSumId = PlanSumId,
                    StructureSetId = StructureSetId,
                    ImageId = ImageId
                };
                foreach (var id in PlanIdsInScope ?? new List<string>()) AddScope(selection.PlanIdsInScope, id);
                foreach (var id in PlanSumIdsInScope ?? new List<string>()) AddScope(selection.PlanSumIdsInScope, id);
                if (scopeMode != ScopeMode.None && selection.PlanIdsInScope.Count == 0 && !string.IsNullOrWhiteSpace(selection.PlanId))
                    selection.PlanIdsInScope.Add(selection.PlanId);
                if (scopeMode != ScopeMode.None && selection.PlanSumIdsInScope.Count == 0 && !string.IsNullOrWhiteSpace(selection.PlanSumId))
                    selection.PlanSumIdsInScope.Add(selection.PlanSumId);
                return selection;
            }
        }
    }
}
