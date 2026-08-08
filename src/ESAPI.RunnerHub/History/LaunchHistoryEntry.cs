using System;
using System.Runtime.Serialization;

namespace EsapiRunnerHub.History
{
    public enum LaunchMode
    {
        WithoutPatient,
        WithPatient,
        Context
    }

    public enum LaunchHistoryState
    {
        Starting,
        Running,
        Exited,
        FailedToStart,
        Unavailable,
        Interrupted
    }

    [DataContract]
    public sealed class LaunchHistoryEntry
    {
        [DataMember(Order = 1)] public string HistoryId { get; set; }
        [DataMember(Order = 2)] public string ApplicationId { get; set; }
        [DataMember(Order = 3)] public string ApplicationName { get; set; }
        [DataMember(Order = 4)] public string ArtifactLabel { get; set; }
        [DataMember(Order = 5)] public string AccessLabel { get; set; }
        [DataMember(Order = 6)] public DateTime StartedUtc { get; set; }
        [DataMember(Order = 7, EmitDefaultValue = false)] public DateTime? FinishedUtc { get; set; }
        [DataMember(Order = 8)] public LaunchHistoryState State { get; set; }
        [DataMember(Order = 9, EmitDefaultValue = false)] public int? ExitCode { get; set; }
        [DataMember(Order = 10)] public LaunchMode LaunchMode { get; set; }
        [DataMember(Order = 11, EmitDefaultValue = false)] public string ProtectedContext { get; set; }
    }
}
