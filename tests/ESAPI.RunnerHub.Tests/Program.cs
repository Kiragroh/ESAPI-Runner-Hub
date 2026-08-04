namespace EsapiRunnerHub.Tests
{
    internal static class Program
    {
        [System.STAThread]
        private static int Main()
        {
            ProjectShapeTests.Register();
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
