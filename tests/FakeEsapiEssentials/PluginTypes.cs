using System.Collections.Generic;
using System.Windows;
using VMS.TPS.Common.Model.API;

namespace EsapiEssentials.Plugin
{
    public abstract class ScriptBase
    {
        public abstract void Run(PluginScriptContext context);
    }

    public abstract class ScriptBaseWithWindow
    {
        public abstract void Execute(PluginScriptContext context, Window window);
    }

    public sealed class PluginScriptContext
    {
        public User CurrentUser { get; set; }
        public Course Course { get; set; }
        public Image Image { get; set; }
        public StructureSet StructureSet { get; set; }
        public Patient Patient { get; set; }
        public PlanSetup PlanSetup { get; set; }
        public PlanSum PlanSum { get; set; }
        public ExternalPlanSetup ExternalPlanSetup { get; set; }
        public IEnumerable<PlanSetup> PlansInScope { get; set; }
        public IEnumerable<ExternalPlanSetup> ExternalPlansInScope { get; set; }
        public IEnumerable<PlanSum> PlanSumsInScope { get; set; }
        public string ApplicationName { get; set; }
        public string VersionInfo { get; set; }
    }
}
