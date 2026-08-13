using System;
using System.Collections.Generic;
using EsapiRunnerHub.Patients;

namespace EsapiRunnerHub.Esapi
{
    public sealed class PatientDirectoryLoadResult
    {
        private PatientDirectoryLoadResult(bool isAvailable, IList<PatientRecord> patients, string errorCode, string errorMessage, Exception exception)
        {
            IsAvailable = isAvailable;
            Patients = patients;
            ErrorCode = errorCode ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
            Exception = exception;
        }

        public bool IsAvailable { get; private set; }

        public IList<PatientRecord> Patients { get; private set; }

        public string ErrorCode { get; private set; }

        public string ErrorMessage { get; private set; }

        public Exception Exception { get; private set; }

        public static PatientDirectoryLoadResult Available(IList<PatientRecord> patients)
        {
            return new PatientDirectoryLoadResult(true, patients ?? new List<PatientRecord>(), string.Empty, string.Empty, null);
        }

        public static PatientDirectoryLoadResult Offline(string errorCode, string errorMessage)
        {
            return Offline(errorCode, errorMessage, null);
        }

        public static PatientDirectoryLoadResult Offline(string errorCode, string errorMessage, Exception exception)
        {
            return new PatientDirectoryLoadResult(false, new List<PatientRecord>(), errorCode, errorMessage, exception);
        }
    }
}
