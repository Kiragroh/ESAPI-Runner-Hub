using System;
using System.Collections;
using System.IO;
using System.Linq;
using EsapiRunnerHub.Configuration;
using EsapiRunnerHub.ViewModels;

namespace EsapiRunnerHub.Tests
{
    internal static class SettingsViewModelTests
    {
        public static void Register()
        {
            TestHarness.Test("settings editor adds edits and deletes applications", AddsEditsAndDeletes);
            TestHarness.Test("settings editor validates and atomically reloads saved INI", SavesAndReloads);
            TestHarness.Test("settings editor exposes executable and Eclipse plug-in kinds", ExposesLaunchKinds);
            TestHarness.Test("settings editor exposes context host options and path", ExposesContextHostOptions);
        }

        private static void AddsEditsAndDeletes()
        {
            var path = Path.Combine(Path.GetTempPath(), "runner-hub-settings-" + Guid.NewGuid().ToString("N"), "settings.ini");
            var viewModel = new SettingsViewModel(new HubConfiguration(), path);

            viewModel.AddApplication();
            TestHarness.AssertEqual(1, viewModel.Applications.Count);
            viewModel.SelectedApplication.Name = "Example Runner";
            viewModel.AssignExecutable(@"C:\Tools\Example.exe");
            viewModel.AssignWorkingDirectory(@"C:\Tools");
            TestHarness.AssertEqual(@"C:\Tools\Example.exe", viewModel.SelectedApplication.Executable);
            viewModel.DeleteSelectedApplication();
            TestHarness.AssertEqual(0, viewModel.Applications.Count);
        }

        private static void SavesAndReloads()
        {
            var directory = Path.Combine(Path.GetTempPath(), "runner-hub-settings-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "settings.ini");
            try
            {
                var viewModel = new SettingsViewModel(new HubConfiguration(), path);
                viewModel.AssignApiAssembly(@"C:\ESAPI\VMS.TPS.Common.Model.API.dll");
                viewModel.AddApplication();
                viewModel.SelectedApplication.Name = "Example Runner";
                viewModel.SelectedApplication.Executable = @"C:\Tools\Example.exe";

                string error;
                TestHarness.AssertTrue(viewModel.TrySave(out error), error);
                TestHarness.AssertTrue(File.Exists(path));
                TestHarness.AssertEqual(Path.GetFullPath(path), viewModel.SettingsPath);

                var reloaded = IniConfigurationStore.Load(path);
                TestHarness.AssertEqual("Example Runner", reloaded.Applications[0].Name);
                TestHarness.AssertContains(File.ReadAllText(path), "EsapiApiAssembly=C:\\ESAPI");
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static void ExposesLaunchKinds()
        {
            var path = Path.Combine(Path.GetTempPath(), "runner-hub-settings-" + Guid.NewGuid().ToString("N"), "settings.ini");
            var viewModel = new SettingsViewModel(new HubConfiguration(), path);
            var property = viewModel.GetType().GetProperty("LaunchKinds");

            TestHarness.AssertTrue(property != null, "SettingsViewModel.LaunchKinds is missing.");
            var values = ((IEnumerable)property.GetValue(viewModel, null)).Cast<object>().Select(value => value.ToString()).ToList();
            TestHarness.AssertTrue(values.Contains("Executable"));
            TestHarness.AssertTrue(values.Contains("EclipsePlugin"));
            TestHarness.AssertTrue(values.Contains("EsapiContextScript"));
        }

        private static void ExposesContextHostOptions()
        {
            var path = Path.Combine(Path.GetTempPath(), "runner-hub-settings-" + Guid.NewGuid().ToString("N"), "settings.ini");
            var viewModel = new SettingsViewModel(new HubConfiguration(), path);

            viewModel.ScriptHostExecutable = @"C:\Runner\ESAPI-Script-Host.exe";

            TestHarness.AssertEqual(@"C:\Runner\ESAPI-Script-Host.exe", viewModel.WorkingConfiguration.Hub.ScriptHostExecutable);
            TestHarness.AssertTrue(viewModel.ScriptEngines.Cast<object>().Any(value => value.ToString() == "EsapiEssentials"));
            TestHarness.AssertTrue(viewModel.ContextRequirements.Cast<object>().Any(value => value.ToString() == "PlanOrStructureSet"));
            TestHarness.AssertTrue(viewModel.ScopeModes.Cast<object>().Any(value => value.ToString() == "Multiple"));
            TestHarness.AssertTrue(viewModel.WriteModes.Cast<object>().Any(value => value.ToString() == "ConfirmSave"));
        }
    }
}
