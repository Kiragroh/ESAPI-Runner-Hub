using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using EsapiRunnerHub.Configuration;

namespace EsapiRunnerHub.Launching
{
    [DataContract]
    public sealed class ContextLaunchPayload
    {
        public const string EnvironmentKey = "ESAPI_RUNNER_CONTEXT";
        private const int MaximumEncodedLength = 131072;

        public ContextLaunchPayload()
        {
            PlanIdsInScope = new List<string>();
            PlanSumIdsInScope = new List<string>();
            ExtraReferencePaths = new List<string>();
        }

        [DataMember] public string LaunchToken { get; set; }
        [DataMember] public string ApplicationId { get; set; }
        [DataMember] public string ScriptPath { get; set; }
        [DataMember] public string ApiAssemblyPath { get; set; }
        [DataMember] public string TypesAssemblyPath { get; set; }
        [DataMember] public ScriptEngine ScriptEngine { get; set; }
        [DataMember] public WriteMode WriteMode { get; set; }
        [DataMember] public string EntryType { get; set; }
        [DataMember] public string PatientId { get; set; }
        [DataMember] public string CourseId { get; set; }
        [DataMember] public string PlanId { get; set; }
        [DataMember] public string PlanSumId { get; set; }
        [DataMember] public string StructureSetId { get; set; }
        [DataMember] public string ImageId { get; set; }
        [DataMember] public List<string> PlanIdsInScope { get; set; }
        [DataMember] public List<string> PlanSumIdsInScope { get; set; }
        [DataMember] public List<string> ExtraReferencePaths { get; set; }

        public string Encode()
        {
            var serializer = new DataContractJsonSerializer(typeof(ContextLaunchPayload));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, this);
                return Convert.ToBase64String(stream.ToArray());
            }
        }

        public static ContextLaunchPayload Decode(string encoded)
        {
            if (string.IsNullOrWhiteSpace(encoded) || encoded.Length > MaximumEncodedLength)
                throw new InvalidOperationException("The context launch payload is missing or too large.");

            ContextLaunchPayload payload;
            try
            {
                var bytes = Convert.FromBase64String(encoded);
                var serializer = new DataContractJsonSerializer(typeof(ContextLaunchPayload));
                using (var stream = new MemoryStream(bytes))
                {
                    payload = serializer.ReadObject(stream) as ContextLaunchPayload;
                }
            }
            catch (Exception exception) when (exception is FormatException || exception is SerializationException)
            {
                throw new InvalidOperationException("The context launch payload is invalid.", exception);
            }

            Guid token;
            if (payload == null || !Guid.TryParseExact(payload.LaunchToken, "N", out token) ||
                string.IsNullOrWhiteSpace(payload.ApplicationId))
                throw new InvalidOperationException("The context launch payload has an invalid token.");

            payload.PlanIdsInScope = payload.PlanIdsInScope ?? new List<string>();
            payload.PlanSumIdsInScope = payload.PlanSumIdsInScope ?? new List<string>();
            payload.ExtraReferencePaths = payload.ExtraReferencePaths ?? new List<string>();
            return payload;
        }
    }
}
