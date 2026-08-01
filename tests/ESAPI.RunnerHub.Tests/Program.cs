namespace EsapiRunnerHub.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            ProjectShapeTests.Register();
            ConfigurationTests.Register();
            PatientSearchTests.Register();
            EsapiPatientDirectoryTests.Register();
            LaunchingTests.Register();
            MainViewModelTests.Register();
            return TestHarness.Run();
        }
    }
}
