using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using EsapiRunnerHub.Context;

namespace EsapiRunnerHub.History
{
    public sealed class ProtectedContextEnvelope
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ESAPI Runner Hub launch context v1");
        private const string RoamingPrefix = "dpapi-ng:v1:";

        public static bool IsRoaming(string protectedValue)
        {
            return protectedValue != null && protectedValue.StartsWith(RoamingPrefix, StringComparison.Ordinal);
        }

        public string Protect(ContextSelection selection)
        {
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            var dto = ContextDto.From(selection);
            var clearBytes = Serialize(dto);
            try
            {
                // Protect to this account, never to the machine or a broader group.
                // Citrix profile-local DPAPI master keys do not necessarily roam.
                return RoamingPrefix + Convert.ToBase64String(RoamingProtection.Protect(clearBytes));
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
                protectedBytes = Convert.FromBase64String(IsRoaming(protectedValue)
                    ? protectedValue.Substring(RoamingPrefix.Length) : protectedValue);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException("The protected launch context is invalid.", exception);
            }

            // Existing opaque DPAPI entries remain readable on their original profile.
            var clearBytes = IsRoaming(protectedValue) ? RoamingProtection.Unprotect(protectedBytes)
                : ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            try
            {
                return Deserialize(clearBytes).ToSelection();
            }
            finally
            {
                Array.Clear(clearBytes, 0, clearBytes.Length);
            }
        }

        private static class RoamingProtection
        {
            private const int Silent = 0x40;

            public static byte[] Protect(byte[] data)
            {
                IntPtr descriptor = IntPtr.Zero, output = IntPtr.Zero;
                int length;
                try
                {
                    using (var identity = WindowsIdentity.GetCurrent())
                    {
                        if (identity.User == null) throw new CryptographicException("Current account is unavailable.");
                        Check(NCryptCreateProtectionDescriptor("SID=" + identity.User.Value, 0, out descriptor));
                    }
                    Check(NCryptProtectSecret(descriptor, Silent, data, data.Length,
                        IntPtr.Zero, IntPtr.Zero, out output, out length));
                    return Copy(output, length);
                }
                finally
                {
                    if (output != IntPtr.Zero) LocalFree(output);
                    if (descriptor != IntPtr.Zero) NCryptCloseProtectionDescriptor(descriptor);
                }
            }

            public static byte[] Unprotect(byte[] data)
            {
                IntPtr output = IntPtr.Zero;
                int length = 0;
                try
                {
                    Check(NCryptUnprotectSecret(IntPtr.Zero, Silent, data, data.Length,
                        IntPtr.Zero, IntPtr.Zero, out output, out length));
                    return Copy(output, length);
                }
                finally
                {
                    if (output != IntPtr.Zero)
                    {
                        // LocalFree does not clear the decrypted native allocation.
                        for (int index = 0; index < length; index++) Marshal.WriteByte(output, index, 0);
                        LocalFree(output);
                    }
                }
            }

            private static byte[] Copy(IntPtr output, int length)
            {
                if (output == IntPtr.Zero || length < 1) throw new CryptographicException("Protected context is empty.");
                var result = new byte[length];
                Marshal.Copy(output, result, 0, length);
                return result;
            }

            private static void Check(int status)
            {
                if (status != 0) throw new CryptographicException(status);
            }

            [DllImport("ncrypt.dll", CharSet = CharSet.Unicode)]
            private static extern int NCryptCreateProtectionDescriptor(string descriptor, int flags, out IntPtr handle);
            [DllImport("ncrypt.dll")]
            private static extern int NCryptCloseProtectionDescriptor(IntPtr handle);
            [DllImport("ncrypt.dll")]
            private static extern int NCryptProtectSecret(IntPtr descriptor, int flags, byte[] data, int length,
                IntPtr memory, IntPtr window, out IntPtr output, out int outputLength);
            [DllImport("ncrypt.dll")]
            private static extern int NCryptUnprotectSecret(IntPtr descriptor, int flags, byte[] data, int length,
                IntPtr memory, IntPtr window, out IntPtr output, out int outputLength);
            [DllImport("kernel32.dll")]
            private static extern IntPtr LocalFree(IntPtr memory);
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
