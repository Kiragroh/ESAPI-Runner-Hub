using System.IO;

namespace EsapiRunnerHub.Tests
{
    internal static class ProjectShapeTests
    {
        public static void Register()
        {
            TestHarness.Test("project targets net48 x64 WPF", () =>
            {
                var path = TestHarness.PathFromRoot("src/ESAPI.RunnerHub/ESAPI.RunnerHub.csproj");
                var project = File.ReadAllText(path);
                TestHarness.AssertContains(project, "<TargetFrameworkVersion>v4.8</TargetFrameworkVersion>");
                TestHarness.AssertContains(project, "<PlatformTarget>x64</PlatformTarget>");
                TestHarness.AssertContains(project, "<OutputType>WinExe</OutputType>");
                TestHarness.AssertContains(project, "<ProjectTypeGuids>{60dc8134-eba5-43b8-bcc9-bb4bc16c2548}");
            });
        }
    }
}
