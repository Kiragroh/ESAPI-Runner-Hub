using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using EsapiRunnerHub.Configuration;
using EsapiRunnerHub.Esapi;
using EsapiRunnerHub.Launching;
using EsapiRunnerHub.Patients;
using EsapiRunnerHub.ViewModels;

namespace EsapiRunnerHub
{
    public partial class MainWindow : Window
    {
        private readonly string[] arguments;
        private string settingsPath;

        public MainWindow()
            : this(Array.Empty<string>())
        {
        }

        public MainWindow(string[] arguments)
        {
            this.arguments = arguments ?? Array.Empty<string>();
            InitializeComponent();
            Loaded += WindowLoaded;
        }

        private async void WindowLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= WindowLoaded;
            settingsPath = ResolveSettingsPath(arguments);
            var smoke = arguments.Any(value => string.Equals(value, "--offline-ui-smoke", StringComparison.OrdinalIgnoreCase));
            HubConfiguration configuration;
            try
            {
                configuration = smoke ? CreateSmokeConfiguration(settingsPath) : LoadConfiguration(settingsPath);
            }
            catch (Exception exception)
            {
                configuration = CreateEmptyConfiguration(settingsPath);
                MessageBox.Show(this, "Settings could not be loaded. The catalogue remains available.\n\n" + exception.Message,
                    "ESAPI Runner Hub", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            var initialPatients = smoke ? SyntheticPatients() : Enumerable.Empty<PatientRecord>();
            var viewModel = new MainViewModel(configuration, initialPatients);
            DataContext = viewModel;

            if (smoke)
            {
                viewModel.SetEsapiStatus(false, "Offline demo · synthetic data");
            }
            else
            {
                var patientResult = await LoadDirectoryOnStaAsync(configuration.Hub.ResolvedEsapiApiAssembly, configuration.Hub.ResolvedEsapiTypesAssembly);
                viewModel.SetPatients(patientResult.Patients);
                viewModel.SetEsapiStatus(patientResult.IsAvailable,
                    patientResult.IsAvailable
                        ? "ESAPI ready · " + patientResult.Patients.Count + " patients cached"
                        : "ESAPI offline · catalogue available");
            }

            var pathProbe = new PathProbe();
            var probes = viewModel.Applications.Select(async card =>
            {
                var result = await pathProbe.ProbeAsync(card.Definition.ResolvedExecutable, configuration.Hub.PathProbeTimeoutMs);
                Dispatcher.Invoke(() => viewModel.UpdateApplicationReadiness(card.Id, result));
            });
            await Task.WhenAll(probes);
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(this, "The settings editor is opening in the next implementation step.\n\nCurrent file:\n" + settingsPath,
                "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static HubConfiguration LoadConfiguration(string path)
        {
            return File.Exists(path) ? IniConfigurationStore.Load(path) : CreateEmptyConfiguration(path);
        }

        private static HubConfiguration CreateEmptyConfiguration(string path)
        {
            var configuration = new HubConfiguration { SourcePath = Path.GetFullPath(path) };
            configuration.ResolvePaths();
            return configuration;
        }

        private static HubConfiguration CreateSmokeConfiguration(string path)
        {
            var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe");
            var configuration = new HubConfiguration { SourcePath = Path.GetFullPath(path) };
            configuration.Applications.Add(new ApplicationDefinition
            {
                Id = "synthetic-review", Name = "Synthetic Plan Review", Category = "Plan review",
                Description = "Demonstrates a patient-aware review application without clinical data.",
                Executable = executable, PatientMode = PatientMode.Required, PatientTransport = PatientTransport.Argument,
                PatientArgumentTemplate = "--patient-id {PatientId}", Enabled = true, SortOrder = 10
            });
            configuration.Applications.Add(new ApplicationDefinition
            {
                Id = "synthetic-export", Name = "Synthetic DICOM Export", Category = "Export",
                Description = "Standalone example that can start with or without the selected patient.",
                Executable = executable, PatientMode = PatientMode.Optional, PatientTransport = PatientTransport.Environment,
                PatientEnvironmentKey = "RUNNER_PATIENT_ID", Enabled = true, SortOrder = 20
            });
            configuration.Applications.Add(new ApplicationDefinition
            {
                Id = "synthetic-utility", Name = "Configuration Utility", Category = "Utilities",
                Description = "Patient-independent application for maintenance and documentation.",
                Executable = executable, PatientMode = PatientMode.None, PatientTransport = PatientTransport.None,
                Enabled = true, SortOrder = 30
            });
            configuration.ResolvePaths();
            return configuration;
        }

        private static IEnumerable<PatientRecord> SyntheticPatients()
        {
            return new[]
            {
                new PatientRecord("SYN-1001", "Ada", "Example", 0),
                new PatientRecord("SYN-1002", "Linus", "Sample", 1),
                new PatientRecord("SYN-1003", "Grace", "Demo", 2)
            };
        }

        private static Task<PatientDirectoryLoadResult> LoadDirectoryOnStaAsync(string apiAssembly, string typesAssembly)
        {
            var completion = new TaskCompletionSource<PatientDirectoryLoadResult>();
            var thread = new Thread(() =>
            {
                try
                {
                    completion.SetResult(new ReflectionPatientDirectoryLoader().Load(apiAssembly, typesAssembly));
                }
                catch (Exception exception)
                {
                    completion.SetResult(PatientDirectoryLoadResult.Offline("esapi_unavailable", exception.Message));
                }
            }) { IsBackground = true, Name = "ESAPI patient directory loader" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }

        private static string ResolveSettingsPath(string[] args)
        {
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], "--settings", StringComparison.OrdinalIgnoreCase))
                {
                    return Path.GetFullPath(args[index + 1]);
                }
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini");
        }
    }
}
