using System.Collections.Generic;
using EsapiRunnerHub.Patients;

namespace EsapiRunnerHub.Tests
{
    internal static class PatientSearchTests
    {
        public static void Register()
        {
            TestHarness.Test("patient search ranks exact and prefix IDs first", RanksIdsFirst);
            TestHarness.Test("patient search matches normalized name tokens", MatchesNormalizedNames);
            TestHarness.Test("patient search keeps source order and removes duplicate IDs", KeepsStableUniqueResults);
        }

        private static void RanksIdsFirst()
        {
            var index = CreateIndex();

            var exact = index.Find("4711", 10);
            TestHarness.AssertEqual("4711", exact[0].Id);

            var prefix = index.Find("47", 10);
            TestHarness.AssertEqual("4711", prefix[0].Id);
            TestHarness.AssertEqual("47110", prefix[1].Id);
        }

        private static void MatchesNormalizedNames()
        {
            var index = CreateIndex();
            var results = index.Find("mueller an", 10);

            TestHarness.AssertEqual(1, results.Count);
            TestHarness.AssertEqual("4711", results[0].Id);
            TestHarness.AssertContains(results[0].Display, "Müller");
            TestHarness.AssertContains(results[0].Display, "4711");
        }

        private static void KeepsStableUniqueResults()
        {
            var index = CreateIndex();
            var results = index.Find("m", 2);

            TestHarness.AssertEqual(2, results.Count);
            TestHarness.AssertEqual("4711", results[0].Id);
            TestHarness.AssertEqual("47110", results[1].Id);
        }

        private static PatientSearchIndex CreateIndex()
        {
            return new PatientSearchIndex(new List<PatientRecord>
            {
                new PatientRecord("4711", "Anna", "Müller", 0),
                new PatientRecord("47110", "Berta", "Meyer", 1),
                new PatientRecord("9000", "Max", "Schmidt", 2),
                new PatientRecord("4711", "Duplicate", "Record", 3)
            });
        }
    }
}
