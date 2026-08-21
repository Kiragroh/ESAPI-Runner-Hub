using System;
using System.IO;
using EsapiEssentials.Plugin;
using VMS.TPS.Common.Model.API;
using System.Windows;

[assembly: ESAPIScript(IsWriteable = true)]

namespace VMS.TPS
{
    public sealed class EssentialsScript : ScriptBase
    {
        public override void Run(PluginScriptContext context)
        {
            WriteMarker("essentials", context.Patient.Id, context.PlanSetup.Id, context.StructureSet.Id);
        }

        private static void WriteMarker(params string[] values)
        {
            File.WriteAllText(Environment.GetEnvironmentVariable("SCRIPT_FIXTURE_MARKER"), string.Join("|", values));
        }
    }

    public sealed class EclipseScript
    {
        public void Execute(ScriptContext context)
        {
            File.WriteAllText(Environment.GetEnvironmentVariable("SCRIPT_FIXTURE_MARKER"),
                string.Join("|", "eclipse", context.Patient.Id, context.PlanSetup.Id, context.StructureSet.Id));
        }
    }

    public sealed class FailingEclipseScript
    {
        public void Execute(ScriptContext context)
        {
            throw new InvalidOperationException("Synthetic patient detail that must not reach host diagnostics.");
        }
    }

    public sealed class WriteEclipseScript
    {
        public void Execute(ScriptContext context)
        {
            var plan = context.Course.AddExternalPlanSetup(context.StructureSet);
            File.WriteAllText(Environment.GetEnvironmentVariable("SCRIPT_FIXTURE_MARKER"),
                string.Join("|", "write", plan.Id, plan.StructureSet.Id));
        }
    }

    public sealed class WpfApplicationAwareEclipseScript
    {
        public void Execute(ScriptContext context)
        {
            if (System.Windows.Application.Current == null)
                throw new InvalidOperationException("A WPF application host is required.");
            File.WriteAllText(Environment.GetEnvironmentVariable("SCRIPT_FIXTURE_MARKER"), "wpf-application-ready");
        }
    }

    public sealed class WindowEclipseScript
    {
        public void Execute(ScriptContext context, Window window)
        {
            window.Title = "Synthetic Eclipse window";
            window.Content = "Synthetic content";
            window.Loaded += delegate
            {
                File.WriteAllText(Environment.GetEnvironmentVariable("SCRIPT_FIXTURE_MARKER"),
                    "window-shown|" + context.Patient.Id);
                window.Close();
            };
        }
    }
}
