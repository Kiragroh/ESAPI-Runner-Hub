using VMS.TPS.Common.Model.API;

#if WRITE_HOST
[assembly: ESAPIScript(IsWriteable = true)]
#endif

namespace EsapiScriptHost
{
    internal static class EsapiAuthorizationMarker
    {
        internal static void DeclareApiReference(PlanSetup plan)
        {
        }
    }
}
