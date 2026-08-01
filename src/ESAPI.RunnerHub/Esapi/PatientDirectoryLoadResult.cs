using System.Collections.Generic;
using EsapiRunnerHub.Patients;

namespace EsapiRunnerHub.Esapi
{
    public sealed class PatientDirectoryLoadResult
    {
        private PatientDirectoryLoadResult(bool isAvailable, IList<PatientRecord> patients, string errorCode, string errorMessage)
        {
            IsAvailable = isAvailable;
            Patients = patients;
            ErrorCode = errorCode ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public bool IsAvailable { get; private set; }

        public IList<PatientRecord> Patients { get; private set; }

        public string ErrorCode { get; private set; }

        public string ErrorMessage { get; private set; }

        public static PatientDirectoryLoadResult Available(IList<PatientRecord> patients)
        {
            return new PatientDirectoryLoadResult(true, patients ?? new List<PatientRecord>(), string.Empty, string.Empty);
        }

        public static PatientDirectoryLoadResult Offline(string errorCode, string errorMessage)
        {
            return new PatientDirectoryLoadResult(false, new List<PatientRecord>(), errorCode, errorMessage);
        }
    }
}
