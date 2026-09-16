using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using EsapiRunnerHub.Context;
using EsapiRunnerHub.History;

namespace EsapiRunnerHub.Tests
{
    internal static class LaunchHistoryTests
    {
        public static void Register()
        {
            TestHarness.Test("history context is DPAPI protected and round trips", ProtectsContext);
            TestHarness.Test("history store enforces age and count retention", EnforcesRetention);
            TestHarness.Test("history store keeps context identifiers out of JSON", KeepsIdentifiersEncrypted);
            TestHarness.Test("history store recovers from corrupt JSON", RecoversFromCorruptJson);
            TestHarness.Test("history stale instances merge without losing launches after restart", MergesStaleInstances);
            TestHarness.Test("history empty stale save cannot erase persisted launches", EmptySaveCannotErase);
            TestHarness.Test("history stale running snapshot cannot resurrect completed launch", KeepsCompletedState);
            TestHarness.Test("history concurrent store instances keep all launches", KeepsConcurrentLaunches);
            TestHarness.Test("history corrupt primary recovers latest backup", RecoversLatestBackup);
            TestHarness.Test("history corrupt primary without backup is never overwritten", PreservesUnrecoverablePrimary);
            TestHarness.Test("history migrates legacy local and network copies without deleting originals", MigratesLegacyCopies);
            TestHarness.Test("history follows host changes with distinct local profiles", FollowsHostChanges);
            TestHarness.Test("history queues during network failure and merges after recovery", RecoversAfterNetworkFailure);
            TestHarness.Test("history shared lock timeout preserves primary and queues locally", LockTimeoutQueuesLocally);
            TestHarness.Test("history independent processes merge every launch", MergesIndependentProcesses);
            TestHarness.Test("history atomic replacement survives a briefly shared file handle", RetriesBriefReplaceConflict);
            TestHarness.Test("history backup repair preserves corrupt original evidence", PreservesCorruptEvidence);
            TestHarness.Test("history readable rows stay visible when migration write is blocked", KeepsReadableRowsWhenWriteBlocked);
            TestHarness.Test("history uses account-scoped roaming protection for new contexts", UsesRoamingProtection);
            TestHarness.Test("history survives an older runner overwriting both snapshot files", SurvivesLegacySnapshotOverwrite);
            TestHarness.Test("history upgrades accessible legacy context without changing its selection", UpgradesLegacyProtection);
            TestHarness.Test("history stale completed snapshot cannot replace roaming context with legacy protection", KeepsRoamingProtection);
            TestHarness.Test("history unavailable legacy context remains opaque and intact", PreservesOpaqueLegacyProtection);
            TestHarness.Test("history healthy primary remains readable when durable recovery is corrupt", SurvivesCorruptDurableRecovery);
            TestHarness.Test("history waits for a bounded slow shared commit without losing the next launch", WaitsForSlowSharedCommit);
        }

        private static void ProtectsContext()
        {
            var selection = Selection();
            var protector = new ProtectedContextEnvelope();

            var encrypted = protector.Protect(selection);
            var restored = protector.Unprotect(encrypted);

            TestHarness.AssertFalse(encrypted.Contains("SYN-1001"));
            TestHarness.AssertEqual("SYN-1001", restored.PatientId);
            TestHarness.AssertEqual("P1", restored.PlanId);
            TestHarness.AssertEqual("SS1", restored.StructureSetId);
            TestHarness.AssertEqual("P1", restored.PlanIdsInScope.Single());
        }

        private static void UsesRoamingProtection()
        {
            var protector = new ProtectedContextEnvelope();
            var encrypted = protector.Protect(Selection());
            TestHarness.AssertTrue(encrypted.StartsWith("dpapi-ng:v1:", StringComparison.Ordinal),
                "New context still depends on a host-local DPAPI master key.");
            TestHarness.AssertEqual("SYN-1001", protector.Unprotect(encrypted).PatientId);
        }

        private static string LegacyProtectedSelection()
        {
            var clear = Encoding.UTF8.GetBytes("{\"PatientId\":\"SYN-1001\",\"CourseId\":\"C1\",\"PlanId\":\"P1\",\"StructureSetId\":\"SS1\",\"ImageId\":\"IMG1\",\"PlanIdsInScope\":[\"P1\"],\"PlanSumIdsInScope\":[]}");
            try
            {
                return Convert.ToBase64String(ProtectedData.Protect(clear,
                    Encoding.UTF8.GetBytes("ESAPI Runner Hub launch context v1"), DataProtectionScope.CurrentUser));
            }
            finally { Array.Clear(clear, 0, clear.Length); }
        }

        private static void UpgradesLegacyProtection()
        {
            WithStore(30, 100, (store, path) =>
            {
                var entry = Entry("legacy", DateTime.UtcNow);
                entry.LaunchMode = LaunchMode.Context;
                entry.ProtectedContext = LegacyProtectedSelection();
                store.Save(new[] { entry });
                var reopened = new LaunchHistoryStore(path, 30, 100).Load().Single();
                TestHarness.AssertTrue(reopened.ProtectedContext.StartsWith("dpapi-ng:v1:", StringComparison.Ordinal),
                    "Accessible legacy context was not upgraded for the next host.");
                var restored = new ProtectedContextEnvelope().Unprotect(reopened.ProtectedContext);
                TestHarness.AssertEqual("SYN-1001", restored.PatientId);
                TestHarness.AssertEqual("C1", restored.CourseId);
                TestHarness.AssertEqual("P1", restored.PlanId);
                TestHarness.AssertEqual("P1", restored.PlanIdsInScope.Single());
                TestHarness.AssertFalse(File.ReadAllText(path).Contains("SYN-1001"));
            });
        }

        private static void KeepsRoamingProtection()
        {
            WithStore(30, 100, (store, path) =>
            {
                var legacy = Entry("same", DateTime.UtcNow);
                legacy.LaunchMode = LaunchMode.Context;
                legacy.ProtectedContext = LegacyProtectedSelection();
                legacy.State = LaunchHistoryState.Exited;
                legacy.FinishedUtc = DateTime.UtcNow;
                var roaming = Entry("same", legacy.StartedUtc);
                roaming.LaunchMode = LaunchMode.Context;
                roaming.State = LaunchHistoryState.Running;
                roaming.ProtectedContext = new ProtectedContextEnvelope().Protect(Selection());
                store.Save(new[] { roaming });
                store.Save(new[] { legacy });
                var reopened = new LaunchHistoryStore(path, 30, 100).Load().Single();
                TestHarness.AssertEqual(LaunchHistoryState.Exited, reopened.State);
                TestHarness.AssertTrue(roaming.ProtectedContext == reopened.ProtectedContext,
                    "Stale terminal state replaced the account-scoped context envelope.");
            });
        }

        private static void PreservesOpaqueLegacyProtection()
        {
            WithStore(30, 100, (store, path) =>
            {
                var entry = Entry("opaque", DateTime.UtcNow);
                entry.LaunchMode = LaunchMode.Context;
                entry.ProtectedContext = "opaque-legacy-cannot-decrypt-here";
                store.Save(new[] { entry });
                TestHarness.AssertEqual(entry.ProtectedContext, new LaunchHistoryStore(path, 30, 100).Load().Single().ProtectedContext);
            });
        }

        private static void SurvivesCorruptDurableRecovery()
        {
            WithStore(30, 100, (store, path) =>
            {
                store.Save(new[] { Entry("readable", DateTime.UtcNow) });
                File.WriteAllText(path + ".durable", "{synthetic-durable-corruption");
                File.WriteAllText(path + ".durable.bak", "{synthetic-durable-backup-corruption");
                TestHarness.AssertEqual(1, new LaunchHistoryStore(path, 30, 100).Load().Count);
            });
        }

        private static void WaitsForSlowSharedCommit()
        {
            WithStore(30, 100, (store, path) =>
            {
                var held = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                var release = Task.Run(() => { System.Threading.Thread.Sleep(3300); held.Dispose(); });
                bool saved;
                try { saved = store.Save(new[] { Entry("after-slow-commit", DateTime.UtcNow) }); }
                finally { release.Wait(); }
                TestHarness.AssertTrue(saved, "A bounded preceding SMB commit lost the queued launch.");
                TestHarness.AssertEqual(1, store.Load().Count);
            });
        }

        private static void SurvivesLegacySnapshotOverwrite()
        {
            WithStore(30, 100, (store, path) =>
            {
                var protector = new ProtectedContextEnvelope();
                var entry = Entry("newer-window", DateTime.UtcNow);
                entry.LaunchMode = LaunchMode.Context;
                entry.ProtectedContext = protector.Protect(Selection());
                TestHarness.AssertTrue(store.Save(new[] { entry }));
                // The old executable cannot be retroactively taught merge-before-save.
                File.WriteAllText(path, "[]");
                File.WriteAllText(path + ".bak", "[]");
                var reopened = new LaunchHistoryStore(path, 30, 100).Load();
                TestHarness.AssertEqual(1, reopened.Count);
                TestHarness.AssertEqual("SYN-1001", protector.Unprotect(reopened.Single().ProtectedContext).PatientId);
            });
        }

        private static void EnforcesRetention()
        {
            WithStore(2, 2, (store, path) =>
            {
                var now = DateTime.UtcNow;
                store.Save(new[]
                {
                    Entry("old", now.AddDays(-3)),
                    Entry("newest", now),
                    Entry("middle", now.AddMinutes(-1)),
                    Entry("third", now.AddMinutes(-2))
                });

                var loaded = store.Load();
                TestHarness.AssertEqual(2, loaded.Count);
                TestHarness.AssertEqual("newest", loaded[0].HistoryId);
                TestHarness.AssertEqual("middle", loaded[1].HistoryId);
            });
        }

        private static void KeepsIdentifiersEncrypted()
        {
            WithStore(30, 100, (store, path) =>
            {
                var entry = Entry("protected", DateTime.UtcNow);
                entry.ProtectedContext = new ProtectedContextEnvelope().Protect(Selection());
                store.Save(new[] { entry });

                var json = File.ReadAllText(path);
                TestHarness.AssertFalse(json.Contains("SYN-1001"));
                TestHarness.AssertFalse(json.Contains("\"P1\""));
                TestHarness.AssertContains(json, "ProtectedContext");
            });
        }

        private static void RecoversFromCorruptJson()
        {
            WithStore(30, 100, (store, path) =>
            {
                File.WriteAllText(path, "{not-json");
                TestHarness.AssertEqual(0, store.Load().Count);
            });
        }

        private static void MergesStaleInstances()
        {
            WithStore(30, 100, (first, path) =>
            {
                var second = new LaunchHistoryStore(path, 30, 100);
                var firstSnapshot = first.Load();
                var secondSnapshot = second.Load();
                firstSnapshot.Add(Entry("first", DateTime.UtcNow));
                secondSnapshot.Add(Entry("second", DateTime.UtcNow));
                TestHarness.AssertTrue(first.Save(firstSnapshot));
                TestHarness.AssertTrue(second.Save(secondSnapshot));
                TestHarness.AssertEqual(2, new LaunchHistoryStore(path, 30, 100).Load().Count);
            });
        }

        private static void EmptySaveCannotErase()
        {
            WithStore(30, 100, (store, path) =>
            {
                store.Save(new[] { Entry("retained", DateTime.UtcNow) });
                new LaunchHistoryStore(path, 30, 100).Save(new LaunchHistoryEntry[0]);
                TestHarness.AssertEqual("retained", store.Load().Single().HistoryId);
            });
        }

        private static void KeepsCompletedState()
        {
            WithStore(30, 100, (store, path) =>
            {
                var active = Entry("same", DateTime.UtcNow);
                active.State = LaunchHistoryState.Running;
                store.Save(new[] { active });
                var stale = new LaunchHistoryStore(path, 30, 100).Load();
                active.State = LaunchHistoryState.Exited;
                active.FinishedUtc = DateTime.UtcNow;
                active.ExitCode = 0;
                store.Save(new[] { active });
                new LaunchHistoryStore(path, 30, 100).Save(stale);
                TestHarness.AssertEqual(LaunchHistoryState.Exited, store.Load().Single().State);
            });
        }

        private static void KeepsConcurrentLaunches()
        {
            WithStore(30, 100, (store, path) =>
            {
                var writes = Enumerable.Range(0, 12).Select(index => Task.Run(() =>
                    new LaunchHistoryStore(path, 30, 100).Save(new[] { Entry("parallel-" + index, DateTime.UtcNow) }))).ToArray();
                Task.WaitAll(writes);
                TestHarness.AssertTrue(writes.All(write => write.Result));
                TestHarness.AssertEqual(12, store.Load().Count);
            });
        }

        private static void RecoversLatestBackup()
        {
            WithStore(30, 100, (store, path) =>
            {
                store.Save(new[] { Entry("backup", DateTime.UtcNow) });
                File.WriteAllText(path, "{broken-primary");
                TestHarness.AssertEqual("backup", new LaunchHistoryStore(path, 30, 100).Load().Single().HistoryId);
            });
        }

        private static void PreservesUnrecoverablePrimary()
        {
            WithStore(30, 100, (store, path) =>
            {
                File.WriteAllText(path, "{preserve-original");
                store.Load();
                TestHarness.AssertFalse(store.Save(new LaunchHistoryEntry[0]));
                TestHarness.AssertEqual("{preserve-original", File.ReadAllText(path));
            });
        }

        private static void MigratesLegacyCopies()
        {
            WithStore(30, 100, (unused, path) =>
            {
                var local = path + ".local";
                var legacy = path + ".legacy";
                new LaunchHistoryStore(local, 30, 100).Save(new[] { Entry("local", DateTime.UtcNow) });
                new LaunchHistoryStore(legacy, 30, 100).Save(new[] { Entry("legacy", DateTime.UtcNow) });
                var original = File.ReadAllText(legacy);
                TestHarness.AssertEqual(2, SharedStore(path, local, legacy).Load().Count);
                TestHarness.AssertEqual(2, new LaunchHistoryStore(path, 30, 100).Load().Count);
                TestHarness.AssertEqual(original, File.ReadAllText(legacy));
                TestHarness.AssertTrue(File.Exists(local));
            });
        }

        private static void FollowsHostChanges()
        {
            WithStore(30, 100, (unused, path) =>
            {
                var hostA = SharedStore(path, path + ".host-a");
                TestHarness.AssertTrue(hostA.Save(new[] { Entry("host-a", DateTime.UtcNow) }));
                var hostB = SharedStore(path, path + ".host-b");
                TestHarness.AssertEqual("host-a", hostB.Load().Single().HistoryId);
                TestHarness.AssertTrue(File.Exists(path + ".host-b"), "New host must retain a local recovery copy.");
                hostB.Save(new[] { Entry("host-b", DateTime.UtcNow) });
                TestHarness.AssertEqual(2, SharedStore(path, path + ".host-c").Load().Count);
            });
        }

        private static void RecoversAfterNetworkFailure()
        {
            WithStore(30, 100, (unused, path) =>
            {
                var blocker = path + ".unavailable-share";
                var sharedPath = Path.Combine(blocker, "shared.json");
                var local = path + ".local";
                File.WriteAllText(blocker, "synthetic network obstruction");
                var offline = SharedStore(sharedPath, local);
                TestHarness.AssertTrue(offline.Save(new[] { Entry("offline", DateTime.UtcNow) }));
                TestHarness.AssertContains(offline.StatusText, "local");
                TestHarness.AssertEqual("offline", SharedStore(sharedPath, local).Load().Single().HistoryId);
                File.Delete(blocker);
                new LaunchHistoryStore(sharedPath, 30, 100).Save(new[] { Entry("remote", DateTime.UtcNow) });
                TestHarness.AssertEqual(2, SharedStore(sharedPath, local).Load().Count);
                TestHarness.AssertEqual(2, new LaunchHistoryStore(sharedPath, 30, 100).Load().Count);
            });
        }

        private static void LockTimeoutQueuesLocally()
        {
            WithStore(30, 100, (store, path) =>
            {
                store.Save(new[] { Entry("original", DateTime.UtcNow) });
                var original = File.ReadAllText(path);
                using (new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    var watch = Stopwatch.StartNew();
                    TestHarness.AssertTrue(SharedStore(path, path + ".local").Save(new[] { Entry("queued", DateTime.UtcNow) }));
                    TestHarness.AssertTrue(watch.Elapsed < TimeSpan.FromSeconds(6), "Lock acquisition must have a bounded timeout.");
                    TestHarness.AssertEqual(original, File.ReadAllText(path));
                }
                TestHarness.AssertEqual(2, SharedStore(path, path + ".local").Load().Count);
            });
        }

        private static LaunchHistoryStore SharedStore(string path, string fallback, string migration = null)
        {
            return new LaunchHistoryStore(path, 30, 100, fallback, migration);
        }

        private static void RetriesBriefReplaceConflict()
        {
            WithStore(30, 100, (store, path) =>
            {
                TestHarness.AssertTrue(store.Save(new[] { Entry("original", DateTime.UtcNow) }));
                var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var release = Task.Run(() => { System.Threading.Thread.Sleep(200); reader.Dispose(); });
                bool saved;
                try { saved = store.Save(new[] { Entry("second", DateTime.UtcNow) }); }
                finally { release.Wait(); }
                TestHarness.AssertTrue(saved, "A brief replace-sharing conflict discarded the save attempt.");
                TestHarness.AssertEqual(2, store.Load().Count);
            });
        }

        private static void MergesIndependentProcesses()
        {
            WithStore(30, 100, (store, path) =>
            {
                var workers = new List<Process>();
                try
                {
                    for (var index = 0; index < 4; index++)
                        workers.Add(Process.Start(new ProcessStartInfo
                        {
                            FileName = typeof(LaunchHistoryTests).Assembly.Location,
                            Arguments = "--history-worker \"" + path + "\" " + index,
                            UseShellExecute = false, CreateNoWindow = true,
                            RedirectStandardOutput = true, RedirectStandardError = true
                        }));
                    foreach (var worker in workers)
                    {
                        TestHarness.AssertTrue(worker.WaitForExit(30000), "Synthetic history worker timed out.");
                        TestHarness.AssertTrue(worker.ExitCode == 0,
                            "Synthetic worker exit " + worker.ExitCode + ": " + worker.StandardOutput.ReadToEnd() + worker.StandardError.ReadToEnd());
                    }
                    TestHarness.AssertEqual(20, store.Load().Count);
                }
                finally
                {
                    foreach (var worker in workers)
                    {
                        // All child handles must finish before the fixture directory is removed.
                        if (!worker.HasExited && !worker.WaitForExit(30000)) { worker.Kill(); worker.WaitForExit(); }
                        worker.Dispose();
                    }
                }
            });
        }

        public static int RunWorker(string path, string worker)
        {
            for (var index = 0; index < 5; index++)
            {
                var store = new LaunchHistoryStore(path, 30, 100);
                var watch = Stopwatch.StartNew();
                if (!store.Save(new[] { Entry("worker-" + worker + "-" + index, DateTime.UtcNow) }))
                {
                    Console.WriteLine("Synthetic history worker failed after " + watch.ElapsedMilliseconds + " ms; code " +
                        (store.LastStorageErrorCode ?? 0).ToString("X8") + ": " + store.StatusText);
                    // The worker owns only synthetic data. Its technical log contains event
                    // categories and exception types, never identifiers or exception messages.
                    System.Threading.Thread.Sleep(200);
                    var log = EsapiRunnerHub.Privacy.TechnicalLog.Current.FilePath;
                    if (File.Exists(log)) Console.WriteLine(File.ReadAllText(log));
                    return 1;
                }
            }
            return 0;
        }

        private static void PreservesCorruptEvidence()
        {
            WithStore(30, 100, (store, path) =>
            {
                store.Save(new[] { Entry("recover", DateTime.UtcNow) });
                File.WriteAllText(path, "{synthetic-corrupt-evidence");
                TestHarness.AssertTrue(store.Save(new[] { Entry("new", DateTime.UtcNow) }));
                TestHarness.AssertEqual(2, store.Load().Count);
                var preserved = Directory.GetFiles(Path.GetDirectoryName(path), "history.json.corrupt-*").Single();
                TestHarness.AssertEqual("{synthetic-corrupt-evidence", File.ReadAllText(preserved));
            });
        }

        private static void KeepsReadableRowsWhenWriteBlocked()
        {
            WithStore(30, 100, (store, path) =>
            {
                store.Save(new[] { Entry("readable", DateTime.UtcNow) });
                var local = path + ".local";
                new LaunchHistoryStore(local, 30, 100).Save(new[] { Entry("local", DateTime.UtcNow) });
                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var shared = SharedStore(path, local);
                    TestHarness.AssertEqual(2, shared.Load().Count);
                    TestHarness.AssertContains(shared.StatusText, "synchronization");
                }
            });
        }

        private static LaunchHistoryEntry Entry(string id, DateTime startedUtc)
        {
            return new LaunchHistoryEntry
            {
                HistoryId = id,
                ApplicationId = "fixture",
                ApplicationName = "Fixture",
                ArtifactLabel = "Standalone",
                AccessLabel = "Read-only",
                StartedUtc = startedUtc,
                State = LaunchHistoryState.Exited,
                LaunchMode = LaunchMode.WithoutPatient
            };
        }

        private static ContextSelection Selection()
        {
            var selection = new ContextSelection
            {
                PatientId = "SYN-1001", CourseId = "C1", PlanId = "P1",
                StructureSetId = "SS1", ImageId = "IMG1"
            };
            selection.PlanIdsInScope.Add("P1");
            return selection;
        }

        private static void WithStore(int retentionDays, int maxEntries, Action<LaunchHistoryStore, string> action)
        {
            var directory = Path.Combine(Path.GetTempPath(), "runner-hub-history-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var path = Path.Combine(directory, "history.json");
                action(new LaunchHistoryStore(path, retentionDays, maxEntries), path);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
