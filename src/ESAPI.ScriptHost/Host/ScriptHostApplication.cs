using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using EsapiScriptHost.Contracts;

namespace EsapiScriptHost.Host
{
    public sealed class ScriptHostApplication
    {
        public const string StageDataKey = "ESAPI.RunnerHub.SafeFailureStage";

        public void Run(ContextLaunchPayload payload, Func<SaveChoice> saveChoice)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (saveChoice == null) throw new ArgumentNullException(nameof(saveChoice));
            var stage = "WPF-Initialisierung";
            try
            {
                WpfApplicationHost.EnsureCurrent();
                if (payload.ScriptPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    stage = "C#-Kompilierung";
                    payload.ScriptPath = new SourceScriptCompiler().Compile(payload.ScriptPath, payload.ApiAssemblyPath,
                        payload.TypesAssemblyPath, payload.ExtraReferencePaths);
                }
                stage = "Skriptprüfung";
                var runtimeReferences = RuntimeReferences(payload).ToList();
                ScriptMetadataInspector.ValidateWriteMode(payload.ScriptPath, payload.WriteMode, runtimeReferences);

                stage = "ESAPI-Sitzung öffnen";
                using (var session = EsapiSession.Open(payload))
                {
                    stage = "Kontext auflösen";
                    var context = new ContextResolver().Resolve(session.Patient, session.CurrentUser, payload, session.ApiAssembly);
                    stage = "Skript ausführen";
                    var engine = payload.ScriptEngine == ScriptEngine.Auto ? DetectEngine(payload) : payload.ScriptEngine;
                    if (engine == ScriptEngine.EsapiEssentials)
                        new EsapiEssentialsInvoker().Invoke(payload.ScriptPath, payload.EntryType, context, runtimeReferences);
                    else if (engine == ScriptEngine.Eclipse)
                        new EclipseScriptInvoker().Invoke(payload.ScriptPath, payload.EntryType, context, runtimeReferences);
                    else
                        throw new InvalidOperationException("The script engine could not be determined.");

                    stage = "Änderungen speichern";
                    var choice = payload.WriteMode == WriteMode.ConfirmSave ? saveChoice() : SaveChoice.Discard;
                    if (SaveDecision.Decide(true, payload.WriteMode, choice) == SaveAction.SaveOnce)
                        session.SaveModifications();
                }
            }
            catch (Exception exception)
            {
                if (!exception.Data.Contains(StageDataKey)) exception.Data[StageDataKey] = stage;
                throw;
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

        private static IEnumerable<string> RuntimeReferences(ContextLaunchPayload payload)
        {
            return new[] { payload.ApiAssemblyPath, payload.TypesAssemblyPath }
                .Concat(payload.ExtraReferencePaths ?? new List<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
        }
    }
}
