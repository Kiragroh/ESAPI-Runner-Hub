using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;

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
                if (methods.Count == 0) throw new MissingMethodException(entryType.FullName, "Execute");
                var instance = Activator.CreateInstance(entryType);
                var selected = methods[0];
                if (selected.GetParameters().Length == 1) selected.Invoke(instance, new[] { scriptContext });
                else if (typeof(Window).IsAssignableFrom(selected.GetParameters()[1].ParameterType))
                    selected.Invoke(instance, new object[] { scriptContext, new Window() });
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
