using System;
using EsapiScriptHost.Contracts;

namespace EsapiScriptHost.Host
{
    public enum ScriptHostKind
    {
        ReadOnly,
        WriteEnabled
    }

    public static class ScriptHostCapability
    {
        public static ScriptHostKind Current
        {
            get
            {
#if WRITE_HOST
                return ScriptHostKind.WriteEnabled;
#else
                return ScriptHostKind.ReadOnly;
#endif
            }
        }

        public static void Validate(ScriptHostKind hostKind, WriteMode writeMode)
        {
            if (hostKind == ScriptHostKind.ReadOnly && writeMode != WriteMode.ReadOnly)
                throw new InvalidOperationException("The read-only script host refuses write-mode requests.");
            if (hostKind == ScriptHostKind.WriteEnabled && writeMode == WriteMode.ReadOnly)
                throw new InvalidOperationException("The write-enabled script host refuses read-only requests.");
        }
    }
}
