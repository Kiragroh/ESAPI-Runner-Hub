using System;
using System.Collections.Generic;
using System.IO;

namespace VMS.TPS.Common.Model.API
{
    public sealed class Application : IDisposable
    {
        private Application()
        {
            PatientSummaries = new List<PatientSummary>
            {
                new PatientSummary { Id = "SYN-1001", FirstName = "Ada", LastName = "Example" },
                new PatientSummary { Id = "SYN-1002", FirstName = "Linus", LastName = "Sample" }
            };
        }

        public IList<PatientSummary> PatientSummaries { get; private set; }

        public static Application CreateApplication()
        {
            return new Application();
        }

        public void Dispose()
        {
            var marker = Environment.GetEnvironmentVariable("FAKE_VMS_DISPOSE_MARKER");
            if (!string.IsNullOrWhiteSpace(marker))
            {
                File.WriteAllText(marker, "disposed");
            }
        }
    }

    public sealed class PatientSummary
    {
        public string Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }
}
