using System;
using System.Windows;
using EsapiRunnerHub.Infrastructure;
using EsapiRunnerHub.Launching;

namespace EsapiRunnerHub.ViewModels
{
    public sealed class ProcessRowViewModel : ObservableObject
    {
        private readonly RunningProcessInfo process;
        private string status;

        public ProcessRowViewModel(string applicationName, RunningProcessInfo process)
        {
            ApplicationName = applicationName;
            this.process = process;
            status = "Running";
            process.Exited += ProcessExited;
        }

        public string ApplicationName { get; private set; }
        public int ProcessId { get { return process.ProcessId; } }
        public string Started { get { return process.StartedUtc.ToLocalTime().ToString("HH:mm:ss"); } }
        public string Status { get { return status; } }

        private void ProcessExited(object sender, EventArgs e)
        {
            Action update = () =>
            {
                status = process.ExitCode.GetValueOrDefault() == 0
                    ? "Completed"
                    : "Ended · Exit " + process.ExitCode.GetValueOrDefault();
                RaisePropertyChanged(nameof(Status));
            };

            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(update);
            }
            else
            {
                update();
            }
        }
    }
}
