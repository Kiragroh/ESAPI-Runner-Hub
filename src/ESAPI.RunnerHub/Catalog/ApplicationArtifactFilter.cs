namespace EsapiRunnerHub.Catalog
{
    public enum ApplicationArtifactFilter
    {
        All,
        Standalone,
        SingleFile,
        Binary
    }

    public sealed class ArtifactFilterOption
    {
        public ArtifactFilterOption(ApplicationArtifactFilter kind, string label)
        {
            Kind = kind;
            Label = label;
        }

        public ApplicationArtifactFilter Kind { get; private set; }
        public string Label { get; private set; }
    }
}
