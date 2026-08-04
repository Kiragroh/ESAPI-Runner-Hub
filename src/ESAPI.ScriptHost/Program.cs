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
                if (payload.WriteMode == WriteMode.ConfirmSave)
                {
                    var start = MessageBox.Show(
                        "Dieses Skript darf den geöffneten Patienten verändern. Nach erfolgreichem Abschluss wird separat gefragt, ob gespeichert werden soll.",
                        "ESAPI Script Host", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                    if (start != MessageBoxResult.OK) return 2;
                }

                new ScriptHostApplication().Run(payload, AskForSave);
                return 0;
            }
            catch (Exception exception)
            {
                var stage = exception.Data[ScriptHostApplication.StageDataKey] as string ?? "Unbekannt";
                var root = HostTechnicalLog.RootException(exception);
                var reasonCode = HostTechnicalLog.ReasonCode(exception, stage);
                HostTechnicalLog.WriteBestEffort(payload == null ? null : payload.LogDirectory, stage,
                    payload == null ? string.Empty : payload.ApplicationId, reasonCode, exception);
                MessageBox.Show(
                    "Das Skript wurde ohne Speichern beendet.\n\nFehlerphase: " + stage +
                    "\nFehlercode: " + reasonCode + "\nFehlertyp: " + root.GetType().Name +
                    "\n\nTechnische Details wurden protokolliert.",
                    "ESAPI Script Host", MessageBoxButton.OK, MessageBoxImage.Error);
                return 10;
            }
        }

        private static SaveChoice AskForSave()
        {
            var result = MessageBox.Show(
                "Das Skript wurde normal beendet. Änderungen jetzt dauerhaft speichern?\n\nNein oder Abbrechen verwirft die Änderungen.",
                "ESAPI Script Host", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning,
                MessageBoxResult.No);
            return result == MessageBoxResult.Yes ? SaveChoice.Save : SaveChoice.Discard;
        }
    }
}
