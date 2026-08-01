using System;
using System.IO;
using System.Linq;
using EsapiRunnerHub.Configuration;

namespace EsapiRunnerHub.Tests
{
    internal static class ConfigurationTests
    {
        public static void Register()
        {
            TestHarness.Test("Eclipse plug-in kind parses and round trips", ParsesEclipsePluginKind);
            TestHarness.Test("Eclipse plug-ins cannot declare external patient transfer", RejectsPluginPatientTransfer);
            TestHarness.Test("launch kind must match the configured target extension", RejectsMismatchedLaunchKind);
            TestHarness.Test("launch kind validation supports whole-path environment variables", SupportsExpandedWholePath);
            TestHarness.Test("INI parser resolves hub and application values", () =>
            {
                var source = @"
[Hub]
EsapiApiAssembly=vendor\VMS.TPS.Common.Model.API.dll
EsapiTypesAssembly=vendor\VMS.TPS.Common.Model.Types.dll
SearchMaxResults=12
SearchDebounceMs=175
PathProbeTimeoutMs=1400

[Application.clearplan]
Name=ClearPlan
Category=Plan review
Description=Review
Executable=apps\ClearPlan.exe
WorkingDirectory=apps
PatientMode=Optional
PatientTransport=None
Arguments=--safe
Enabled=true
SortOrder=10
";
                var settingsPath = Path.Combine(Path.GetTempPath(), "runnerhub-config-tests", "settings.ini");
                var configuration = IniConfigurationStore.ParseText(source, settingsPath);
                var app = configuration.Applications.Single();

                TestHarness.AssertEqual(12, configuration.Hub.SearchMaxResults);
                TestHarness.AssertEqual(175, configuration.Hub.SearchDebounceMs);
                TestHarness.AssertEqual(PatientMode.Optional, app.PatientMode);
                TestHarness.AssertEqual(PatientTransport.None, app.PatientTransport);
                TestHarness.AssertEqual(
                    Path.GetFullPath(Path.Combine(Path.GetDirectoryName(settingsPath), "apps", "ClearPlan.exe")),
                    app.ResolvedExecutable);
                TestHarness.AssertTrue(configuration.Validate().IsValid);
            });

            TestHarness.Test("required patient application needs a transport", () =>
            {
                var source = @"
[Application.edoc]
Name=eDoc
Executable=eDoc.exe
PatientMode=Required
PatientTransport=None
Enabled=true
";
                var configuration = IniConfigurationStore.ParseText(source, @"C:\portable\settings.ini");
                var validation = configuration.Validate();
                TestHarness.AssertFalse(validation.IsValid);
                TestHarness.AssertTrue(validation.Errors.Any(error => error.Contains("PatientTransport")));
            });

            TestHarness.Test("argument transport requires patient placeholder", () =>
            {
                var source = @"
[Application.tool]
Name=Tool
Executable=tool.exe
PatientMode=Optional
PatientTransport=Argument
PatientArgumentTemplate=--patient-id
";
                var validation = IniConfigurationStore.ParseText(source, @"C:\portable\settings.ini").Validate();
                TestHarness.AssertFalse(validation.IsValid);
                TestHarness.AssertTrue(validation.Errors.Any(error => error.Contains("{PatientId}")));
            });

            TestHarness.Test("configuration saves atomically and round trips", () =>
            {
                var directory = Path.Combine(Path.GetTempPath(), "runnerhub-config-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                try
                {
                    var path = Path.Combine(directory, "settings.ini");
                    var configuration = IniConfigurationStore.ParseText(@"
[Hub]
SearchMaxResults=8
[Application.tool]
Name=Tool
Executable=tool.exe
PatientMode=None
PatientTransport=None
Enabled=true
", path);
                    IniConfigurationStore.SaveAtomic(configuration, path);
                    var reloaded = IniConfigurationStore.Load(path);
                    TestHarness.AssertEqual(8, reloaded.Hub.SearchMaxResults);
                    TestHarness.AssertEqual("Tool", reloaded.Applications.Single().Name);
                    TestHarness.AssertFalse(File.Exists(path + ".tmp"));
                }
                finally
                {
                    Directory.Delete(directory, true);
                }
            });
        }

        private static void ParsesEclipsePluginKind()
        {
            var configuration = IniConfigurationStore.ParseText(@"
[Application.plugin]
Name=Plan FieldNamer
Category=Eclipse plug-ins
Description=Runs inside Eclipse.
Executable=plugins\Plan_FieldNamer.esapi.dll
LaunchKind=EclipsePlugin
PatientMode=None
PatientTransport=None
Enabled=true
", @"C:\portable\settings.ini");
            var application = configuration.Applications.Single();
            var launchKind = application.GetType().GetProperty("LaunchKind");

            TestHarness.AssertTrue(launchKind != null, "ApplicationDefinition.LaunchKind is missing.");
            TestHarness.AssertEqual("EclipsePlugin", launchKind.GetValue(application, null).ToString());
            TestHarness.AssertContains(IniConfigurationStore.Serialize(configuration), "LaunchKind=EclipsePlugin");
            TestHarness.AssertTrue(configuration.Validate().IsValid);
        }

        private static void RejectsPluginPatientTransfer()
        {
            var configuration = IniConfigurationStore.ParseText(@"
[Application.plugin]
Name=Plug-in
Executable=plugins\Plugin.esapi.dll
LaunchKind=EclipsePlugin
PatientMode=Optional
PatientTransport=Argument
PatientArgumentTemplate=--patient {PatientId}
", @"C:\portable\settings.ini");

            var validation = configuration.Validate();
            TestHarness.AssertFalse(validation.IsValid);
            TestHarness.AssertTrue(validation.Errors.Any(error => error.Contains("EclipsePlugin")));
        }

        private static void RejectsMismatchedLaunchKind()
        {
            var executableAsPlugin = IniConfigurationStore.ParseText(@"
[Application.tool]
Name=Tool
Executable=tool.exe
LaunchKind=EclipsePlugin
PatientMode=None
PatientTransport=None
", @"C:\portable\settings.ini").Validate();

            var pluginAsExecutable = IniConfigurationStore.ParseText(@"
[Application.plugin]
Name=Plug-in
Executable=Plugin.esapi.dll
LaunchKind=Executable
PatientMode=None
PatientTransport=None
", @"C:\portable\settings.ini").Validate();

            TestHarness.AssertFalse(executableAsPlugin.IsValid);
            TestHarness.AssertFalse(pluginAsExecutable.IsValid);
            TestHarness.AssertTrue(executableAsPlugin.Errors.Any(error => error.Contains(".cs") && error.Contains(".esapi.dll")));
            TestHarness.AssertTrue(pluginAsExecutable.Errors.Any(error => error.Contains(".exe")));
        }

        private static void SupportsExpandedWholePath()
        {
            const string executableVariable = "ESAPI_RUNNER_HUB_TEST_EXE";
            const string pluginVariable = "ESAPI_RUNNER_HUB_TEST_PLUGIN";
            var previousExecutable = Environment.GetEnvironmentVariable(executableVariable);
            var previousPlugin = Environment.GetEnvironmentVariable(pluginVariable);
            try
            {
                Environment.SetEnvironmentVariable(executableVariable, TestHarness.PathFromRoot("tests/RunnerFixture/bin/x64/Release/RunnerFixture.exe"));
                Environment.SetEnvironmentVariable(pluginVariable, @"C:\portable\Plugin.esapi.dll");
                var configuration = IniConfigurationStore.ParseText(@"
[Application.tool]
Name=Tool
Executable=%ESAPI_RUNNER_HUB_TEST_EXE%
LaunchKind=Executable
PatientMode=None
PatientTransport=None

[Application.plugin]
Name=Plug-in
Executable=%ESAPI_RUNNER_HUB_TEST_PLUGIN%
LaunchKind=EclipsePlugin
PatientMode=None
PatientTransport=None
", @"C:\portable\settings.ini");

                TestHarness.AssertTrue(configuration.Validate().IsValid);
            }
            finally
            {
                Environment.SetEnvironmentVariable(executableVariable, previousExecutable);
                Environment.SetEnvironmentVariable(pluginVariable, previousPlugin);
            }
        }
    }
}
