using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace EsapiScriptHost.Host
{
    public sealed class EsapiEssentialsInvoker
    {
        public void Invoke(string scriptPath, string entryTypeName, ResolvedContext context, IEnumerable<string> extraReferences = null)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            using (var scope = new ScriptAssemblyScope(scriptPath, extraReferences))
            {
                var assembly = scope.Load(scriptPath);
                var entryType = ResolveEntryType(assembly, entryTypeName);
                var instance = Activator.CreateInstance(entryType);
                var contextType = ResolvePluginContextType(entryType);
                var pluginContext = Activator.CreateInstance(contextType);
                SetIfPresent(pluginContext, "CurrentUser", context.CurrentUser);
                SetIfPresent(pluginContext, "Course", context.Course);
                SetIfPresent(pluginContext, "Image", context.Image);
                SetIfPresent(pluginContext, "StructureSet", context.StructureSet);
                SetIfPresent(pluginContext, "Patient", context.Patient);
                SetIfPresent(pluginContext, "PlanSetup", context.Plan);
                SetIfPresent(pluginContext, "PlanSum", context.PlanSum);
                SetIfPresent(pluginContext, "ExternalPlanSetup", IsType(context.Plan, "ExternalPlanSetup") ? context.Plan : null);
                SetIfPresent(pluginContext, "BrachyPlanSetup", IsType(context.Plan, "BrachyPlanSetup") ? context.Plan : null);
                SetIfPresent(pluginContext, "PlansInScope", context.PlansInScope);
                SetIfPresent(pluginContext, "ExternalPlansInScope", context.PlansInScope.Where(item => IsType(item, "ExternalPlanSetup")));
                SetIfPresent(pluginContext, "BrachyPlansInScope", context.PlansInScope.Where(item => IsType(item, "BrachyPlanSetup")));
                SetIfPresent(pluginContext, "PlanSumsInScope", context.PlanSumsInScope);
                SetIfPresent(pluginContext, "ApplicationName", "ESAPI Runner Hub");
                SetIfPresent(pluginContext, "VersionInfo", "Eclipse 18 context host");

                var run = entryType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(method => method.Name == "Run" && method.GetParameters().Length == 1 &&
                                              method.GetParameters()[0].ParameterType == contextType);
                if (run != null)
                {
                    run.Invoke(instance, new[] { pluginContext });
                    return;
                }

                var execute = entryType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(method => method.Name == "Execute" && method.GetParameters().Length == 2 &&
                                              method.GetParameters()[0].ParameterType == contextType &&
                                              typeof(Window).IsAssignableFrom(method.GetParameters()[1].ParameterType));
                if (execute == null) throw new MissingMethodException(entryType.FullName, "Run/Execute");
                execute.Invoke(instance, new object[] { pluginContext, new Window() });
            }
        }

        private static Type ResolveEntryType(Assembly assembly, string entryTypeName)
        {
            if (!string.IsNullOrWhiteSpace(entryTypeName))
                return assembly.GetType(entryTypeName, true, false);
            var matches = assembly.GetExportedTypes().Where(type => type.GetMethods().Any(method => method.Name == "Run" || method.Name == "Execute")).Take(2).ToList();
            if (matches.Count != 1) throw new InvalidOperationException("The EsapiEssentials entry type is ambiguous.");
            return matches[0];
        }

        private static Type ResolvePluginContextType(Type entryType)
        {
            var method = entryType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(candidate => (candidate.Name == "Run" || candidate.Name == "Execute") &&
                                             candidate.GetParameters().Length > 0 &&
                                             candidate.GetParameters()[0].ParameterType.FullName == "EsapiEssentials.Plugin.PluginScriptContext");
            if (method == null) throw new InvalidOperationException("The selected type is not an EsapiEssentials context script.");
            return method.GetParameters()[0].ParameterType;
        }

        private static void SetIfPresent(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null || !property.CanWrite) return;
            if (value == null)
            {
                property.SetValue(target, null, null);
                return;
            }
            if (property.PropertyType.IsInstanceOfType(value))
            {
                property.SetValue(target, value, null);
                return;
            }
            if (typeof(IEnumerable).IsAssignableFrom(property.PropertyType) && value is IEnumerable)
            {
                property.SetValue(target, CreateTypedList(property.PropertyType, (IEnumerable)value), null);
                return;
            }
            throw new InvalidOperationException("The EsapiEssentials context property " + propertyName + " has an incompatible type.");
        }

        private static object CreateTypedList(Type propertyType, IEnumerable values)
        {
            var itemType = propertyType.IsGenericType ? propertyType.GetGenericArguments()[0] : typeof(object);
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
            foreach (var value in values) if (value != null && itemType.IsInstanceOfType(value)) list.Add(value);
            return list;
        }

        private static bool IsType(object value, string typeName)
        {
            return value != null && string.Equals(value.GetType().Name, typeName, StringComparison.Ordinal);
        }
    }
}
