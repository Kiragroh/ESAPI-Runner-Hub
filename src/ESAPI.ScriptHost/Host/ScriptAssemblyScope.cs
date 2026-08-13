using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace EsapiScriptHost.Host
{
    internal sealed class ScriptAssemblyScope : IDisposable
    {
        private readonly string scriptDirectory;
        private readonly IList<string> references;
        private readonly ResolveEventHandler resolver;

        public ScriptAssemblyScope(string scriptPath, IEnumerable<string> extraReferences = null)
        {
            scriptDirectory = Path.GetDirectoryName(Path.GetFullPath(scriptPath));
            references = (extraReferences ?? Enumerable.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                .Select(Path.GetFullPath)
                .ToList();
            resolver = Resolve;
            AppDomain.CurrentDomain.AssemblyResolve += resolver;
            foreach (var reference in references) Assembly.LoadFrom(reference);
        }

        public Assembly Load(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("The configured script assembly was not found.");
            return Assembly.LoadFrom(Path.GetFullPath(path));
        }

        public void Dispose()
        {
            AppDomain.CurrentDomain.AssemblyResolve -= resolver;
        }

        private Assembly Resolve(object sender, ResolveEventArgs arguments)
        {
            var requested = new AssemblyName(arguments.Name).Name;
            var explicitReference = references.FirstOrDefault(path =>
                string.Equals(Path.GetFileNameWithoutExtension(path), requested, StringComparison.OrdinalIgnoreCase));
            if (explicitReference != null) return Assembly.LoadFrom(explicitReference);
            var local = Path.Combine(scriptDirectory, requested + ".dll");
            return File.Exists(local) ? Assembly.LoadFrom(local) : null;
        }
    }
}
