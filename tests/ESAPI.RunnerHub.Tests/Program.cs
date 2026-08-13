namespace EsapiRunnerHub.Tests
{
    internal static class Program
    {
        [System.STAThread]
        private static int Main()
        {
            System.AppDomain.CurrentDomain.AssemblyResolve += (sender, arguments) =>
            {
                if (!string.Equals(new System.Reflection.AssemblyName(arguments.Name).Name,
                    "VMS.TPS.Common.Model.API", System.StringComparison.OrdinalIgnoreCase)) return null;
                var adjacent = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory,
                    "VMS.TPS.Common.Model.API.dll");
                return System.Reflection.Assembly.LoadFrom(System.IO.File.Exists(adjacent)
                    ? adjacent
                    : TestHarness.PathFromRoot("tests/FakeVms.Api/bin/x64/Debug/VMS.TPS.Common.Model.API.dll"));
            };
            ProjectShapeTests.Register();
            DualScriptHostTests.Register();
            ConfigurationTests.Register();
            ContextConfigurationTests.Register();
            CatalogConfigurationTests.Register();
            ApplicationMetadataTests.Register();
            LaunchHistoryTests.Register();
            LaunchHistoryViewModelTests.Register();
            ExampleSettingsTests.Register();
            ContextDirectoryTests.Register();
            ContextSelectionTests.Register();
            ContextLaunchProtocolTests.Register();
            ContextCommandLineTests.Register();
            ScriptHostCoreTests.Register();
            ScriptInvocationTests.Register();
            SourceScriptCompilerTests.Register();
            MainContextViewModelTests.Register();
            PatientSearchTests.Register();
            EsapiAssemblyLocatorTests.Register();
            EsapiPatientDirectoryTests.Register();
            LaunchingTests.Register();
            MainViewModelTests.Register();
            SettingsViewModelTests.Register();
            PrivacyDiagnosticsTests.Register();
            ReleaseMetadataTests.Register();
            return TestHarness.Run();
        }
    }
}
