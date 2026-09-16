using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Threading;
using EsapiRunnerHub.Privacy;

namespace EsapiRunnerHub.History
{
    public sealed class LaunchHistoryStore
    {
        private readonly string path;
        private readonly int retentionDays;
        private readonly int maxEntries;
        private readonly string fallbackPath;
        private readonly string migrationPath;
        private readonly object fileGate = new object();

        public LaunchHistoryStore(string path, int retentionDays, int maxEntries)
            : this(path, retentionDays, maxEntries, null, null)
        {
        }

        public LaunchHistoryStore(string path, int retentionDays, int maxEntries, string fallbackPath, string migrationPath)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("History path is required.", nameof(path));
            if (retentionDays < 1) throw new ArgumentOutOfRangeException(nameof(retentionDays));
            if (maxEntries < 1) throw new ArgumentOutOfRangeException(nameof(maxEntries));
            this.path = Path.GetFullPath(path);
            this.retentionDays = retentionDays;
            this.maxEntries = maxEntries;
            this.fallbackPath = DistinctPath(fallbackPath, this.path);
            this.migrationPath = DistinctPath(migrationPath, this.path, this.fallbackPath);
        }

        public string StatusText { get; private set; }
        // Numeric-only storage diagnostics; never expose exception messages or paths.
        public int? LastStorageErrorCode { get; private set; }

        // Independent of the shared I/O gate: a stalled SMB operation must not prevent
        // the current host from committing the next encrypted launch locally.
        public bool SaveLocalRecovery(IEnumerable<LaunchHistoryEntry> entries)
        {
            return TryMergeInto(fallbackPath ?? path, entries ?? Enumerable.Empty<LaunchHistoryEntry>());
        }

        public IList<LaunchHistoryEntry> Load()
        {
            lock (fileGate)
            {
                try
                {
                    using (AcquireLock(path))
                    {
                        bool recovered;
                        var primary = ReadRecoverable(path, out recovered);
                        var previous = ReadAuxiliary(fallbackPath).Concat(ReadAuxiliary(migrationPath)).ToList();
                        var entries = Retain(primary.Concat(previous));
                        var upgraded = UpgradeLegacyContexts(entries);
                        // Loading also imports local/legacy rows, so they survive a host change
                        // even if this Runner session does not start another application.
                        var synchronized = true;
                        try
                        {
                            if (entries.Count > 0 && (upgraded || recovered || previous.Count > 0 || !File.Exists(path + ".durable")))
                                WriteSnapshot(path, entries, recovered);
                        }
                        catch (Exception exception)
                        {
                            // A successfully read history must remain visible even if the
                            // subsequent migration/backup write is denied or the share drops.
                            synchronized = false;
                            TechnicalLog.Current.Write("WARN", "history_migration_save_failed", string.Empty, exception);
                        }
                        var mirrored = MirrorFallback(entries);
                        StatusText = !synchronized ? "History loaded; shared synchronization is unavailable. Existing files have been preserved."
                            : recovered ? "History recovered from its backup."
                            : mirrored ? string.Empty : "Shared history loaded; local recovery copy is unavailable.";
                        return entries;
                    }
                }
                catch (Exception exception)
                {
                    TechnicalLog.Current.Write("WARN", "history_load_failed", string.Empty, exception);
                    return LoadFallback();
                }
            }
        }

        public bool Save(IEnumerable<LaunchHistoryEntry> entries)
        {
            lock (fileGate)
            {
                try
                {
                    using (AcquireLock(path))
                    {
                        bool recovered;
                        var persisted = ReadRecoverable(path, out recovered);
                        var retained = Retain(persisted.Concat(ReadAuxiliary(fallbackPath))
                            .Concat(ReadAuxiliary(migrationPath)).Concat(entries ?? Enumerable.Empty<LaunchHistoryEntry>()));
                        WriteSnapshot(path, retained, recovered);
                        var mirrored = MirrorFallback(retained);
                        StatusText = recovered ? "History recovered from its backup."
                            : mirrored ? string.Empty : "Shared history saved; local recovery copy is unavailable.";
                        return true;
                    }
                }
                catch (Exception exception)
                {
                    LastStorageErrorCode = exception.HResult;
                    TechnicalLog.Current.Write("WARN", "history_save_failed", string.Empty, exception);
                    return SaveFallback(entries);
                }
            }
        }

        private IList<LaunchHistoryEntry> LoadFallback()
        {
            try
            {
                if (fallbackPath != null)
                {
                    using (AcquireLock(fallbackPath))
                    {
                        bool recovered;
                        var entries = Retain(ReadRecoverable(fallbackPath, out recovered).Concat(ReadAuxiliary(migrationPath)));
                        StatusText = "Shared history unavailable. Using local history; it will merge on the next load/save.";
                        return entries;
                    }
                }
            }
            catch (Exception exception) { TechnicalLog.Current.Write("WARN", "history_fallback_load_failed", string.Empty, exception); }
            StatusText = "History could not be loaded. Existing files have been preserved.";
            return new List<LaunchHistoryEntry>();
        }

        private bool SaveFallback(IEnumerable<LaunchHistoryEntry> entries)
        {
            if (fallbackPath != null && TryMergeInto(fallbackPath,
                (entries ?? Enumerable.Empty<LaunchHistoryEntry>()).Concat(ReadAuxiliary(migrationPath))))
            {
                StatusText = "Shared history unavailable. Saved locally; it will merge on the next load/save.";
                return true;
            }
            StatusText = "History could not be saved. Existing files have been preserved.";
            return false;
        }

        private bool MirrorFallback(IEnumerable<LaunchHistoryEntry> entries)
        {
            return fallbackPath == null || TryMergeInto(fallbackPath, entries);
        }

        private bool TryMergeInto(string target, IEnumerable<LaunchHistoryEntry> entries)
        {
            try
            {
                using (AcquireLock(target))
                {
                    bool recovered;
                    var retained = Retain(ReadRecoverable(target, out recovered).Concat(entries));
                    WriteSnapshot(target, retained, recovered);
                    return true;
                }
            }
            catch (Exception exception)
            {
                TechnicalLog.Current.Write("WARN", "history_fallback_save_failed", string.Empty, exception);
                return false;
            }
        }

        private static IEnumerable<LaunchHistoryEntry> ReadAuxiliary(string target)
        {
            if (target == null) return Enumerable.Empty<LaunchHistoryEntry>();
            try
            {
                using (AcquireLock(target))
                {
                    bool recovered;
                    return ReadRecoverable(target, out recovered);
                }
            }
            catch (Exception exception)
            {
                TechnicalLog.Current.Write("WARN", "history_previous_copy_load_failed", string.Empty, exception);
                return Enumerable.Empty<LaunchHistoryEntry>();
            }
        }

        private static string DistinctPath(string candidate, params string[] others)
        {
            if (string.IsNullOrWhiteSpace(candidate)) return null;
            var fullPath = Path.GetFullPath(candidate);
            return others.Any(other => string.Equals(fullPath, other, StringComparison.OrdinalIgnoreCase)) ? null : fullPath;
        }

        // FileShare.None is enforced by SMB too, unlike a machine-local mutex.
        // The empty lock file remains in place; only the open handle owns the lock.
        private static FileStream AcquireLock(string target)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            var timer = Stopwatch.StartNew();
            while (true)
            {
                try { return new FileStream(target + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
                catch (IOException)
                {
                    // Several preceding durable commits on SMB can occupy the lock
                    // for more than three seconds; retain a bounded five-second queue.
                    if (timer.ElapsedMilliseconds >= 5000) throw;
                    Thread.Sleep(40);
                }
            }
        }

        private static List<LaunchHistoryEntry> ReadFile(string target)
        {
            try
            {
                using (var stream = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var serializer = new DataContractJsonSerializer(typeof(List<LaunchHistoryEntry>));
                    var entries = serializer.ReadObject(stream) as List<LaunchHistoryEntry>;
                    if (entries == null || entries.Any(item => item == null || string.IsNullOrWhiteSpace(item.HistoryId)))
                        throw new InvalidDataException("Invalid history document.");
                    return entries;
                }
            }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
        }

        private static List<LaunchHistoryEntry> ReadRecoverable(string target, out bool recovered)
        {
            bool durableRecovered;
            List<LaunchHistoryEntry> durable;
            try { durable = ReadRecoverableSnapshot(target + ".durable", out durableRecovered); }
            catch (Exception exception)
            {
                TechnicalLog.Current.Write("WARN", "history_durable_load_failed", string.Empty, exception);
                durable = new List<LaunchHistoryEntry>();
            }
            try
            {
                return ReadRecoverableSnapshot(target, out recovered).Concat(durable).ToList();
            }
            catch
            {
                if (durable.Count == 0) throw;
                recovered = true;
                return durable;
            }
        }

        private static bool UpgradeLegacyContexts(IEnumerable<LaunchHistoryEntry> entries)
        {
            var protector = new ProtectedContextEnvelope();
            var changed = false;
            foreach (var entry in entries.Where(item => item.LaunchMode != LaunchMode.WithoutPatient &&
                !string.IsNullOrWhiteSpace(item.ProtectedContext) && !ProtectedContextEnvelope.IsRoaming(item.ProtectedContext)))
            {
                try
                {
                    var selection = protector.Unprotect(entry.ProtectedContext);
                    var upgraded = protector.Protect(selection);
                    // Verify before replacing the only locally readable legacy envelope.
                    protector.Unprotect(upgraded);
                    entry.ProtectedContext = upgraded;
                    changed = true;
                }
                catch (Exception exception)
                {
                    // A different VDA may still have the original profile key. Keep the
                    // exact opaque bytes so that host can migrate this row in the future.
                    TechnicalLog.Current.Write("WARN", "history_context_upgrade_unavailable", entry.ApplicationId, exception);
                }
            }
            return changed;
        }

        private static List<LaunchHistoryEntry> ReadRecoverableSnapshot(string target, out bool recovered)
        {
            recovered = false;
            try
            {
                var primary = ReadFile(target);
                if (primary != null) return primary;
            }
            catch
            {
                var backup = ReadFile(target + ".bak");
                if (backup == null) throw;
                recovered = true;
                return backup;
            }
            var remainingBackup = ReadFile(target + ".bak");
            recovered = remainingBackup != null;
            return remainingBackup ?? new List<LaunchHistoryEntry>();
        }

        private static void WriteSnapshot(string target, IList<LaunchHistoryEntry> entries, bool recovered)
        {
            // Earlier executables can still replace primary + .bak with stale lists.
            // They never write this generation's recovery snapshot. Commit it first,
            // under the same SMB lock, so a later refresh/restart can recover every row.
            bool durableRecovered;
            try { ReadRecoverableSnapshot(target + ".durable", out durableRecovered); }
            catch { durableRecovered = true; }
            WriteSnapshotFile(target + ".durable", entries, durableRecovered);
            WriteSnapshotFile(target, entries, recovered);
        }

        private static void WriteSnapshotFile(string target, IList<LaunchHistoryEntry> entries, bool recovered)
        {
            var temporaryPath = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(List<LaunchHistoryEntry>));
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    serializer.WriteObject(stream, entries.ToList());
                    stream.Flush(true);
                }
                if (File.Exists(target))
                {
                    // Never place a corrupt primary over the known-good backup.
                    var previous = recovered ? target + ".corrupt-" + Guid.NewGuid().ToString("N") : target + ".bak";
                    ReplaceSnapshot(temporaryPath, target, previous);
                }
                else File.Move(temporaryPath, target);

                // Keep the latest complete snapshot as a separate atomic recovery copy.
                File.Copy(target, temporaryPath);
                if (File.Exists(target + ".bak")) ReplaceSnapshot(temporaryPath, target + ".bak", null);
                else File.Move(temporaryPath, target + ".bak");
            }
            finally
            {
                try { File.Delete(temporaryPath); }
                catch { }
            }
        }

        private static void ReplaceSnapshot(string source, string destination, string backup)
        {
            var timer = Stopwatch.StartNew();
            while (true)
            {
                try { File.Replace(source, destination, backup); return; }
                catch (IOException exception)
                {
                    int code = exception.HResult & 0xffff;
                    // Brief non-delete-sharing handles can outlive their reader. These
                    // errors preserve the source/destination names; do not retry the
                    // partially-moved 1176/1177 cases or replace atomic I/O with deletion.
                    // Win32 ReplaceFile: ERROR_UNABLE_TO_REMOVE_REPLACED (1175).
                    if ((code != 32 && code != 33 && code != 1175) || timer.ElapsedMilliseconds >= 1500) throw;
                    Thread.Sleep(40);
                }
            }
        }

        private List<LaunchHistoryEntry> Retain(IEnumerable<LaunchHistoryEntry> entries)
        {
            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            return entries.Where(item => item != null && !string.IsNullOrWhiteSpace(item.HistoryId) && item.StartedUtc >= cutoff)
                .GroupBy(item => item.HistoryId, StringComparer.Ordinal)
                .Select(MergeEntryGroup)
                .OrderByDescending(item => item.StartedUtc)
                .ThenBy(item => item.HistoryId, StringComparer.Ordinal)
                .Take(maxEntries)
                .ToList();
        }

        private static LaunchHistoryEntry MergeEntryGroup(IEnumerable<LaunchHistoryEntry> group)
        {
            var versions = group.ToList();
            var selected = versions.OrderByDescending(item => StatePriority(item.State))
                .ThenByDescending(item => item.FinishedUtc ?? item.StartedUtc).First();
            // Outcome and context are independent: a legacy window may provide the
            // definitive exit while still holding a pre-migration DPAPI envelope.
            var roaming = versions.FirstOrDefault(item => ProtectedContextEnvelope.IsRoaming(item.ProtectedContext) &&
                item.LaunchMode == selected.LaunchMode && string.Equals(item.ApplicationId, selected.ApplicationId, StringComparison.OrdinalIgnoreCase));
            if (roaming == null || ProtectedContextEnvelope.IsRoaming(selected.ProtectedContext)) return selected;
            return new LaunchHistoryEntry
            {
                HistoryId = selected.HistoryId, ApplicationId = selected.ApplicationId,
                ApplicationName = selected.ApplicationName, ArtifactLabel = selected.ArtifactLabel,
                AccessLabel = selected.AccessLabel, StartedUtc = selected.StartedUtc,
                FinishedUtc = selected.FinishedUtc, State = selected.State, ExitCode = selected.ExitCode,
                LaunchMode = selected.LaunchMode, ProtectedContext = roaming.ProtectedContext
            };
        }

        private static int StatePriority(LaunchHistoryState state)
        {
            // A completed launch cannot restart under the same ID. Definitive completion
            // beats another instance's stale or inferred state, regardless of its clock.
            switch (state)
            {
                case LaunchHistoryState.Exited:
                case LaunchHistoryState.FailedToStart: return 5;
                case LaunchHistoryState.Interrupted: return 4;
                case LaunchHistoryState.Unavailable: return 3;
                case LaunchHistoryState.Running: return 2;
                default: return 1;
            }
        }
    }
}
