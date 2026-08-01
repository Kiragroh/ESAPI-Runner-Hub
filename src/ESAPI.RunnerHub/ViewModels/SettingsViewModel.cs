using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using EsapiRunnerHub.Configuration;
using EsapiRunnerHub.Infrastructure;

namespace EsapiRunnerHub.ViewModels
{
    public sealed class SettingsViewModel : ObservableObject
    {
        private readonly HubConfiguration workingConfiguration;
        private ApplicationDefinition selectedApplication;
        private string settingsPath;
        private string validationSummary;

        public SettingsViewModel(HubConfiguration source, string path)
        {
            settingsPath = Path.GetFullPath(path);
            var serialized = IniConfigurationStore.Serialize(source ?? new HubConfiguration());
            workingConfiguration = IniConfigurationStore.ParseText(serialized, settingsPath);
            Applications = new ObservableCollection<ApplicationDefinition>(workingConfiguration.Applications);
            PatientModes = Enum.GetValues(typeof(PatientMode)).Cast<PatientMode>().ToList();
            PatientTransports = Enum.GetValues(typeof(PatientTransport)).Cast<PatientTransport>().ToList();
            SelectedApplication = Applications.FirstOrDefault();
            UpdateValidation();
        }

        public ObservableCollection<ApplicationDefinition> Applications { get; private set; }
        public IList<PatientMode> PatientModes { get; private set; }
        public IList<PatientTransport> PatientTransports { get; private set; }
        public HubConfiguration WorkingConfiguration { get { return workingConfiguration; } }

        public string SettingsPath
        {
            get { return settingsPath; }
            set
            {
                if (!string.IsNullOrWhiteSpace(value) && SetProperty(ref settingsPath, Path.GetFullPath(value)))
                {
                    UpdateValidation();
                }
            }
        }

        public string EsapiApiAssembly
        {
            get { return workingConfiguration.Hub.EsapiApiAssembly; }
            set { workingConfiguration.Hub.EsapiApiAssembly = value; RaisePropertyChanged(); }
        }

        public string EsapiTypesAssembly
        {
            get { return workingConfiguration.Hub.EsapiTypesAssembly; }
            set { workingConfiguration.Hub.EsapiTypesAssembly = value; RaisePropertyChanged(); }
        }

        public int SearchMaxResults
        {
            get { return workingConfiguration.Hub.SearchMaxResults; }
            set { workingConfiguration.Hub.SearchMaxResults = value; RaisePropertyChanged(); UpdateValidation(); }
        }

        public int SearchDebounceMs
        {
            get { return workingConfiguration.Hub.SearchDebounceMs; }
            set { workingConfiguration.Hub.SearchDebounceMs = value; RaisePropertyChanged(); UpdateValidation(); }
        }

        public int PathProbeTimeoutMs
        {
            get { return workingConfiguration.Hub.PathProbeTimeoutMs; }
            set { workingConfiguration.Hub.PathProbeTimeoutMs = value; RaisePropertyChanged(); UpdateValidation(); }
        }

        public string LogDirectory
        {
            get { return workingConfiguration.Hub.LogDirectory; }
            set { workingConfiguration.Hub.LogDirectory = value; RaisePropertyChanged(); }
        }

        public ApplicationDefinition SelectedApplication
        {
            get { return selectedApplication; }
            set { SetProperty(ref selectedApplication, value); }
        }

        public string ValidationSummary { get { return validationSummary; } }

        public void AddApplication()
        {
            var number = 1;
            string id;
            do
            {
                id = "tool-" + number++;
            }
            while (Applications.Any(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase)));

            var application = new ApplicationDefinition
            {
                Id = id,
                Name = "New application",
                Category = "Other",
                Description = "Configured executable",
                Enabled = true,
                PatientMode = PatientMode.None,
                PatientTransport = PatientTransport.None,
                SortOrder = Applications.Count == 0 ? 10 : Applications.Max(item => item.SortOrder) + 10
            };
            Applications.Add(application);
            SelectedApplication = application;
            UpdateValidation();
        }

        public void DeleteSelectedApplication()
        {
            if (SelectedApplication == null)
            {
                return;
            }

            var index = Applications.IndexOf(SelectedApplication);
            Applications.Remove(SelectedApplication);
            SelectedApplication = Applications.Count == 0 ? null : Applications[Math.Min(index, Applications.Count - 1)];
            UpdateValidation();
        }

        public void AssignApiAssembly(string path)
        {
            EsapiApiAssembly = path;
        }

        public void AssignTypesAssembly(string path)
        {
            EsapiTypesAssembly = path;
        }

        public void AssignExecutable(string path)
        {
            if (SelectedApplication != null)
            {
                SelectedApplication.Executable = path;
                if (string.IsNullOrWhiteSpace(SelectedApplication.WorkingDirectory) && !string.IsNullOrWhiteSpace(path))
                {
                    SelectedApplication.WorkingDirectory = Path.GetDirectoryName(path);
                }
                RaisePropertyChanged(nameof(SelectedApplication));
                UpdateValidation();
            }
        }

        public void AssignWorkingDirectory(string path)
        {
            if (SelectedApplication != null)
            {
                SelectedApplication.WorkingDirectory = path;
                RaisePropertyChanged(nameof(SelectedApplication));
            }
        }

        public bool TrySave(out string error)
        {
            error = string.Empty;
            try
            {
                SyncApplications();
                workingConfiguration.SourcePath = SettingsPath;
                workingConfiguration.ResolvePaths();
                var validation = workingConfiguration.Validate();
                if (!validation.IsValid)
                {
                    error = string.Join(Environment.NewLine, validation.Errors);
                    validationSummary = error;
                    RaisePropertyChanged(nameof(ValidationSummary));
                    return false;
                }

                IniConfigurationStore.SaveAtomic(workingConfiguration, SettingsPath);
                var reloaded = IniConfigurationStore.Load(SettingsPath);
                if (!reloaded.Validate().IsValid)
                {
                    error = "Saved settings could not be validated after reload.";
                    return false;
                }

                validationSummary = "Settings saved and reloaded successfully.";
                RaisePropertyChanged(nameof(ValidationSummary));
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                validationSummary = error;
                RaisePropertyChanged(nameof(ValidationSummary));
                return false;
            }
        }

        public void UpdateValidation()
        {
            SyncApplications();
            var result = workingConfiguration.Validate();
            validationSummary = result.IsValid ? "Configuration is valid." : string.Join(Environment.NewLine, result.Errors);
            RaisePropertyChanged(nameof(ValidationSummary));
        }

        private void SyncApplications()
        {
            workingConfiguration.Applications.Clear();
            foreach (var application in Applications)
            {
                workingConfiguration.Applications.Add(application);
            }
        }
    }
}
