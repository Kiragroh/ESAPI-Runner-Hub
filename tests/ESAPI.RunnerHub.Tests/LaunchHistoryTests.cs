using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
