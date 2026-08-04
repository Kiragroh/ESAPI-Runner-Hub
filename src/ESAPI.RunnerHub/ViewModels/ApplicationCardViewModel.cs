using System.Windows;
using EsapiRunnerHub.Configuration;
using EsapiRunnerHub.Infrastructure;
using EsapiRunnerHub.Launching;
using EsapiRunnerHub.Patients;
using EsapiRunnerHub.Context;
using EsapiRunnerHub.Catalog;

namespace EsapiRunnerHub.ViewModels
{
    public sealed class ApplicationCardViewModel : ObservableObject
    {
        private PathReadiness readiness;
        private string statusText;
        private PatientRecord selectedPatient;
        private ContextSelection contextSelection;

        public ApplicationCardViewModel(ApplicationDefinition definition)
            : this(definition, string.Empty)
        {
        }

        public ApplicationCardViewModel(ApplicationDefinition definition, string strHubBaseUrl)
        {
            Definition = definition;
            readiness = PathReadiness.Unavailable;
            statusText = "Path not checked";
            ArtifactKind = ApplicationMetadata.ArtifactFor(definition);
            AccessMode = ApplicationMetadata.AccessFor(definition);
            ArtifactLabel = ApplicationMetadata.ArtifactLabel(ArtifactKind);
            AccessLabel = ApplicationMetadata.AccessLabel(AccessMode);
            CompactPath = ApplicationMetadata.CompactPath(string.IsNullOrWhiteSpace(definition.ResolvedExecutable)
                ? definition.Executable
                : definition.ResolvedExecutable);
            HubReadmeUri = ApplicationMetadata.BuildReadmeUri(strHubBaseUrl, definition.HubScriptId);
        }

        public ApplicationDefinition Definition { get; private set; }

        public string Id { get { return Definition.Id; } }
        public string Name { get { return Definition.Name; } }
        public string Category { get { return string.IsNullOrWhiteSpace(Definition.Category) ? "Other" : Definition.Category; } }
        public string Description { get { return Definition.Description; } }
        public ApplicationArtifactKind ArtifactKind { get; private set; }
        public ApplicationAccessMode AccessMode { get; private set; }
        public string ArtifactLabel { get; private set; }
        public string AccessLabel { get; private set; }
        public string CompactPath { get; private set; }
        public System.Uri HubReadmeUri { get; private set; }
        public bool HasHubReadme { get { return HubReadmeUri != null; } }
        public string ModeLabel
        {
            get
            {
                if (Definition.LaunchKind == LaunchKind.EclipsePlugin) return "Eclipse plug-in";
                if (Definition.LaunchKind == LaunchKind.EsapiContextScript)
                    return Definition.WriteMode == WriteMode.ConfirmSave ? "Context script · save confirmation" : "Context script · read-only";
                if (Definition.PatientMode == PatientMode.Required) return "Patient required";
                if (Definition.PatientMode == PatientMode.Optional && Definition.PatientTransport != PatientTransport.None) return "Patient optional";
                return "No patient transfer";
            }
        }

        public string StatusText { get { return statusText; } }
        public bool IsReady { get { return readiness == PathReadiness.Ready; } }
        public bool CanStartWithoutPatient { get { return Definition.LaunchKind == LaunchKind.Executable && IsReady && Definition.PatientMode != PatientMode.Required; } }
        public bool CanStartWithPatient
        {
            get
            {
                return Definition.LaunchKind == LaunchKind.Executable && IsReady && selectedPatient != null && Definition.PatientMode != PatientMode.None &&
                       Definition.PatientTransport != PatientTransport.None;
            }
        }

        public bool CanStartContext
        {
            get
            {
                return Definition.LaunchKind == LaunchKind.EsapiContextScript && IsReady && contextSelection != null &&
                       string.IsNullOrWhiteSpace(contextSelection.MissingFor(Definition.ContextRequirement));
            }
        }

        public string ContextHint
        {
            get
            {
                if (Definition.LaunchKind != LaunchKind.EsapiContextScript) return string.Empty;
                return contextSelection == null ? "Patient required" : contextSelection.MissingFor(Definition.ContextRequirement);
            }
        }

        public string ContextActionLabel
        {
            get
            {
                if (contextSelection == null) return "Select context";
                if (!string.IsNullOrWhiteSpace(contextSelection.PlanId)) return "Start with plan";
                if (!string.IsNullOrWhiteSpace(contextSelection.PlanSumId)) return "Start with plan sum";
                if (!string.IsNullOrWhiteSpace(contextSelection.StructureSetId)) return "Start with structure set";
                if (!string.IsNullOrWhiteSpace(contextSelection.PatientId)) return "Start with patient";
                return "Start without patient";
            }
        }

        public Visibility WithPatientVisibility
        {
            get { return Definition.LaunchKind == LaunchKind.EclipsePlugin || Definition.PatientMode == PatientMode.None || Definition.PatientTransport == PatientTransport.None ? Visibility.Collapsed : Visibility.Visible; }
        }

        public Visibility WithoutPatientVisibility
        {
            get { return Definition.LaunchKind == LaunchKind.EclipsePlugin || Definition.PatientMode == PatientMode.Required ? Visibility.Collapsed : Visibility.Visible; }
        }

        public Visibility ReferenceVisibility { get { return Definition.LaunchKind == LaunchKind.EclipsePlugin ? Visibility.Visible : Visibility.Collapsed; } }
        public Visibility ContextVisibility { get { return Definition.LaunchKind == LaunchKind.EsapiContextScript ? Visibility.Visible : Visibility.Collapsed; } }
        public string ReferenceHint { get { return "Available in Eclipse · Tools > Scripts"; } }

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

        public void SetContext(ContextSelection selection)
        {
            contextSelection = selection;
            RaiseAllState();
        }

        private void RaiseAllState()
        {
            RaisePropertyChanged(nameof(StatusText));
            RaisePropertyChanged(nameof(IsReady));
            RaisePropertyChanged(nameof(CanStartWithoutPatient));
            RaisePropertyChanged(nameof(CanStartWithPatient));
            RaisePropertyChanged(nameof(WithPatientLabel));
            RaisePropertyChanged(nameof(CanStartContext));
            RaisePropertyChanged(nameof(ContextHint));
            RaisePropertyChanged(nameof(ContextActionLabel));
        }
    }
}
