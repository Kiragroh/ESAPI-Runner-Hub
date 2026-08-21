using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using VMS.TPS.Common.Model.API;

namespace EsapiScriptHost.Host
{
    public sealed class EclipseScriptInvoker
    {
        public void Invoke(string scriptPath, string entryTypeName, ResolvedContext context, IEnumerable<string> extraReferences = null)
        {
            using (var scope = new ScriptAssemblyScope(scriptPath, extraReferences))
            {
                var assembly = scope.Load(scriptPath);
                var entryType = ResolveEntryType(assembly, entryTypeName);
                var scriptContext = new Eclipse18ScriptContextAdapter().Create(context);
                var methods = entryType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(method => method.Name == "Execute")
                    .Where(method => method.GetParameters().Length == 1 || method.GetParameters().Length == 2)
                    .Where(method => method.GetParameters()[0].ParameterType == scriptContext.GetType())
                    .OrderBy(method => method.GetParameters().Length)
                    .ToList();
                if (methods.Count == 0)
                {
                    var candidates = entryType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .Where(method => method.Name == "Execute")
                        .Select(method => string.Join(", ", method.GetParameters().Select(parameter =>
                            parameter.ParameterType.AssemblyQualifiedName + " @ " + parameter.ParameterType.Assembly.Location +
                            " # " + parameter.ParameterType.Module.ModuleVersionId)))
                        .ToArray();
                    throw new MissingMethodException(entryType.FullName,
                        "Execute (host context " + scriptContext.GetType().AssemblyQualifiedName + " @ " +
                        scriptContext.GetType().Assembly.Location + " # " + scriptContext.GetType().Module.ModuleVersionId +
                        "; candidates " + string.Join(" | ", candidates) + ")");
                }
                var instance = Activator.CreateInstance(entryType);
                var selected = methods[0];
                if (selected.GetParameters().Length == 1)
                {
                    var execute = (Action<ScriptContext>)Delegate.CreateDelegate(
                        typeof(Action<ScriptContext>), instance, selected, true);
                    execute(scriptContext);
                }
                else if (selected.GetParameters()[1].ParameterType == typeof(Window))
                {
                    var window = new Window();
                    var execute = (Action<ScriptContext, Window>)Delegate.CreateDelegate(
                        typeof(Action<ScriptContext, Window>), instance, selected, true);
                    execute(scriptContext, window);
                    if (!window.IsVisible && window.Content != null) window.ShowDialog();
                }
                else throw new InvalidOperationException("The Eclipse Execute overload has an unsupported second parameter.");
            }
        }

        private static Type ResolveEntryType(Assembly assembly, string entryTypeName)
        {
            if (!string.IsNullOrWhiteSpace(entryTypeName)) return assembly.GetType(entryTypeName, true, false);
            var matches = assembly.GetExportedTypes().Where(type => type.GetMethods().Any(method => method.Name == "Execute")).Take(2).ToList();
            if (matches.Count != 1) throw new InvalidOperationException("The Eclipse script entry type is ambiguous.");
            return matches[0];
        }
    }
}
