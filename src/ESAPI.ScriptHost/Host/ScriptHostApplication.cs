using System;
using System.Linq;
using EsapiRunnerHub.Configuration;
using EsapiRunnerHub.Launching;

namespace EsapiScriptHost.Host
{
    public sealed class ScriptHostApplication
    {
        public void Run(ContextLaunchPayload payload, Func<SaveChoice> saveChoice)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (saveChoice == null) throw new ArgumentNullException(nameof(saveChoice));
            ScriptMetadataInspector.ValidateWriteMode(payload.ScriptPath, payload.WriteMode);

            using (var session = EsapiSession.Open(payload))
            {
                var context = new ContextResolver().Resolve(session.Patient, session.CurrentUser, payload);
                var engine = payload.ScriptEngine == ScriptEngine.Auto ? DetectEngine(payload) : payload.ScriptEngine;
                if (engine == ScriptEngine.EsapiEssentials)
                    new EsapiEssentialsInvoker().Invoke(payload.ScriptPath, payload.EntryType, context, payload.ExtraReferencePaths);
                else if (engine == ScriptEngine.Eclipse)
                    new EclipseScriptInvoker().Invoke(payload.ScriptPath, payload.EntryType, context, payload.ExtraReferencePaths);
                else
                    throw new InvalidOperationException("The script engine could not be determined.");

                var choice = payload.WriteMode == WriteMode.ConfirmSave ? saveChoice() : SaveChoice.Discard;
                if (SaveDecision.Decide(true, payload.WriteMode, choice) == SaveAction.SaveOnce)
                    session.SaveModifications();
            }
        }

        private static ScriptEngine DetectEngine(ContextLaunchPayload payload)
        {
            using (var scope = new ScriptAssemblyScope(payload.ScriptPath, payload.ExtraReferencePaths))
            {
                var assembly = scope.Load(payload.ScriptPath);
                var entry = string.IsNullOrWhiteSpace(payload.EntryType)
                    ? null
                    : assembly.GetType(payload.EntryType, false, false);
                var types = entry == null ? assembly.GetExportedTypes() : new[] { entry };
                if (types.Any(type => type.BaseType != null &&
                                      type.BaseType.FullName != null &&
                                      type.BaseType.FullName.StartsWith("EsapiEssentials.Plugin.ScriptBase", StringComparison.Ordinal)))
                    return ScriptEngine.EsapiEssentials;
                if (types.Any(type => type.GetMethods().Any(method => method.Name == "Execute")))
                    return ScriptEngine.Eclipse;
            }
            throw new InvalidOperationException("The script engine could not be detected.");
        }
    }
}
