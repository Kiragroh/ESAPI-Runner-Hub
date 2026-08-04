using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using EsapiRunnerHub.Configuration;
using EsapiRunnerHub.Infrastructure;
using EsapiRunnerHub.Launching;
using EsapiRunnerHub.Patients;
using EsapiRunnerHub.Privacy;
using EsapiRunnerHub.Context;
using EsapiRunnerHub.Catalog;
using EsapiRunnerHub.History;

namespace EsapiRunnerHub.ViewModels
{
    public sealed class MainViewModel : ObservableObject
    {
        private readonly ChildProcessLauncher launcher = new ChildProcessLauncher();
        private readonly LaunchHistoryStore historyStore;
        private readonly ProtectedContextEnvelope contextProtector;
        private readonly List<LaunchHistoryEntry> historyEntries = new List<LaunchHistoryEntry>();
        private readonly object historyGate = new object();
        private PatientSearchIndex patientIndex;
        private PatientRecord selectedPatient;
        private string searchText;
        private string applicationFilter;
        private string selectedCategory;
        private ArtifactFilterOption selectedArtifactFilter;
        private string esapiStatusText;
        private bool isEsapiAvailable;
        private string notificationText;
        private ContextDirectory contextDirectory = new ContextDirectory();
        private CourseDescriptor selectedCourse;
        private PlanDescriptor selectedPlan;
        private PlanSumDescriptor selectedPlanSum;
        private StructureSetDescriptor selectedStructureSet;
        private ImageDescriptor selectedImage;
        private string contextStatusText;

        public MainViewModel(HubConfiguration configuration, IEnumerable<PatientRecord> patients)
            : this(configuration, patients, CreateDefaultHistoryStore(configuration), new ProtectedContextEnvelope())
        {
        }

        public MainViewModel(HubConfiguration configuration, IEnumerable<PatientRecord> patients,
            LaunchHistoryStore historyStore, ProtectedContextEnvelope contextProtector)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.historyStore = historyStore;
            this.contextProtector = contextProtector ?? throw new ArgumentNullException(nameof(contextProtector));
            Applications = new ObservableCollection<ApplicationCardViewModel>(
                configuration.Applications.Where(item => item.Enabled)
                    .OrderBy(item => item.SortOrder)
                    .ThenBy(item => item.Name)
                    .Select(item => new ApplicationCardViewModel(item, configuration.Hub.StrHubBaseUrl)));
            VisibleApplications = new ObservableCollection<ApplicationCardViewModel>();
            Categories = new ObservableCollection<string>(new[] { "All tools" }.Concat(
                Applications.Select(item => item.Category).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item)));
            ArtifactFilters = new ObservableCollection<ArtifactFilterOption>(new[]
            {
                new ArtifactFilterOption(ApplicationArtifactFilter.All, "All formats"),
                new ArtifactFilterOption(ApplicationArtifactFilter.Standalone, "Standalone"),
                new ArtifactFilterOption(ApplicationArtifactFilter.SingleFile, "Single-file (.cs)"),
                new ArtifactFilterOption(ApplicationArtifactFilter.Binary, "Binary (.dll)")
            });
            Suggestions = new ObservableCollection<PatientRecord>();
            Processes = new ObservableCollection<ProcessRowViewModel>();
            Activities = new ObservableCollection<ActivityRowViewModel>();
            Courses = new ObservableCollection<CourseDescriptor>();
            Plans = new ObservableCollection<PlanDescriptor>();
            PlanSums = new ObservableCollection<PlanSumDescriptor>();
            StructureSets = new ObservableCollection<StructureSetDescriptor>();
            Images = new ObservableCollection<ImageDescriptor>();
            ContextSelection = new ContextSelection();
            SelectPatientCommand = new RelayCommand(parameter => SelectPatient(parameter as PatientRecord));
            ClearPatientCommand = new RelayCommand(parameter => ClearPatient());
            StartWithPatientCommand = new RelayCommand(parameter => Start(parameter as ApplicationCardViewModel, true));
            StartWithoutPatientCommand = new RelayCommand(parameter => Start(parameter as ApplicationCardViewModel, false));
            StartContextCommand = new RelayCommand(parameter => StartContext(parameter as ApplicationCardViewModel));
            RunAgainCommand = new RelayCommand(parameter => RunAgain(parameter as ActivityRowViewModel),
                parameter => parameter is ActivityRowViewModel row && row.CanRunAgain);
            OpenReadmeCommand = new RelayCommand(parameter => OpenReadme(parameter as ApplicationCardViewModel),
                parameter => parameter is ApplicationCardViewModel card && card.HasHubReadme);
            selectedCategory = "All tools";
            selectedArtifactFilter = ArtifactFilters[0];
            SetPatients(patients ?? Enumerable.Empty<PatientRecord>());
            SetEsapiStatus(false, "Loading patient directory…");
            UpdateVisibleApplications();
            LoadHistory();
        }

        public HubConfiguration Configuration { get; private set; }
        public ObservableCollection<ApplicationCardViewModel> Applications { get; private set; }
        public ObservableCollection<ApplicationCardViewModel> VisibleApplications { get; private set; }
        public ObservableCollection<string> Categories { get; private set; }
        public ObservableCollection<ArtifactFilterOption> ArtifactFilters { get; private set; }
        public ObservableCollection<PatientRecord> Suggestions { get; private set; }
        public ObservableCollection<ProcessRowViewModel> Processes { get; private set; }
        public ObservableCollection<ActivityRowViewModel> Activities { get; private set; }
        public ObservableCollection<CourseDescriptor> Courses { get; private set; }
        public ObservableCollection<PlanDescriptor> Plans { get; private set; }
        public ObservableCollection<PlanSumDescriptor> PlanSums { get; private set; }
        public ObservableCollection<StructureSetDescriptor> StructureSets { get; private set; }
        public ObservableCollection<ImageDescriptor> Images { get; private set; }
        public ContextSelection ContextSelection { get; private set; }
        public ICommand SelectPatientCommand { get; private set; }
        public ICommand ClearPatientCommand { get; private set; }
        public ICommand StartWithPatientCommand { get; private set; }
        public ICommand StartWithoutPatientCommand { get; private set; }
        public ICommand StartContextCommand { get; private set; }
        public ICommand RunAgainCommand { get; private set; }
        public ICommand OpenReadmeCommand { get; private set; }
        public event Action<PatientRecord> PatientSelectionChanged;

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
        public string ContextStatusText { get { return contextStatusText; } }

        public CourseDescriptor SelectedCourse
        {
            get { return selectedCourse; }
            set
            {
                if (SetProperty(ref selectedCourse, value))
                {
                    ContextSelection.CourseId = value == null ? null : value.Id;
                    NotifyContextChanged();
                }
            }
        }

        public ArtifactFilterOption SelectedArtifactFilter
        {
            get { return selectedArtifactFilter; }
            set
            {
                if (SetProperty(ref selectedArtifactFilter, value))
                {
                    UpdateVisibleApplications();
                }
            }
        }

        public PlanDescriptor SelectedPlan
        {
            get { return selectedPlan; }
            set
            {
                if (!SetProperty(ref selectedPlan, value)) return;
                if (value != null)
                {
                    ContextSelection.SelectPlan(contextDirectory, value.Id);
                    selectedPlanSum = null;
                    selectedCourse = Courses.FirstOrDefault(item => item.Id == ContextSelection.CourseId);
                    selectedStructureSet = StructureSets.FirstOrDefault(item => item.Id == ContextSelection.StructureSetId);
                    selectedImage = Images.FirstOrDefault(item => item.Id == ContextSelection.ImageId);
                    RaisePropertyChanged(nameof(SelectedPlanSum));
                    RaisePropertyChanged(nameof(SelectedCourse));
                    RaisePropertyChanged(nameof(SelectedStructureSet));
                    RaisePropertyChanged(nameof(SelectedImage));
                }
                NotifyContextChanged();
            }
        }

        public PlanSumDescriptor SelectedPlanSum
        {
            get { return selectedPlanSum; }
            set
            {
                if (!SetProperty(ref selectedPlanSum, value)) return;
                if (value != null)
                {
                    ContextSelection.SelectPlanSum(contextDirectory, value.Id);
                    selectedPlan = null;
                    selectedCourse = Courses.FirstOrDefault(item => item.Id == ContextSelection.CourseId);
                    selectedStructureSet = StructureSets.FirstOrDefault(item => item.Id == ContextSelection.StructureSetId);
                    selectedImage = Images.FirstOrDefault(item => item.Id == ContextSelection.ImageId);
                    RaisePropertyChanged(nameof(SelectedPlan));
                    RaisePropertyChanged(nameof(SelectedCourse));
                    RaisePropertyChanged(nameof(SelectedStructureSet));
                    RaisePropertyChanged(nameof(SelectedImage));
                }
                NotifyContextChanged();
            }
        }

        public StructureSetDescriptor SelectedStructureSet
        {
            get { return selectedStructureSet; }
            set
            {
                if (!SetProperty(ref selectedStructureSet, value)) return;
                if (value != null)
                {
                    ContextSelection.SelectStructureSet(contextDirectory, value.Id);
                    selectedPlan = null;
                    selectedPlanSum = null;
                    selectedCourse = null;
                    selectedImage = Images.FirstOrDefault(item => item.Id == ContextSelection.ImageId);
                    RaisePropertyChanged(nameof(SelectedPlan));
                    RaisePropertyChanged(nameof(SelectedPlanSum));
                    RaisePropertyChanged(nameof(SelectedCourse));
                    RaisePropertyChanged(nameof(SelectedImage));
                }
                NotifyContextChanged();
            }
        }

        public ImageDescriptor SelectedImage
        {
            get { return selectedImage; }
            set
            {
                if (!SetProperty(ref selectedImage, value)) return;
                if (value != null)
                {
                    ContextSelection.SelectImage(contextDirectory, value.Id);
                    if (string.IsNullOrWhiteSpace(ContextSelection.StructureSetId))
                    {
                        selectedPlan = null;
                        selectedPlanSum = null;
                        selectedStructureSet = null;
                        selectedCourse = null;
                        RaisePropertyChanged(nameof(SelectedPlan));
                        RaisePropertyChanged(nameof(SelectedPlanSum));
                        RaisePropertyChanged(nameof(SelectedStructureSet));
                        RaisePropertyChanged(nameof(SelectedCourse));
                    }
                }
                NotifyContextChanged();
            }
        }

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
            ContextSelection = new ContextSelection { PatientId = patient.Id };
            ClearContextCollections();
            contextStatusText = "Loading courses, plans, and structure sets…";
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
                application.SetContext(ContextSelection);
            }
            RaisePropertyChanged(nameof(ContextSelection));
            RaisePropertyChanged(nameof(ContextStatusText));
            var handler = PatientSelectionChanged;
            if (handler != null) handler(patient);
        }

        public void ClearPatient()
        {
            selectedPatient = null;
            ContextSelection = new ContextSelection();
            ClearContextCollections();
            contextStatusText = string.Empty;
            foreach (var application in Applications)
            {
                application.SetPatient(null);
                application.SetContext(ContextSelection);
            }

            RaisePropertyChanged(nameof(SelectedPatient));
            RaisePropertyChanged(nameof(HasSelectedPatient));
            RaisePropertyChanged(nameof(SelectedPatientDisplay));
            RaisePropertyChanged(nameof(SelectedPatientId));
            RaisePropertyChanged(nameof(ContextSelection));
            RaisePropertyChanged(nameof(ContextStatusText));
        }

        public void SetContextDirectory(ContextDirectory directory)
        {
            contextDirectory = directory ?? new ContextDirectory();
            ClearContextCollections();
            foreach (var item in contextDirectory.Courses) Courses.Add(item);
            foreach (var item in contextDirectory.Plans) Plans.Add(item);
            foreach (var item in contextDirectory.PlanSums) PlanSums.Add(item);
            foreach (var item in contextDirectory.StructureSets) StructureSets.Add(item);
            foreach (var item in contextDirectory.Images) Images.Add(item);
            contextStatusText = Plans.Count + " plans · " + PlanSums.Count + " sums · " + StructureSets.Count + " structure sets · " + Images.Count + " images";
            RaisePropertyChanged(nameof(ContextStatusText));
            NotifyContextChanged();
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
                RefreshRelaunchAvailability(application.Id);
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

            if (selectedArtifactFilter != null && selectedArtifactFilter.Kind != ApplicationArtifactFilter.All)
            {
                var artifactKind = ArtifactKindFor(selectedArtifactFilter.Kind);
                query = query.Where(item => item.ArtifactKind == artifactKind);
            }

            VisibleApplications.Clear();
            foreach (var application in query)
            {
                VisibleApplications.Add(application);
            }
        }

        private void Start(ApplicationCardViewModel card, bool withPatient)
        {
            if (card == null) return;

            if (card.Definition.LaunchKind == LaunchKind.EclipsePlugin)
            {
                notificationText = card.Name + " runs inside Eclipse. Open it from Tools > Scripts.";
                TechnicalLog.Current.Write("INFO", "eclipse_plugin_reference", card.Id, null);
                RaisePropertyChanged(nameof(NotificationText));
                return;
            }

            if (card.Definition.LaunchKind == LaunchKind.EsapiContextScript)
            {
                StartContext(card);
                return;
            }
            Launch(card, withPatient ? LaunchMode.WithPatient : LaunchMode.WithoutPatient,
                withPatient ? selectedPatient : null, null);
        }

        private static ApplicationArtifactKind ArtifactKindFor(ApplicationArtifactFilter filter)
        {
            if (filter == ApplicationArtifactFilter.Standalone) return ApplicationArtifactKind.Standalone;
            if (filter == ApplicationArtifactFilter.SingleFile) return ApplicationArtifactKind.SingleFile;
            if (filter == ApplicationArtifactFilter.Binary) return ApplicationArtifactKind.Binary;
            return ApplicationArtifactKind.Auto;
        }

        private void OpenReadme(ApplicationCardViewModel card)
        {
            if (card == null || !card.HasHubReadme) return;
            try
            {
                Process.Start(new ProcessStartInfo(card.HubReadmeUri.AbsoluteUri) { UseShellExecute = true });
                TechnicalLog.Current.Write("INFO", "hub_readme_opened", card.Id, null);
            }
            catch (Exception exception)
            {
                notificationText = "STR Hub README could not be opened: " + exception.Message;
                TechnicalLog.Current.Write("WARN", "hub_readme_open_failed", card.Id, exception);
                RaisePropertyChanged(nameof(NotificationText));
            }
        }

        private void StartContext(ApplicationCardViewModel card)
        {
            if (card == null) return;
            Launch(card, LaunchMode.Context, selectedPatient, CopySelectionFor(card.Definition.ScopeMode));
        }

        private void Launch(ApplicationCardViewModel card, LaunchMode mode, PatientRecord patient, ContextSelection selection)
        {
            if (card == null) return;
            var protectedSelection = selection;
            if (mode == LaunchMode.WithPatient)
                protectedSelection = new ContextSelection { PatientId = patient == null ? null : patient.Id };

            LaunchHistoryEntry entry;
            try
            {
                entry = new LaunchHistoryEntry
                {
                    HistoryId = Guid.NewGuid().ToString("N"),
                    ApplicationId = card.Id,
                    ApplicationName = card.Name,
                    ArtifactLabel = card.ArtifactLabel,
                    AccessLabel = card.AccessLabel,
                    StartedUtc = DateTime.UtcNow,
                    State = LaunchHistoryState.Starting,
                    LaunchMode = mode,
                    ProtectedContext = mode == LaunchMode.WithoutPatient ? null : contextProtector.Protect(protectedSelection)
                };
            }
            catch (Exception exception)
            {
                notificationText = card.Name + " could not be started because its restart context could not be protected.";
                TechnicalLog.Current.Write("ERROR", "history_context_protect_failed", card.Id, exception);
                RaisePropertyChanged(nameof(NotificationText));
                return;
            }

            var row = new ActivityRowViewModel(entry, DescribeContext(mode, protectedSelection));
            row.SetCanRunAgain(card.IsReady);
            lock (historyGate) historyEntries.Insert(0, entry);
            Activities.Insert(0, row);
            PersistHistory();

            try
            {
                LaunchRequest request;
                if (mode == LaunchMode.Context)
                {
                    request = ContextScriptRequestComposer.Compose(card.Definition, patient, selection,
                        Configuration.Hub, Configuration.Hub.ResolvedScriptHostExecutable);
                }
                else
                {
                    request = ArgumentComposer.Compose(card.Definition, patient, mode == LaunchMode.WithPatient);
                }

                var process = launcher.Start(request);
                entry.State = LaunchHistoryState.Running;
                row.AttachProcess(process);
                row.Refresh();
                Processes.Insert(0, new ProcessRowViewModel(card.Name, process));
                PersistHistory();
                TechnicalLog.Current.Write("INFO", mode == LaunchMode.Context ? "context_child_started" : "child_started", card.Id, null);
                notificationText = mode == LaunchMode.Context
                    ? card.Name + " started with the selected planning context."
                    : card.Name + " started in a separate process.";

                var exitHandled = 0;
                Action handleExit = () =>
                {
                    if (Interlocked.Exchange(ref exitHandled, 1) != 0) return;
                    lock (historyGate)
                    {
                        entry.State = LaunchHistoryState.Exited;
                        entry.FinishedUtc = process.ExitedUtc ?? DateTime.UtcNow;
                        entry.ExitCode = process.ExitCode;
                    }
                    row.Refresh();
                    PersistHistory();
                    TechnicalLog.Current.Write(process.ExitCode.GetValueOrDefault() == 0 ? "INFO" : "WARN",
                        process.ExitCode.GetValueOrDefault() == 0 ? "child_exit_ok" : "child_exit_nonzero", card.Id, null);
                };
                process.Exited += (sender, args) => handleExit();
                if (!process.IsRunning) handleExit();
            }
            catch (Exception exception)
            {
                entry.State = LaunchHistoryState.FailedToStart;
                entry.FinishedUtc = DateTime.UtcNow;
                row.Refresh();
                PersistHistory();
                notificationText = card.Name + " could not be started: " + exception.Message;
                TechnicalLog.Current.Write("ERROR", mode == LaunchMode.Context ? "context_child_start_failed" : "child_start_failed", card.Id, exception);
            }

            RaisePropertyChanged(nameof(NotificationText));
        }

        private void RunAgain(ActivityRowViewModel row)
        {
            if (row == null) return;
            var card = Applications.FirstOrDefault(item => string.Equals(item.Id, row.ApplicationId, StringComparison.OrdinalIgnoreCase));
            if (card == null || !card.IsReady)
            {
                row.Entry.State = LaunchHistoryState.Unavailable;
                row.SetCanRunAgain(false);
                row.Refresh();
                PersistHistory();
                notificationText = row.ApplicationName + " is not available in the current catalogue.";
                RaisePropertyChanged(nameof(NotificationText));
                return;
            }

            try
            {
                ContextSelection selection = null;
                PatientRecord patient = null;
                if (row.Entry.LaunchMode != LaunchMode.WithoutPatient)
                {
                    selection = contextProtector.Unprotect(row.Entry.ProtectedContext);
                    patient = new PatientRecord(selection.PatientId, string.Empty, string.Empty, 0);
                }
                Launch(card, row.Entry.LaunchMode, patient, selection);
            }
            catch (Exception exception)
            {
                row.SetCanRunAgain(false);
                notificationText = row.ApplicationName + " cannot be restarted because its protected context is unavailable.";
                TechnicalLog.Current.Write("WARN", "history_context_unprotect_failed", row.ApplicationId, exception);
                RaisePropertyChanged(nameof(NotificationText));
            }
        }

        private void LoadHistory()
        {
            if (historyStore == null) return;
            var unavailableChanged = false;
            foreach (var entry in historyStore.Load())
            {
                var card = Applications.FirstOrDefault(item => string.Equals(item.Id, entry.ApplicationId, StringComparison.OrdinalIgnoreCase));
                ContextSelection selection = null;
                var protectedContextAvailable = entry.LaunchMode == LaunchMode.WithoutPatient;
                if (!protectedContextAvailable)
                {
                    try
                    {
                        selection = contextProtector.Unprotect(entry.ProtectedContext);
                        protectedContextAvailable = true;
                    }
                    catch (Exception exception)
                    {
                        TechnicalLog.Current.Write("WARN", "history_context_unprotect_failed", entry.ApplicationId, exception);
                    }
                }
                if (card == null)
                {
                    entry.State = LaunchHistoryState.Unavailable;
                    unavailableChanged = true;
                }
                historyEntries.Add(entry);
                var row = new ActivityRowViewModel(entry, DescribeContext(entry.LaunchMode, selection));
                row.SetCanRunAgain(card != null && card.IsReady && protectedContextAvailable);
                Activities.Add(row);
            }
            if (unavailableChanged) PersistHistory();
        }

        private void RefreshRelaunchAvailability(string applicationId)
        {
            var card = Applications.FirstOrDefault(item => string.Equals(item.Id, applicationId, StringComparison.OrdinalIgnoreCase));
            foreach (var row in Activities.Where(item => string.Equals(item.ApplicationId, applicationId, StringComparison.OrdinalIgnoreCase)))
            {
                var protectedContextAvailable = row.Entry.LaunchMode == LaunchMode.WithoutPatient || !string.IsNullOrWhiteSpace(row.Entry.ProtectedContext);
                row.SetCanRunAgain(card != null && card.IsReady && protectedContextAvailable);
            }
        }

        private void PersistHistory()
        {
            if (historyStore == null) return;
            List<LaunchHistoryEntry> snapshot;
            lock (historyGate) snapshot = historyEntries.ToList();
            historyStore.Save(snapshot);
        }

        private static string DescribeContext(LaunchMode mode, ContextSelection selection)
        {
            if (mode == LaunchMode.WithoutPatient) return "No patient";
            if (selection == null) return "Protected context unavailable";
            var values = new List<string>();
            if (!string.IsNullOrWhiteSpace(selection.PatientId)) values.Add("Patient " + selection.PatientId);
            if (!string.IsNullOrWhiteSpace(selection.CourseId)) values.Add("Course " + selection.CourseId);
            if (!string.IsNullOrWhiteSpace(selection.PlanId)) values.Add("Plan " + selection.PlanId);
            if (!string.IsNullOrWhiteSpace(selection.PlanSumId)) values.Add("Plan sum " + selection.PlanSumId);
            if (!string.IsNullOrWhiteSpace(selection.StructureSetId)) values.Add("SS " + selection.StructureSetId);
            if (!string.IsNullOrWhiteSpace(selection.ImageId)) values.Add("Image " + selection.ImageId);
            return values.Count == 0 ? "No context" : string.Join(" · ", values);
        }

        private static LaunchHistoryStore CreateDefaultHistoryStore(HubConfiguration configuration)
        {
            if (configuration == null || string.IsNullOrWhiteSpace(configuration.Hub.ResolvedHistoryFile)) return null;
            return new LaunchHistoryStore(configuration.Hub.ResolvedHistoryFile,
                configuration.Hub.HistoryRetentionDays, configuration.Hub.HistoryMaxEntries);
        }

        private ContextSelection CopySelectionFor(ScopeMode scopeMode)
        {
            var copy = new ContextSelection
            {
                PatientId = ContextSelection.PatientId, CourseId = ContextSelection.CourseId,
                PlanId = ContextSelection.PlanId, PlanSumId = ContextSelection.PlanSumId,
                StructureSetId = ContextSelection.StructureSetId, ImageId = ContextSelection.ImageId
            };
            if (scopeMode == ScopeMode.Multiple)
            {
                foreach (var plan in Plans) copy.PlanIdsInScope.Add(plan.Id);
                foreach (var planSum in PlanSums) copy.PlanSumIdsInScope.Add(planSum.Id);
            }
            else if (scopeMode == ScopeMode.Single)
            {
                if (!string.IsNullOrWhiteSpace(copy.PlanId)) copy.PlanIdsInScope.Add(copy.PlanId);
                if (!string.IsNullOrWhiteSpace(copy.PlanSumId)) copy.PlanSumIdsInScope.Add(copy.PlanSumId);
            }
            return copy;
        }

        private void ClearContextCollections()
        {
            Courses.Clear(); Plans.Clear(); PlanSums.Clear(); StructureSets.Clear(); Images.Clear();
            selectedCourse = null; selectedPlan = null; selectedPlanSum = null; selectedStructureSet = null; selectedImage = null;
            RaisePropertyChanged(nameof(SelectedCourse));
            RaisePropertyChanged(nameof(SelectedPlan));
            RaisePropertyChanged(nameof(SelectedPlanSum));
            RaisePropertyChanged(nameof(SelectedStructureSet));
            RaisePropertyChanged(nameof(SelectedImage));
        }

        private void NotifyContextChanged()
        {
            foreach (var application in Applications) application.SetContext(ContextSelection);
        }
    }
}
