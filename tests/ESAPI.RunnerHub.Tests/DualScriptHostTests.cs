using System;
using System.IO;
using System.Linq;
using System.Reflection;
using EsapiRunnerHub.Configuration;
using EsapiRunnerHub.Context;
using EsapiRunnerHub.Launching;
using EsapiRunnerHub.Patients;
using EsapiRunnerHub.ViewModels;
using HostMode = EsapiScriptHost.Contracts.WriteMode;
using RunnerMode = EsapiRunnerHub.Configuration.WriteMode;

namespace EsapiRunnerHub.Tests
{
    internal static class DualScriptHostTests
    {
        public static void Register()
        {
            TestHarness.Test("dual script host paths parse resolve and round trip", PathsRoundTrip);
            TestHarness.Test("settings editor exposes both script host paths and discard mode", SettingsExposeBothHosts);
            TestHarness.Test("write host is required only for enabled direct write applications", WriteHostValidationIsConditional);
            TestHarness.Test("write mode alone selects the script host executable", WriteModeSelectsHost);
            TestHarness.Test("host capability rejects a mismatched write mode", HostCapabilityFailsClosed);
            TestHarness.Test("execute and discard can never save", ExecuteAndDiscardNeverSaves);
            TestHarness.Test("only the write host carries the ESAPI write marker", OnlyWriteHostCarriesMarker);
            TestHarness.Test("example settings declare both script host paths", ExampleSettingsDeclareBothHosts);
        }

        private static void PathsRoundTrip()
        {
            var directory = Path.Combine(Path.GetTempPath(), "runner-dual-host-" + Guid.NewGuid().ToString("N"));
            var settingsPath = Path.Combine(directory, "settings.ini");
            var configuration = IniConfigurationStore.ParseText(
                "[Hub]\nScriptHostExecutable=read.exe\nWriteScriptHostExecutable=write.exe\n", settingsPath);

            TestHarness.AssertEqual("read.exe", configuration.Hub.ScriptHostExecutable);
            TestHarness.AssertEqual("write.exe", configuration.Hub.WriteScriptHostExecutable);
            TestHarness.AssertEqual(Path.Combine(directory, "read.exe"), configuration.Hub.ResolvedScriptHostExecutable);
            TestHarness.AssertEqual(Path.Combine(directory, "write.exe"), configuration.Hub.ResolvedWriteScriptHostExecutable);
            TestHarness.AssertContains(IniConfigurationStore.Serialize(configuration), "WriteScriptHostExecutable=write.exe");
        }

        private static void SettingsExposeBothHosts()
        {
            var configuration = IniConfigurationStore.ParseText("[Hub]\n", Path.Combine(Path.GetTempPath(), "dual-settings.ini"));
            var viewModel = new SettingsViewModel(configuration, configuration.SourcePath);

            TestHarness.AssertEqual("ESAPI-Write-Script-Host.exe", viewModel.WriteScriptHostExecutable);
            TestHarness.AssertTrue(viewModel.WriteModes.Any(mode => mode == RunnerMode.ExecuteAndDiscard));
            viewModel.AssignWriteScriptHostExecutable(@"C:\Tools\WriteHost.exe");
            TestHarness.AssertEqual(@"C:\Tools\WriteHost.exe", viewModel.WorkingConfiguration.Hub.WriteScriptHostExecutable);
        }

        private static void WriteHostValidationIsConditional()
        {
            var readOnly = ParseApplication(RunnerMode.ReadOnly, string.Empty, true);
            TestHarness.AssertFalse(readOnly.Validate().Errors.Any(error => error.Contains("WriteScriptHostExecutable")));

            var disabledWrite = ParseApplication(RunnerMode.ConfirmSave, string.Empty, false);
            TestHarness.AssertFalse(disabledWrite.Validate().Errors.Any(error => error.Contains("WriteScriptHostExecutable")));

            var enabledWrite = ParseApplication(RunnerMode.ExecuteAndDiscard, string.Empty, true);
            TestHarness.AssertTrue(enabledWrite.Validate().Errors.Any(error => error.Contains("WriteScriptHostExecutable")));
        }

        private static void WriteModeSelectsHost()
        {
            var directory = Path.Combine(Path.GetTempPath(), "runner-host-select");
            var hub = IniConfigurationStore.ParseText(
                "[Hub]\nScriptHostExecutable=read.exe\nWriteScriptHostExecutable=write.exe\n",
                Path.Combine(directory, "settings.ini")).Hub;
            var selection = new ContextSelection();

            TestHarness.AssertEqual(Path.Combine(directory, "read.exe"), Compose(RunnerMode.ReadOnly, hub, selection).ExecutablePath);
            TestHarness.AssertEqual(Path.Combine(directory, "write.exe"), Compose(RunnerMode.ConfirmSave, hub, selection).ExecutablePath);
            TestHarness.AssertEqual(Path.Combine(directory, "write.exe"), Compose(RunnerMode.ExecuteAndDiscard, hub, selection).ExecutablePath);
        }

        private static void HostCapabilityFailsClosed()
        {
            EsapiScriptHost.Host.ScriptHostCapability.Validate(
                EsapiScriptHost.Host.ScriptHostKind.ReadOnly, HostMode.ReadOnly);
            EsapiScriptHost.Host.ScriptHostCapability.Validate(
                EsapiScriptHost.Host.ScriptHostKind.WriteEnabled, HostMode.ConfirmSave);
            EsapiScriptHost.Host.ScriptHostCapability.Validate(
                EsapiScriptHost.Host.ScriptHostKind.WriteEnabled, HostMode.ExecuteAndDiscard);

            TestHarness.AssertThrows<InvalidOperationException>(() =>
                EsapiScriptHost.Host.ScriptHostCapability.Validate(
                    EsapiScriptHost.Host.ScriptHostKind.ReadOnly, HostMode.ConfirmSave));
            TestHarness.AssertThrows<InvalidOperationException>(() =>
                EsapiScriptHost.Host.ScriptHostCapability.Validate(
                    EsapiScriptHost.Host.ScriptHostKind.ReadOnly, HostMode.ExecuteAndDiscard));
            TestHarness.AssertThrows<InvalidOperationException>(() =>
                EsapiScriptHost.Host.ScriptHostCapability.Validate(
                    EsapiScriptHost.Host.ScriptHostKind.WriteEnabled, HostMode.ReadOnly));
        }

        private static void ExecuteAndDiscardNeverSaves()
        {
            TestHarness.AssertEqual(EsapiScriptHost.Host.SaveAction.CloseWithoutSave,
                EsapiScriptHost.Host.SaveDecision.Decide(true, HostMode.ExecuteAndDiscard,
                    EsapiScriptHost.Host.SaveChoice.Save));
        }

        private static void OnlyWriteHostCarriesMarker()
        {
            var configurationName = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Name;
            var readHost = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ESAPI-Script-Host.exe");
            var writeHost = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ESAPI-Write-Script-Host.exe");
            if (!File.Exists(readHost))
                readHost = TestHarness.PathFromRoot("src/ESAPI.ScriptHost/bin/x64/" + configurationName + "/ESAPI-Script-Host.exe");
            if (!File.Exists(writeHost))
                writeHost = TestHarness.PathFromRoot("src/ESAPI.WriteScriptHost/bin/x64/" + configurationName + "/ESAPI-Write-Script-Host.exe");
            var repository = Path.GetDirectoryName(TestHarness.PathFromRoot("ESAPI-Runner-Hub.sln"));
            var api = FindApiReference(repository);
            ResolveEventHandler resolver = (sender, arguments) =>
                new AssemblyName(arguments.Name).Name == "VMS.TPS.Common.Model.API"
                    ? Assembly.ReflectionOnlyLoadFrom(api)
                    : null;
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += resolver;
            try
            {
                TestHarness.AssertFalse(IsWriteEnabled(Assembly.ReflectionOnlyLoadFrom(readHost)));
                TestHarness.AssertTrue(IsWriteEnabled(Assembly.ReflectionOnlyLoadFrom(writeHost)));
            }
            finally
            {
                AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve -= resolver;
            }
        }

        private static string FindApiReference(string startDirectory)
        {
            var current = new DirectoryInfo(startDirectory);
            while (current != null)
            {
                var candidate = Path.Combine(current.FullName, "_Assets", "VMS.TPS.Common.Model.API.dll");
                if (File.Exists(candidate)) return candidate;
                current = current.Parent;
            }
            throw new FileNotFoundException("Eclipse API metadata was not found for marker inspection.");
        }

        private static bool IsWriteEnabled(Assembly assembly)
        {
            var attribute = assembly.GetCustomAttributesData().FirstOrDefault(item =>
                string.Equals(item.AttributeType.Name, "ESAPIScriptAttribute", StringComparison.Ordinal));
            if (attribute == null) return false;
            var argument = attribute.NamedArguments.FirstOrDefault(item => item.MemberName == "IsWriteable");
            return argument.TypedValue.Value is bool && (bool)argument.TypedValue.Value;
        }

        private static void ExampleSettingsDeclareBothHosts()
        {
            var text = File.ReadAllText(TestHarness.PathFromRoot("settings.example.ini"));
            TestHarness.AssertContains(text, "ScriptHostExecutable=ESAPI-Script-Host.exe");
            TestHarness.AssertContains(text, "WriteScriptHostExecutable=ESAPI-Write-Script-Host.exe");
        }

        private static HubConfiguration ParseApplication(RunnerMode mode, string writeHost, bool enabled)
        {
            return IniConfigurationStore.ParseText(
                "[Hub]\nWriteScriptHostExecutable=" + writeHost +
                "\n[Application.tool]\nName=Tool\nExecutable=Tool.esapi.dll\nLaunchKind=EsapiContextScript" +
                "\nContextRequirement=None\nScopeMode=None\nPatientMode=None\nPatientTransport=None\nWriteMode=" + mode +
                "\nEnabled=" + enabled.ToString().ToLowerInvariant() + "\n",
                Path.Combine(Path.GetTempPath(), "dual-validation.ini"));
        }

        private static LaunchRequest Compose(RunnerMode mode, HubSettings hub, ContextSelection selection)
        {
            var application = new ApplicationDefinition
            {
                Id = "tool",
                Name = "Tool",
                Executable = Path.Combine(Path.GetTempPath(), "Tool.esapi.dll"),
                LaunchKind = LaunchKind.EsapiContextScript,
                ContextRequirement = ContextRequirement.None,
                PatientMode = PatientMode.None,
                PatientTransport = PatientTransport.None,
                WriteMode = mode
            };
            return ContextScriptRequestComposer.Compose(application, (PatientRecord)null, selection, hub);
        }
    }
}
