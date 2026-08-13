using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using EsapiRunnerHub.Privacy;

namespace EsapiRunnerHub.History
{
    public sealed class LaunchHistoryStore
    {
        private readonly string path;
        private readonly int retentionDays;
        private readonly int maxEntries;
        private readonly object fileGate = new object();

        public LaunchHistoryStore(string path, int retentionDays, int maxEntries)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("History path is required.", nameof(path));
            if (retentionDays < 1) throw new ArgumentOutOfRangeException(nameof(retentionDays));
            if (maxEntries < 1) throw new ArgumentOutOfRangeException(nameof(maxEntries));
            this.path = Path.GetFullPath(path);
            this.retentionDays = retentionDays;
            this.maxEntries = maxEntries;
        }

        public IList<LaunchHistoryEntry> Load()
        {
            lock (fileGate)
            {
                try
                {
                    if (!File.Exists(path)) return new List<LaunchHistoryEntry>();
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(List<LaunchHistoryEntry>));
                        var entries = serializer.ReadObject(stream) as List<LaunchHistoryEntry>;
                        return Retain(entries ?? new List<LaunchHistoryEntry>());
                    }
                }
                catch (Exception exception)
                {
                    TechnicalLog.Current.Write("WARN", "history_load_failed", string.Empty, exception);
                    return new List<LaunchHistoryEntry>();
                }
            }
        }

        public bool Save(IEnumerable<LaunchHistoryEntry> entries)
        {
            lock (fileGate)
            {
                string temporaryPath = null;
                try
                {
                    var retained = Retain(entries ?? Enumerable.Empty<LaunchHistoryEntry>());
                    var directory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                    temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    var serializer = new DataContractJsonSerializer(typeof(List<LaunchHistoryEntry>));
                    using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        serializer.WriteObject(stream, retained.ToList());
                        stream.Flush(true);
                    }

                    if (File.Exists(path)) File.Replace(temporaryPath, path, null);
                    else File.Move(temporaryPath, path);
                    temporaryPath = null;
                    return true;
                }
                catch (Exception exception)
                {
                    TechnicalLog.Current.Write("WARN", "history_save_failed", string.Empty, exception);
                    return false;
                }
                finally
                {
                    if (temporaryPath != null)
                    {
                        try { File.Delete(temporaryPath); }
                        catch { }
                    }
                }
            }
        }

        private List<LaunchHistoryEntry> Retain(IEnumerable<LaunchHistoryEntry> entries)
        {
            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            return entries.Where(item => item != null && item.StartedUtc >= cutoff)
                .OrderByDescending(item => item.StartedUtc)
                .Take(maxEntries)
                .ToList();
        }
    }
}
