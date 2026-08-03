namespace EsapiRunnerHub.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            ProjectShapeTests.Register();
            ConfigurationTests.Register();
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
