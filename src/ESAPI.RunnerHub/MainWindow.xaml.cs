using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using EsapiRunnerHub.Configuration;
using EsapiRunnerHub.Context;
using EsapiRunnerHub.Esapi;
using EsapiRunnerHub.Launching;
using EsapiRunnerHub.Patients;
using EsapiRunnerHub.Privacy;
using EsapiRunnerHub.ViewModels;

namespace EsapiRunnerHub
{
    public partial class MainWindow : Window
    {
        private readonly string[] arguments;
        private string settingsPath;
        private bool smokeMode;

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
            smokeMode = arguments.Any(value => string.Equals(value, "--offline-ui-smoke", StringComparison.OrdinalIgnoreCase));
            await InitializeAsync(smokeMode);
        }

        private async Task InitializeAsync(bool smoke)
        {
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
            viewModel.PatientSelectionChanged += async patient =>
            {
                var patientId = patient.Id;
                if (smoke)
                {
                    viewModel.SetContextDirectory(SyntheticContext());
                    return;
                }
                var contextResult = await LoadContextOnStaAsync(configuration.Hub.ResolvedEsapiApiAssembly,
                    configuration.Hub.ResolvedEsapiTypesAssembly, patientId);
                if (!ReferenceEquals(DataContext, viewModel) || viewModel.SelectedPatientId != patientId) return;
                viewModel.SetContextDirectory(contextResult.Directory);
                TechnicalLog.Current.Write(contextResult.IsAvailable ? "INFO" : "WARN",
                    contextResult.IsAvailable ? "esapi_context_loaded" : contextResult.ErrorCode,
                    string.Empty, contextResult.Exception);
            };
            DataContext = viewModel;
            TechnicalLog.Configure(configuration.Hub.ResolvedLogDirectory);

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
                TechnicalLog.Current.Write(patientResult.IsAvailable ? "INFO" : "WARN",
                    patientResult.IsAvailable ? "esapi_directory_loaded" : patientResult.ErrorCode,
                    string.Empty, patientResult.Exception);
            }

            var pathProbe = new PathProbe();
            var probes = viewModel.Applications.Select(async card =>
            {
                var result = await pathProbe.ProbeAsync(card.Definition.ResolvedExecutable, configuration.Hub.PathProbeTimeoutMs);
                Dispatcher.Invoke(() => viewModel.UpdateApplicationReadiness(card.Id, result));
                if (result.Readiness != PathReadiness.Ready)
                {
                    TechnicalLog.Current.Write("WARN",
                        result.Readiness == PathReadiness.Missing ? "executable_missing" : "network_path_unavailable",
                        card.Id, null);
                }
            });
            await Task.WhenAll(probes);
        }

        private async void Settings_Click(object sender, RoutedEventArgs e)
        {
            var current = DataContext as MainViewModel;
            var configuration = current == null ? CreateEmptyConfiguration(settingsPath) : current.Configuration;
            var window = new SettingsWindow(configuration, settingsPath) { Owner = this };
            if (window.ShowDialog() == true)
            {
                settingsPath = window.SavedSettingsPath;
                smokeMode = false;
                await InitializeAsync(false);
            }
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
                Executable = executable, LaunchKind = LaunchKind.EsapiContextScript, ScriptEngine = ScriptEngine.Eclipse,
                ContextRequirement = ContextRequirement.PlanOrStructureSet, ScopeMode = ScopeMode.Multiple,
                WriteMode = WriteMode.ReadOnly, PatientMode = PatientMode.Required, PatientTransport = PatientTransport.None,
                Enabled = true, SortOrder = 10
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
                    completion.SetResult(PatientDirectoryLoadResult.Offline("esapi_unavailable", exception.Message, exception));
                }
            }) { IsBackground = true, Name = "ESAPI patient directory loader" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }

        private static Task<ContextDirectoryLoadResult> LoadContextOnStaAsync(string apiAssembly, string typesAssembly, string patientId)
        {
            var completion = new TaskCompletionSource<ContextDirectoryLoadResult>();
            var thread = new Thread(() =>
            {
                try
                {
                    completion.SetResult(new ReflectionContextDirectoryLoader().Load(apiAssembly, typesAssembly, patientId));
                }
                catch (Exception exception)
                {
                    completion.SetResult(ContextDirectoryLoadResult.Offline("context_unavailable", exception));
                }
            }) { IsBackground = true, Name = "ESAPI planning context loader" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }

        private static EsapiRunnerHub.Context.ContextDirectory SyntheticContext()
        {
            var directory = new EsapiRunnerHub.Context.ContextDirectory();
            directory.Courses.Add(new EsapiRunnerHub.Context.CourseDescriptor { Id = "COURSE-DEMO" });
            directory.Plans.Add(new EsapiRunnerHub.Context.PlanDescriptor
            {
                Id = "PLAN-DEMO", CourseId = "COURSE-DEMO", StructureSetId = "SS-DEMO", ImageId = "CT-DEMO", Kind = "ExternalPlanSetup"
            });
            directory.StructureSets.Add(new EsapiRunnerHub.Context.StructureSetDescriptor { Id = "SS-DEMO", ImageId = "CT-DEMO" });
            directory.StructureSets.Add(new EsapiRunnerHub.Context.StructureSetDescriptor { Id = "SS-CONTOUR", ImageId = "CT-DEMO" });
            directory.Images.Add(new EsapiRunnerHub.Context.ImageDescriptor { Id = "CT-DEMO" });
            return directory;
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
