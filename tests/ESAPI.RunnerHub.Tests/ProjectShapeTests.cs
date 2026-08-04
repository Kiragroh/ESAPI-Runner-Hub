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

            TestHarness.Test("synthetic test host permits assemblies from a UNC worktree", () =>
            {
                var path = TestHarness.PathFromRoot("tests/ESAPI.RunnerHub.Tests/App.config");
                TestHarness.AssertTrue(File.Exists(path), "The test host App.config is missing.");
                var config = File.ReadAllText(path);
                TestHarness.AssertContains(config, "<loadFromRemoteSources enabled=\"true\" />");
            });

            TestHarness.Test("main window shows restartable persistent activity", () =>
            {
                var path = TestHarness.PathFromRoot("src/ESAPI.RunnerHub/MainWindow.xaml");
                var xaml = File.ReadAllText(path);
                TestHarness.AssertContains(xaml, "ItemsSource=\"{Binding Activities}\"");
                TestHarness.AssertContains(xaml, "RunAgainCommand");
                TestHarness.AssertContains(xaml, "ApplicationName");
                TestHarness.AssertContains(xaml, "ArtifactLabel");
                TestHarness.AssertContains(xaml, "ContextSummary");
                TestHarness.AssertContains(xaml, "Started");
                TestHarness.AssertContains(xaml, "Status");
            });
        }
    }
}
