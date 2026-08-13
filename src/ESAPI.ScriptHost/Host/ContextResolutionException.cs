using System;

namespace EsapiScriptHost.Host
{
    public sealed class ContextResolutionException : InvalidOperationException
    {
        public const string ReasonCodeDataKey = "ESAPI.RunnerHub.SafeResolutionCode";

        public ContextResolutionException(string reasonCode, string message)
            : base(message)
        {
            ReasonCode = string.IsNullOrWhiteSpace(reasonCode) ? "context_resolution_failed" : reasonCode;
            Data[ReasonCodeDataKey] = ReasonCode;
        }

        public string ReasonCode { get; private set; }
    }
}
