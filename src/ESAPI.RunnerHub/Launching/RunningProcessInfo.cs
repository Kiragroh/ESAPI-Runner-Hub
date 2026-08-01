using System;
using System.Diagnostics;

namespace EsapiRunnerHub.Launching
{
    public sealed class RunningProcessInfo
    {
        private readonly Process process;

        internal RunningProcessInfo(string applicationId, Process process)
        {
            ApplicationId = applicationId;
            this.process = process;
            ProcessId = process.Id;
            StartedUtc = DateTime.UtcNow;
        }

        public event EventHandler Exited;

        public string ApplicationId { get; private set; }

        public int ProcessId { get; private set; }

        public DateTime StartedUtc { get; private set; }

        public DateTime? ExitedUtc { get; private set; }

        public int? ExitCode { get; private set; }

        public bool IsRunning { get { return !ExitCode.HasValue; } }

        internal void MarkExited()
        {
            if (ExitCode.HasValue)
            {
                return;
            }

            try
            {
                ExitCode = process.ExitCode;
                ExitedUtc = DateTime.UtcNow;
            }
            catch (InvalidOperationException)
            {
                return;
            }

            var handler = Exited;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        public bool WaitForExit(int milliseconds)
        {
            if (!process.WaitForExit(milliseconds))
            {
                return false;
            }

            MarkExited();
            return true;
        }
    }
}
