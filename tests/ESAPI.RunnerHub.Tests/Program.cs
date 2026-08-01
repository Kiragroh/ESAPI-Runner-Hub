namespace EsapiRunnerHub.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            ProjectShapeTests.Register();
            ConfigurationTests.Register();
            return TestHarness.Run();
        }
    }
}
