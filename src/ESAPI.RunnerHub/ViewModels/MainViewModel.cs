using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using EsapiRunnerHub.Configuration;
using EsapiRunnerHub.Infrastructure;
using EsapiRunnerHub.Launching;
using EsapiRunnerHub.Patients;
using EsapiRunnerHub.Privacy;

namespace EsapiRunnerHub.ViewModels
{
    public sealed class MainViewModel : ObservableObject
    {
        private readonly ChildProcessLauncher launcher = new ChildProcessLauncher();
        private PatientSearchIndex patientIndex;
        private PatientRecord selectedPatient;
        private string searchText;
        private string applicationFilter;
        private string selectedCategory;
        private string esapiStatusText;
        private bool isEsapiAvailable;
        private string notificationText;

        public MainViewModel(HubConfiguration configuration, IEnumerable<PatientRecord> patients)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Applications = new ObservableCollection<ApplicationCardViewModel>(
                configuration.Applications.Where(item => item.Enabled)
                    .OrderBy(item => item.SortOrder)
                    .ThenBy(item => item.Name)
                    .Select(item => new ApplicationCardViewModel(item)));
            VisibleApplications = new ObservableCollection<ApplicationCardViewModel>();
            Categories = new ObservableCollection<string>(new[] { "All tools" }.Concat(
                Applications.Select(item => item.Category).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item)));
            Suggestions = new ObservableCollection<PatientRecord>();
            Processes = new ObservableCollection<ProcessRowViewModel>();
            SelectPatientCommand = new RelayCommand(parameter => SelectPatient(parameter as PatientRecord));
            ClearPatientCommand = new RelayCommand(parameter => ClearPatient());
            StartWithPatientCommand = new RelayCommand(parameter => Start(parameter as ApplicationCardViewModel, true));
            StartWithoutPatientCommand = new RelayCommand(parameter => Start(parameter as ApplicationCardViewModel, false));
            selectedCategory = "All tools";
            SetPatients(patients ?? Enumerable.Empty<PatientRecord>());
            SetEsapiStatus(false, "Loading patient directory…");
            UpdateVisibleApplications();
        }

        public HubConfiguration Configuration { get; private set; }
        public ObservableCollection<ApplicationCardViewModel> Applications { get; private set; }
        public ObservableCollection<ApplicationCardViewModel> VisibleApplications { get; private set; }
        public ObservableCollection<string> Categories { get; private set; }
        public ObservableCollection<PatientRecord> Suggestions { get; private set; }
        public ObservableCollection<ProcessRowViewModel> Processes { get; private set; }
        public ICommand SelectPatientCommand { get; private set; }
        public ICommand ClearPatientCommand { get; private set; }
        public ICommand StartWithPatientCommand { get; private set; }
        public ICommand StartWithoutPatientCommand { get; private set; }

        public PatientRecord SelectedPatient { get { return selectedPatient; } }
        public bool HasSelectedPatient { get { return selectedPatient != null; } }
        public string SelectedPatientDisplay { get { return selectedPatient == null ? string.Empty : selectedPatient.Display; } }
        public string SelectedPatientId { get { return selectedPatient == null ? string.Empty : selectedPatient.Id; } }

        public string SearchText
        {
            get { return searchText; }
            set
            {
                if (SetProperty(ref searchText, value))
                {
                    UpdateSuggestions();
                }
            }
        }

        public string ApplicationFilter
        {
            get { return applicationFilter; }
            set
            {
                if (SetProperty(ref applicationFilter, value))
                {
                    UpdateVisibleApplications();
                }
            }
        }

        public string SelectedCategory
        {
            get { return selectedCategory; }
            set
            {
                if (SetProperty(ref selectedCategory, value))
                {
                    UpdateVisibleApplications();
                }
            }
        }

        public string EsapiStatusText { get { return esapiStatusText; } }
        public bool IsEsapiAvailable { get { return isEsapiAvailable; } }
        public string NotificationText { get { return notificationText; } }

        public void SetPatients(IEnumerable<PatientRecord> patients)
        {
            patientIndex = new PatientSearchIndex(patients);
            ClearPatient();
            UpdateSuggestions();
        }

        public void SelectPatient(PatientRecord patient)
        {
            if (patient == null)
            {
                return;
            }

            selectedPatient = patient;
            searchText = string.Empty;
            Suggestions.Clear();
            RaisePropertyChanged(nameof(SelectedPatient));
            RaisePropertyChanged(nameof(HasSelectedPatient));
            RaisePropertyChanged(nameof(SelectedPatientDisplay));
            RaisePropertyChanged(nameof(SelectedPatientId));
            RaisePropertyChanged(nameof(SearchText));
            foreach (var application in Applications)
            {
                application.SetPatient(patient);
            }
        }

        public void ClearPatient()
        {
            selectedPatient = null;
            foreach (var application in Applications)
            {
                application.SetPatient(null);
            }

            RaisePropertyChanged(nameof(SelectedPatient));
            RaisePropertyChanged(nameof(HasSelectedPatient));
            RaisePropertyChanged(nameof(SelectedPatientDisplay));
            RaisePropertyChanged(nameof(SelectedPatientId));
        }

        public void SetEsapiStatus(bool available, string message)
        {
            isEsapiAvailable = available;
            esapiStatusText = message ?? string.Empty;
            RaisePropertyChanged(nameof(IsEsapiAvailable));
            RaisePropertyChanged(nameof(EsapiStatusText));
        }

        public void UpdateApplicationReadiness(string applicationId, PathProbeResult result)
        {
            var application = Applications.FirstOrDefault(item => string.Equals(item.Id, applicationId, StringComparison.OrdinalIgnoreCase));
            if (application != null)
            {
                application.SetReadiness(result.Readiness, result.Message);
            }
        }

        private void UpdateSuggestions()
        {
            Suggestions.Clear();
            if (patientIndex == null || string.IsNullOrWhiteSpace(searchText))
            {
                return;
            }

            foreach (var patient in patientIndex.Find(searchText, Math.Max(1, Configuration.Hub.SearchMaxResults)))
            {
                Suggestions.Add(patient);
            }
        }

        private void UpdateVisibleApplications()
        {
            var query = Applications.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(selectedCategory) && !string.Equals(selectedCategory, "All tools", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(item => string.Equals(item.Category, selectedCategory, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(applicationFilter))
            {
                query = query.Where(item => (item.Name + " " + item.Description + " " + item.Category)
                    .IndexOf(applicationFilter.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
            }

            VisibleApplications.Clear();
            foreach (var application in query)
            {
                VisibleApplications.Add(application);
            }
        }

        private void Start(ApplicationCardViewModel card, bool withPatient)
        {
            if (card == null)
            {
                return;
            }

            if (card.Definition.LaunchKind == LaunchKind.EclipsePlugin)
            {
                notificationText = card.Name + " runs inside Eclipse. Open it from Tools > Scripts.";
                TechnicalLog.Current.Write("INFO", "eclipse_plugin_reference", card.Id, null);
                RaisePropertyChanged(nameof(NotificationText));
                return;
            }

            try
            {
                var request = ArgumentComposer.Compose(card.Definition, selectedPatient, withPatient);
                var process = launcher.Start(request);
                var lifecycleLog = TechnicalLog.Current;
                lifecycleLog.Write("INFO", "child_started", card.Id, null);
                var exitLogged = 0;
                Action writeExit = () =>
                {
                    if (Interlocked.Exchange(ref exitLogged, 1) == 0)
                    {
                        lifecycleLog.Write(process.ExitCode.GetValueOrDefault() == 0 ? "INFO" : "WARN",
                            process.ExitCode.GetValueOrDefault() == 0 ? "child_exit_ok" : "child_exit_nonzero",
                            card.Id, null);
                    }
                };
                process.Exited += (sender, args) => writeExit();
                if (!process.IsRunning)
                {
                    writeExit();
                }
                Processes.Insert(0, new ProcessRowViewModel(card.Name, process));
                notificationText = card.Name + " started in a separate process.";
            }
            catch (Exception exception)
            {
                notificationText = card.Name + " could not be started: " + exception.Message;
                TechnicalLog.Current.Write("ERROR", "child_start_failed", card.Id, exception);
            }

            RaisePropertyChanged(nameof(NotificationText));
        }
    }
}
