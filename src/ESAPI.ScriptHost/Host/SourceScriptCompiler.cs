using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using Microsoft.CSharp;

namespace EsapiScriptHost.Host
{
    public sealed class SourceScriptCompiler
    {
        private readonly string cacheDirectory;

        public SourceScriptCompiler()
            : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ESAPI Runner Hub", "ScriptCache"))
        {
        }

        public SourceScriptCompiler(string cacheDirectory)
        {
            this.cacheDirectory = Path.GetFullPath(cacheDirectory);
        }

        public string Compile(string sourcePath, string apiAssemblyPath, string typesAssemblyPath, IEnumerable<string> extraReferences)
        {
            sourcePath = Path.GetFullPath(sourcePath);
            if (!sourcePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || !File.Exists(sourcePath))
                throw new FileNotFoundException("The configured C# source file was not found.");
            var references = BuildReferences(apiAssemblyPath, typesAssemblyPath, extraReferences).ToList();
            foreach (var reference in references)
                if (!File.Exists(reference)) throw new FileNotFoundException("A configured compiler reference was not found.");

            Directory.CreateDirectory(cacheDirectory);
            var hash = ComputeHash(sourcePath, references);
            var output = Path.Combine(cacheDirectory, hash + ".esapi.dll");
            if (File.Exists(output)) return output;
            var temporary = Path.Combine(cacheDirectory, hash + "." + Guid.NewGuid().ToString("N") + ".tmp.dll");
            try
            {
                using (var provider = new CSharpCodeProvider(new Dictionary<string, string> { { "CompilerVersion", "v4.0" } }))
                {
                    var parameters = new CompilerParameters
                    {
                        GenerateExecutable = false,
                        GenerateInMemory = false,
                        IncludeDebugInformation = false,
                        TreatWarningsAsErrors = false,
                        OutputAssembly = temporary,
                        CompilerOptions = "/target:library /platform:x64 /optimize"
                    };
                    foreach (var reference in references) parameters.ReferencedAssemblies.Add(reference);
                    var result = provider.CompileAssemblyFromFile(parameters, sourcePath);
                    if (result.Errors.HasErrors)
                    {
                        var diagnostics = result.Errors.Cast<CompilerError>().Where(error => !error.IsWarning)
                            .Take(8).Select(error => error.ErrorNumber + " line " + error.Line + ", column " + error.Column);
                        throw new InvalidOperationException("Script compilation failed: " + string.Join("; ", diagnostics));
                    }
                }

                if (!File.Exists(output)) File.Move(temporary, output);
                return output;
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private static IEnumerable<string> BuildReferences(string api, string types, IEnumerable<string> extras)
        {
            return new[]
                {
                    typeof(object).Assembly.Location,
                    typeof(Uri).Assembly.Location,
                    typeof(Enumerable).Assembly.Location,
                    typeof(Window).Assembly.Location,
                    typeof(System.Windows.Media.Color).Assembly.Location,
                    typeof(System.Windows.DependencyObject).Assembly.Location,
                    typeof(System.Xml.XmlDocument).Assembly.Location,
                    typeof(System.Xaml.XamlReader).Assembly.Location,
                    typeof(System.Windows.Forms.DialogResult).Assembly.Location,
                    api,
                    types
                }
                .Concat(extras ?? Enumerable.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static string ComputeHash(string sourcePath, IEnumerable<string> references)
        {
            using (var sha = SHA256.Create())
            using (var stream = new MemoryStream())
            {
                WriteFile(stream, sourcePath);
                foreach (var reference in references.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)) WriteFile(stream, reference);
                stream.Position = 0;
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static void WriteFile(Stream target, string path)
        {
            var name = Encoding.UTF8.GetBytes(Path.GetFileName(path).ToLowerInvariant());
            target.Write(name, 0, name.Length);
            var bytes = File.ReadAllBytes(path);
            target.Write(bytes, 0, bytes.Length);
        }
    }
}
