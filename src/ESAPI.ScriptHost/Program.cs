using System;
using System.Reflection;
using System.Windows;
using EsapiScriptHost.Contracts;
using EsapiScriptHost.Host;

namespace EsapiScriptHost
{
    internal static class Program
    {
        [STAThread]
        private static int Main()
        {
            ContextLaunchPayload payload = null;
            try
            {
                var encoded = Environment.GetEnvironmentVariable(ContextLaunchPayload.EnvironmentKey);
                Environment.SetEnvironmentVariable(ContextLaunchPayload.EnvironmentKey, null);
                payload = ContextLaunchPayload.Decode(encoded);
                ScriptHostCapability.Validate(ScriptHostCapability.Current, payload.WriteMode);
                if (payload.WriteMode != WriteMode.ReadOnly)
                {
                    var behavior = payload.WriteMode == WriteMode.ConfirmSave
                        ? "After successful completion, you will be asked separately whether to save the changes."
                        : "Any in-memory changes will be discarded automatically after the script finishes.";
                    var start = MessageBox.Show(
                        "This script may modify the open patient. " + behavior,
                        HostTitle, MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                    if (start != MessageBoxResult.OK) return 2;
                }

                new ScriptHostApplication().Run(payload, AskForSave);
                return 0;
            }
            catch (Exception exception)
            {
                var stage = exception.Data[ScriptHostApplication.StageDataKey] as string ?? "Unknown";
                var root = HostTechnicalLog.RootException(exception);
                var reasonCode = HostTechnicalLog.ReasonCode(exception, stage);
                HostTechnicalLog.WriteBestEffort(payload == null ? null : payload.LogDirectory, stage,
                    payload == null ? string.Empty : payload.ApplicationId, reasonCode, exception);
                MessageBox.Show(
                    "The script ended without saving.\n\nFailure phase: " + stage +
                    "\nFailure code: " + reasonCode + "\nFailure type: " + root.GetType().Name +
                    "\n\nTechnical details were recorded.",
                    HostTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return 10;
            }
        }

        private static SaveChoice AskForSave()
        {
            var result = MessageBox.Show(
                "The script completed normally. Save the changes permanently?\n\nNo or Cancel discards the changes.",
                HostTitle, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning,
                MessageBoxResult.No);
            return result == MessageBoxResult.Yes ? SaveChoice.Save : SaveChoice.Discard;
        }

        private static string HostTitle
        {
            get
            {
                return ScriptHostCapability.Current == ScriptHostKind.WriteEnabled
                    ? "ESAPI Write Script Host"
                    : "ESAPI Script Host";
            }
        }
    }
}
