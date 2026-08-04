using System;
using System.Windows;
using EsapiRunnerHub.History;
using EsapiRunnerHub.Infrastructure;
using EsapiRunnerHub.Launching;

namespace EsapiRunnerHub.ViewModels
{
    public sealed class ActivityRowViewModel : ObservableObject
    {
        private bool canRunAgain;
        private int? processId;

        public ActivityRowViewModel(LaunchHistoryEntry entry, string contextSummary)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            ContextSummary = contextSummary ?? string.Empty;
        }

        public LaunchHistoryEntry Entry { get; private set; }
        public string ApplicationId { get { return Entry.ApplicationId; } }
        public string ApplicationName { get { return Entry.ApplicationName; } }
        public string ArtifactLabel { get { return Entry.ArtifactLabel; } }
        public string AccessLabel { get { return Entry.AccessLabel; } }
        public string ContextSummary { get; private set; }
        public LaunchHistoryState State { get { return Entry.State; } }
        public int? ExitCode { get { return Entry.ExitCode; } }
        public string ProcessId { get { return processId.HasValue ? processId.Value.ToString() : "—"; } }
        public string Started { get { return Entry.StartedUtc.ToLocalTime().ToString("dd.MM. HH:mm:ss"); } }
        public string Status
        {
            get
            {
                if (Entry.State == LaunchHistoryState.Exited)
                    return Entry.ExitCode.GetValueOrDefault() == 0 ? "Completed" : "Exited · code " + Entry.ExitCode.GetValueOrDefault();
                if (Entry.State == LaunchHistoryState.FailedToStart) return "Failed to start";
                if (Entry.State == LaunchHistoryState.Unavailable) return "Unavailable";
                return Entry.State.ToString();
            }
        }
        public bool CanRunAgain { get { return canRunAgain; } }

        public void AttachProcess(RunningProcessInfo process)
        {
            processId = process == null ? (int?)null : process.ProcessId;
            RaiseOnUi(nameof(ProcessId));
        }

        public void SetCanRunAgain(bool value)
        {
            if (canRunAgain == value) return;
            canRunAgain = value;
            RaiseOnUi(nameof(CanRunAgain));
        }

        public void Refresh()
        {
            RaiseOnUi(nameof(State));
            RaiseOnUi(nameof(ExitCode));
            RaiseOnUi(nameof(Status));
        }

        private void RaiseOnUi(string propertyName)
        {
            Action update = () => RaisePropertyChanged(propertyName);
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
                Application.Current.Dispatcher.BeginInvoke(update);
            else
                update();
        }
    }
}
