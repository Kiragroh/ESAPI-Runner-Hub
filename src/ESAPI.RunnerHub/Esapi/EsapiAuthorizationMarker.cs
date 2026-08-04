using VMS.TPS.Common.Model.API;

namespace EsapiRunnerHub.Esapi
{
    internal sealed class EsapiAuthorizationMarker
    {
        // ESAPI 16.1 checks that the standalone entry assembly declares an API reference.
        // The method is never invoked; the installed API is still located and loaded at runtime.
        public void DoNothing(PlanSetup plan)
        {
        }
    }
}
