using System;
using System.IO;
using EsapiScriptHost.Host;

namespace EsapiRunnerHub.Tests
{
    internal static class SourceScriptCompilerTests
    {
        public static void Register()
        {
            TestHarness.Test("source compiler caches by source and references", CachesByContent);
            TestHarness.Test("source compiler diagnostics omit source content", SanitizesDiagnostics);
            TestHarness.Test("source compiler includes standard XML and XAML references", IncludesStandardScriptReferences);
        }

        private static void CachesByContent()
        {
            var directory = NewDirectory();
            try
            {
                var source = Path.Combine(directory, "Tool.cs");
                File.WriteAllText(source, Source("One"));
                var compiler = new SourceScriptCompiler(Path.Combine(directory, "cache"));
                var api = TestHarness.PathFromRoot("tests/FakeVms.Api/bin/x64/Debug/VMS.TPS.Common.Model.API.dll");

                var first = compiler.Compile(source, api, string.Empty, new string[0]);
                var repeated = compiler.Compile(source, api, string.Empty, new string[0]);
                File.WriteAllText(source, Source("Two"));
                var changed = compiler.Compile(source, api, string.Empty, new string[0]);

                TestHarness.AssertEqual(first, repeated);
                TestHarness.AssertFalse(string.Equals(first, changed, StringComparison.OrdinalIgnoreCase));
                TestHarness.AssertTrue(File.Exists(first));
                TestHarness.AssertTrue(File.Exists(changed));
                TestHarness.AssertFalse(File.Exists(Path.ChangeExtension(source, ".esapi.dll")));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static void SanitizesDiagnostics()
        {
            var directory = NewDirectory();
            try
            {
                var source = Path.Combine(directory, "Broken.cs");
                File.WriteAllText(source, "public class Broken { string secret = \"PATIENT-SECRET\" ");
                var api = TestHarness.PathFromRoot("tests/FakeVms.Api/bin/x64/Debug/VMS.TPS.Common.Model.API.dll");
                var exception = TestHarness.AssertThrows<InvalidOperationException>(() =>
                    new SourceScriptCompiler(Path.Combine(directory, "cache")).Compile(source, api, string.Empty, new string[0]));

                TestHarness.AssertContains(exception.Message, "CS");
                TestHarness.AssertFalse(exception.Message.Contains("PATIENT-SECRET"));
                TestHarness.AssertFalse(exception.Message.Contains(source));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static void IncludesStandardScriptReferences()
        {
            var directory = NewDirectory();
            try
            {
                var source = Path.Combine(directory, "References.cs");
                File.WriteAllText(source,
                    "using System.Xml.Serialization; using System.Windows.Markup; " +
                    "public sealed class References { public IXmlSerializable Xml { get; set; } " +
                    "public IQueryAmbient Ambient { get; set; } }");
                var api = TestHarness.PathFromRoot("tests/FakeVms.Api/bin/x64/Debug/VMS.TPS.Common.Model.API.dll");

                var output = new SourceScriptCompiler(Path.Combine(directory, "cache"))
                    .Compile(source, api, string.Empty, new string[0]);

                TestHarness.AssertTrue(File.Exists(output));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static string Source(string suffix)
        {
            return "using VMS.TPS.Common.Model.API; namespace VMS.TPS { public sealed class Script" + suffix +
                   " { public void Execute(ScriptContext context) { } } }";
        }

        private static string NewDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "runner-hub-compiler-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
