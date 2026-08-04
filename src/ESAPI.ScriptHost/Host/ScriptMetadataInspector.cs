using System;
using System.Linq;
using System.Reflection;
using EsapiRunnerHub.Configuration;

namespace EsapiScriptHost.Host
{
    public static class ScriptMetadataInspector
    {
        public static bool IsWriteable(string scriptPath)
        {
            using (var scope = new ScriptAssemblyScope(scriptPath))
            {
                var assembly = scope.Load(scriptPath);
                var attribute = CustomAttributeData.GetCustomAttributes(assembly).FirstOrDefault(item =>
                    string.Equals(item.AttributeType.Name, "ESAPIScriptAttribute", StringComparison.Ordinal));
                if (attribute == null) return false;
                var named = attribute.NamedArguments.FirstOrDefault(argument => argument.MemberName == "IsWriteable");
                return named.TypedValue.Value is bool && (bool)named.TypedValue.Value;
            }
        }

        public static void ValidateWriteMode(string scriptPath, WriteMode writeMode)
        {
            var writeable = IsWriteable(scriptPath);
            if (writeable && writeMode != WriteMode.ConfirmSave)
                throw new InvalidOperationException("The script is write-enabled but save confirmation is not configured.");
            if (!writeable && writeMode == WriteMode.ConfirmSave)
                throw new InvalidOperationException("Save confirmation requires explicit write-enabled script metadata.");
        }
    }
}
