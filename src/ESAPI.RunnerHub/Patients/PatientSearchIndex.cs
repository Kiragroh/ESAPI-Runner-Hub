using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace EsapiRunnerHub.Patients
{
    public sealed class PatientSearchIndex
    {
        private sealed class IndexedPatient
        {
            public PatientRecord Patient { get; set; }
            public string Id { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string Combined { get; set; }
        }

        private readonly List<IndexedPatient> patients;

        public PatientSearchIndex(IEnumerable<PatientRecord> patients)
        {
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            this.patients = (patients ?? Enumerable.Empty<PatientRecord>())
                .Where(patient => patient != null && !string.IsNullOrWhiteSpace(patient.Id))
                .Where(patient => seenIds.Add(patient.Id.Trim()))
                .Select(patient => new IndexedPatient
                {
                    Patient = patient,
                    Id = Normalize(patient.Id),
                    FirstName = Normalize(patient.FirstName),
                    LastName = Normalize(patient.LastName),
                    Combined = Normalize(patient.Id + " " + patient.FirstName + " " + patient.LastName)
                })
                .OrderBy(patient => patient.Patient.SourceIndex)
                .ToList();
        }

        public IList<PatientRecord> Find(string query, int maxResults)
        {
            if (maxResults <= 0)
            {
                return new List<PatientRecord>();
            }

            var normalizedQuery = Normalize(query);
            if (string.IsNullOrWhiteSpace(normalizedQuery))
            {
                return patients.Take(maxResults).Select(item => item.Patient).ToList();
            }

            var tokens = normalizedQuery.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return patients
                .Where(item => tokens.All(token => item.Combined.Contains(token)))
                .Select(item => new
                {
                    Item = item,
                    Rank = GetRank(item, normalizedQuery, tokens)
                })
                .OrderBy(match => match.Rank)
                .ThenBy(match => match.Item.Patient.SourceIndex)
                .Take(maxResults)
                .Select(match => match.Item.Patient)
                .ToList();
        }

        public PatientRecord FindById(string id)
        {
            var normalized = Normalize(id);
            return patients.Where(item => item.Id == normalized)
                .Select(item => item.Patient)
                .FirstOrDefault();
        }

        private static int GetRank(IndexedPatient item, string query, string[] tokens)
        {
            if (item.Id == query)
            {
                return 0;
            }

            if (item.Id.StartsWith(query, StringComparison.Ordinal))
            {
                return 1;
            }

            if (tokens.All(token => item.FirstName.StartsWith(token, StringComparison.Ordinal) ||
                                    item.LastName.StartsWith(token, StringComparison.Ordinal) ||
                                    item.Id.StartsWith(token, StringComparison.Ordinal)))
            {
                return 2;
            }

            return 3;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var german = value.Trim().ToLowerInvariant()
                .Replace("ä", "ae")
                .Replace("ö", "oe")
                .Replace("ü", "ue")
                .Replace("ß", "ss");
            var decomposed = german.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            var previousWasSpace = false;
            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                    previousWasSpace = false;
                }
                else if (!previousWasSpace)
                {
                    builder.Append(' ');
                    previousWasSpace = true;
                }
            }

            return builder.ToString().Trim();
        }
    }
}
