using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using EsapiRunnerHub.Context;

namespace EsapiRunnerHub.History
{
    public sealed class ProtectedContextEnvelope
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ESAPI Runner Hub launch context v1");

        public string Protect(ContextSelection selection)
        {
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            var dto = ContextDto.From(selection);
            var clearBytes = Serialize(dto);
            try
            {
                return Convert.ToBase64String(ProtectedData.Protect(clearBytes, Entropy, DataProtectionScope.CurrentUser));
            }
            finally
            {
                Array.Clear(clearBytes, 0, clearBytes.Length);
            }
        }

        public ContextSelection Unprotect(string protectedValue)
        {
            if (string.IsNullOrWhiteSpace(protectedValue))
                throw new InvalidOperationException("The protected launch context is missing.");
            byte[] protectedBytes;
            try
            {
                protectedBytes = Convert.FromBase64String(protectedValue);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException("The protected launch context is invalid.", exception);
            }

            var clearBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            try
            {
                return Deserialize(clearBytes).ToSelection();
            }
            finally
            {
                Array.Clear(clearBytes, 0, clearBytes.Length);
            }
        }

        private static byte[] Serialize(ContextDto dto)
        {
            var serializer = new DataContractJsonSerializer(typeof(ContextDto));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, dto);
                return stream.ToArray();
            }
        }

        private static ContextDto Deserialize(byte[] bytes)
        {
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(ContextDto));
                using (var stream = new MemoryStream(bytes))
                {
                    var dto = serializer.ReadObject(stream) as ContextDto;
                    if (dto == null) throw new SerializationException("No context payload was found.");
                    return dto;
                }
            }
            catch (SerializationException exception)
            {
                throw new InvalidOperationException("The protected launch context could not be read.", exception);
            }
        }

        [DataContract]
        private sealed class ContextDto
        {
            [DataMember(Order = 1, EmitDefaultValue = false)] public string PatientId { get; set; }
            [DataMember(Order = 2, EmitDefaultValue = false)] public string CourseId { get; set; }
            [DataMember(Order = 3, EmitDefaultValue = false)] public string PlanId { get; set; }
            [DataMember(Order = 4, EmitDefaultValue = false)] public string PlanSumId { get; set; }
            [DataMember(Order = 5, EmitDefaultValue = false)] public string StructureSetId { get; set; }
            [DataMember(Order = 6, EmitDefaultValue = false)] public string ImageId { get; set; }
            [DataMember(Order = 7)] public List<string> PlanIdsInScope { get; set; }
            [DataMember(Order = 8)] public List<string> PlanSumIdsInScope { get; set; }

            public static ContextDto From(ContextSelection selection)
            {
                return new ContextDto
                {
                    PatientId = selection.PatientId,
                    CourseId = selection.CourseId,
                    PlanId = selection.PlanId,
                    PlanSumId = selection.PlanSumId,
                    StructureSetId = selection.StructureSetId,
                    ImageId = selection.ImageId,
                    PlanIdsInScope = new List<string>(selection.PlanIdsInScope),
                    PlanSumIdsInScope = new List<string>(selection.PlanSumIdsInScope)
                };
            }

            public ContextSelection ToSelection()
            {
                var selection = new ContextSelection
                {
                    PatientId = PatientId,
                    CourseId = CourseId,
                    PlanId = PlanId,
                    PlanSumId = PlanSumId,
                    StructureSetId = StructureSetId,
                    ImageId = ImageId
                };
                foreach (var id in PlanIdsInScope ?? new List<string>()) selection.PlanIdsInScope.Add(id);
                foreach (var id in PlanSumIdsInScope ?? new List<string>()) selection.PlanSumIdsInScope.Add(id);
                return selection;
            }
        }
    }
}
