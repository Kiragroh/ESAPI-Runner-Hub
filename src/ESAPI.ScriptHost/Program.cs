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
            try
            {
                var encoded = Environment.GetEnvironmentVariable(ContextLaunchPayload.EnvironmentKey);
                Environment.SetEnvironmentVariable(ContextLaunchPayload.EnvironmentKey, null);
                var payload = ContextLaunchPayload.Decode(encoded);
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
                var root = exception is TargetInvocationException && exception.InnerException != null
                    ? exception.InnerException
                    : exception;
                MessageBox.Show(
                    "Das Skript wurde ohne Speichern beendet.\n\nFehlerphase: " + stage + "\nFehlertyp: " + root.GetType().Name,
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
