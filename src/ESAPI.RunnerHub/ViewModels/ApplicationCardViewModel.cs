using System.Windows;
using EsapiRunnerHub.Configuration;
using EsapiRunnerHub.Infrastructure;
using EsapiRunnerHub.Launching;
using EsapiRunnerHub.Patients;

namespace EsapiRunnerHub.ViewModels
{
    public sealed class ApplicationCardViewModel : ObservableObject
    {
        private PathReadiness readiness;
        private string statusText;
        private PatientRecord selectedPatient;

        public ApplicationCardViewModel(ApplicationDefinition definition)
        {
            Definition = definition;
            readiness = PathReadiness.Unavailable;
            statusText = "Path not checked";
        }

        public ApplicationDefinition Definition { get; private set; }

        public string Id { get { return Definition.Id; } }
        public string Name { get { return Definition.Name; } }
        public string Category { get { return string.IsNullOrWhiteSpace(Definition.Category) ? "Other" : Definition.Category; } }
        public string Description { get { return Definition.Description; } }
        public string ModeLabel
        {
            get
            {
                if (Definition.PatientMode == PatientMode.Required) return "Patient required";
                if (Definition.PatientMode == PatientMode.Optional && Definition.PatientTransport != PatientTransport.None) return "Patient optional";
                return "No patient transfer";
            }
        }

        public string StatusText { get { return statusText; } }
        public bool IsReady { get { return readiness == PathReadiness.Ready; } }
        public bool CanStartWithoutPatient { get { return IsReady && Definition.PatientMode != PatientMode.Required; } }
        public bool CanStartWithPatient
        {
            get
            {
                return IsReady && selectedPatient != null && Definition.PatientMode != PatientMode.None &&
                       Definition.PatientTransport != PatientTransport.None;
            }
        }

        public Visibility WithPatientVisibility
        {
            get { return Definition.PatientMode == PatientMode.None || Definition.PatientTransport == PatientTransport.None ? Visibility.Collapsed : Visibility.Visible; }
        }

        public Visibility WithoutPatientVisibility
        {
            get { return Definition.PatientMode == PatientMode.Required ? Visibility.Collapsed : Visibility.Visible; }
        }

        public string WithPatientLabel
        {
            get { return selectedPatient == null ? "Select patient" : "Start with " + selectedPatient.Id; }
        }

        public string WithoutPatientLabel
        {
            get { return Definition.PatientMode == PatientMode.None ? "Start" : "Start without patient"; }
        }

        public void SetReadiness(PathReadiness value, string message)
        {
            readiness = value;
            statusText = message;
            RaiseAllState();
        }

        public void SetPatient(PatientRecord patient)
        {
            selectedPatient = patient;
            RaiseAllState();
        }

        private void RaiseAllState()
        {
            RaisePropertyChanged(nameof(StatusText));
            RaisePropertyChanged(nameof(IsReady));
            RaisePropertyChanged(nameof(CanStartWithoutPatient));
            RaisePropertyChanged(nameof(CanStartWithPatient));
            RaisePropertyChanged(nameof(WithPatientLabel));
        }
    }
}
