namespace EsapiRunnerHub.Patients
{
    public sealed class PatientRecord
    {
        public PatientRecord(string id, string firstName, string lastName, int sourceIndex)
        {
            Id = id ?? string.Empty;
            FirstName = firstName ?? string.Empty;
            LastName = lastName ?? string.Empty;
            SourceIndex = sourceIndex;
        }

        public string Id { get; private set; }

        public string FirstName { get; private set; }

        public string LastName { get; private set; }

        public int SourceIndex { get; private set; }

        public string Display
        {
            get
            {
                var name = (LastName + ", " + FirstName).Trim(' ', ',');
                return string.IsNullOrWhiteSpace(name) ? Id : name + "  ·  " + Id;
            }
        }
    }
}
