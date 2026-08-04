using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace EsapiScriptHost.Host
{
    public sealed class Eclipse18ScriptContextAdapter
    {
        private static readonly Version SupportedApiVersion = new Version(1, 0, 600, 194);

        public object Create(ResolvedContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var apiAssembly = context.ApiAssembly ?? (context.Patient == null ? null : context.Patient.GetType().Assembly);
            if (apiAssembly == null) throw new InvalidOperationException("The Eclipse API assembly is unavailable.");
            if (apiAssembly.GetName().Version != SupportedApiVersion)
                throw new InvalidOperationException("The classic context adapter supports only Eclipse 18 API 1.0.600.194.");
            var scriptContextType = apiAssembly.GetType("VMS.TPS.Common.Model.API.ScriptContext", true, false);
            var constructor = scriptContextType.GetConstructor(new[] { typeof(object), typeof(object), typeof(object), typeof(string) });
            if (constructor == null) throw new InvalidOperationException("The Eclipse 18 ScriptContext constructor is unavailable.");
            var scriptContext = constructor.Invoke(new object[] { null, context.CurrentUser, null, "ESAPI Runner Hub" });
            SetField(scriptContext, "m_user", context.CurrentUser);
            SetField(scriptContext, "m_course", context.Course);
            SetField(scriptContext, "m_image", context.Image);
            SetField(scriptContext, "m_patient", context.Patient);
            SetField(scriptContext, "m_planSetup", context.Plan);
            SetListField(scriptContext, "m_planSetups", context.PlansInScope);
            SetListField(scriptContext, "m_planSums", context.PlanSumsInScope);
            SetField(scriptContext, "m_planSum", context.PlanSum);
            SetField(scriptContext, "m_structureSet", context.StructureSet);
            return scriptContext;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) throw new InvalidOperationException("The Eclipse 18 ScriptContext field " + fieldName + " is unavailable.");
            if (value != null && !field.FieldType.IsInstanceOfType(value))
                throw new InvalidOperationException("The Eclipse 18 ScriptContext field " + fieldName + " has changed type.");
            field.SetValue(target, value);
        }

        private static void SetListField(object target, string fieldName, IEnumerable<object> values)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null || !field.FieldType.IsGenericType)
                throw new InvalidOperationException("The Eclipse 18 ScriptContext field " + fieldName + " is unavailable.");
            var itemType = field.FieldType.GetGenericArguments()[0];
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
            foreach (var value in values ?? new object[0])
            {
                if (!itemType.IsInstanceOfType(value))
                    throw new InvalidOperationException("The Eclipse 18 scope field " + fieldName + " has changed type.");
                list.Add(value);
            }
            field.SetValue(target, list);
        }
    }
}
