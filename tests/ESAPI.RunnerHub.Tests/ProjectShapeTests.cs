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

            TestHarness.Test("script host permits configured binaries from UNC paths", () =>
            {
                var path = TestHarness.PathFromRoot("src/ESAPI.ScriptHost/App.config");
                TestHarness.AssertTrue(File.Exists(path), "The Script Host App.config is missing.");
                var config = File.ReadAllText(path);
                var project = File.ReadAllText(TestHarness.PathFromRoot("src/ESAPI.ScriptHost/ESAPI.ScriptHost.csproj"));
                TestHarness.AssertContains(config, "<loadFromRemoteSources enabled=\"true\" />");
                TestHarness.AssertContains(project, "<None Include=\"App.config\" />");
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

            TestHarness.Test("application paths wrap instead of being clipped", () =>
            {
                var xaml = File.ReadAllText(TestHarness.PathFromRoot("src/ESAPI.RunnerHub/MainWindow.xaml"));
                var marker = "Text=\"{Binding CompactPath}\"";
                var start = xaml.IndexOf(marker, System.StringComparison.Ordinal);
                TestHarness.AssertTrue(start >= 0, "CompactPath text block is missing.");
                var declaration = xaml.Substring(start, System.Math.Min(500, xaml.Length - start));
                TestHarness.AssertContains(declaration, "TextWrapping=\"Wrap\"");
                TestHarness.AssertFalse(declaration.Contains("TextTrimming=\"CharacterEllipsis\""));
            });

            TestHarness.Test("script host error dialog identifies the safe execution phase", () =>
            {
                var program = File.ReadAllText(TestHarness.PathFromRoot("src/ESAPI.ScriptHost/Program.cs"));
                TestHarness.AssertContains(program, "The script ended without saving");
                TestHarness.AssertContains(program, "Failure phase:");
                TestHarness.AssertContains(program, "Failure code:");
                TestHarness.AssertContains(program, "Failure type:");
                TestHarness.AssertContains(program, "StageDataKey");
            });

            TestHarness.Test("Citrix launcher shows only English product errors", () =>
            {
                var program = File.ReadAllText(TestHarness.PathFromRoot("src/ESAPI.CitrixLauncher/Program.cs"));
                TestHarness.AssertContains(program, "The installation directory could not be determined.");
                TestHarness.AssertContains(program, "The selected application version was not found.");
                TestHarness.AssertContains(program, "The ESAPI Runner Hub could not be started.");
                TestHarness.AssertFalse(program.Contains("Das Installationsverzeichnis"));
                TestHarness.AssertFalse(program.Contains("wurde nicht gefunden"));
            });

            TestHarness.Test("Hub exposes privacy-safe context command line modes", () =>
            {
                var app = File.ReadAllText(TestHarness.PathFromRoot("src/ESAPI.RunnerHub/App.xaml.cs"));
                var runner = File.ReadAllText(TestHarness.PathFromRoot("src/ESAPI.RunnerHub/Launching/ContextCommandLineRunner.cs"));
                TestHarness.AssertContains(runner, "--run-context");
                TestHarness.AssertContains(runner, "--replay-latest");
                TestHarness.AssertContains(runner, "--run-request");
                TestHarness.AssertContains(app, "ContextCommandLineRunner");
                TestHarness.AssertContains(app, "TryRunPending");
            });

            TestHarness.Test("Citrix debug helper creates an explicit context request", () =>
            {
                var helper = File.ReadAllText(TestHarness.PathFromRoot("tools/Invoke-CitrixContextDebug.ps1"));
                TestHarness.AssertContains(helper, "ApplicationId");
                TestHarness.AssertContains(helper, "PatientId");
                TestHarness.AssertContains(helper, ".result.json");
                TestHarness.AssertContains(helper, ".pending");
                TestHarness.AssertContains(helper, "RequestedBySid");
                TestHarness.AssertContains(helper, "WindowsIdentity");
                TestHarness.AssertContains(helper, "ClaimSeconds");
                TestHarness.AssertFalse(helper.Contains("$requestedBy:"));
                TestHarness.AssertFalse(helper.Contains("-cmdline"));
                TestHarness.AssertContains(helper, "ESAPI-Runner-Hub.lnk");
            });

            TestHarness.Test("main window uses a compact centered responsive catalogue", () =>
            {
                var xaml = File.ReadAllText(TestHarness.PathFromRoot("src/ESAPI.RunnerHub/MainWindow.xaml"));
                TestHarness.AssertContains(xaml, "x:Name=\"CatalogueFilterBar\"");
                TestHarness.AssertContains(xaml, "HorizontalScrollBarVisibility=\"Disabled\"");
                TestHarness.AssertContains(xaml, "Width=\"284\"");
                TestHarness.AssertContains(xaml, "ReplayAvailabilityText");
                TestHarness.AssertFalse(xaml.Contains("<ColumnDefinition Width=\"154\" />"));
                TestHarness.AssertFalse(xaml.Contains("<GridView>"));
            });

            TestHarness.Test("privacy mode covers patient context paths and activity", () =>
            {
                var xaml = File.ReadAllText(TestHarness.PathFromRoot("src/ESAPI.RunnerHub/MainWindow.xaml"));
                TestHarness.AssertContains(xaml, "Command=\"{Binding TogglePrivacyBlurCommand}\"");
                TestHarness.AssertContains(xaml, "DataContext.IsPrivacyBlurEnabled");
                TestHarness.AssertContains(xaml, "x:Key=\"SensitiveText\"");
                TestHarness.AssertContains(xaml, "x:Key=\"SensitiveControl\"");
                TestHarness.AssertContains(xaml, "Property=\"ToolTip\" Value=\"{x:Null}\"");
                TestHarness.AssertContains(xaml, "Start with patient");
            });

            TestHarness.Test("offline UI smoke data is synthetic and history isolated", () =>
            {
                var code = File.ReadAllText(TestHarness.PathFromRoot("src/ESAPI.RunnerHub/MainWindow.xaml.cs"));
                TestHarness.AssertEqual(4, System.Text.RegularExpressions.Regex.Matches(code,
                    "configuration\\.Applications\\.Add\\(new ApplicationDefinition").Count);
                TestHarness.AssertContains(code, "new MainViewModel(configuration, initialPatients, null");
                TestHarness.AssertContains(code, "new ActivityRowViewModel");
                TestHarness.AssertContains(code, "viewModel.SelectPatient");
                TestHarness.AssertContains(code, "viewModel.SelectedPlan = viewModel.Plans.FirstOrDefault()");
                TestHarness.AssertContains(code, "LaunchHistoryState.Exited");
            });

            TestHarness.Test("public documentation explains the Citrix runner advantage", () =>
            {
                var readme = File.ReadAllText(TestHarness.PathFromRoot("README.md"));
                TestHarness.AssertContains(readme, "## Why a Citrix runner?");
                TestHarness.AssertContains(readme, "Citrix Published Application");
                TestHarness.AssertContains(readme, "blue ARIA toolbar");
                TestHarness.AssertContains(readme, "remote desktop");
                TestHarness.AssertContains(readme, "child process");
                TestHarness.AssertContains(readme, "docs/images/esapi-runner-hub-overview.png");

                var citrix = File.ReadAllText(TestHarness.PathFromRoot("citrix/README-Citrix.md"));
                TestHarness.AssertContains(citrix, "Leave the Arguments field empty.");
                TestHarness.AssertContains(citrix, "one stable published entry point");
                TestHarness.AssertContains(citrix, "user-scoped shared request");
                TestHarness.AssertFalse(citrix.Contains("currently configured `\"%**\"` placeholder"));

                var debugging = File.ReadAllText(TestHarness.PathFromRoot("docs/CONTEXT_DEBUGGING.md"));
                TestHarness.AssertContains(debugging, "one Citrix Published Application");
                TestHarness.AssertContains(debugging, "does not depend on client argument forwarding");
            });
        }
    }
}
