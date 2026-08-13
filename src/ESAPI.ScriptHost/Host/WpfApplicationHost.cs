using System;
using System.Threading;
using System.Windows;

namespace EsapiScriptHost.Host
{
    internal static class WpfApplicationHost
    {
        private static Application ownedApplication;

        public static void EnsureCurrent()
        {
            if (Application.Current != null) return;
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
                throw new InvalidOperationException("The ESAPI Script Host requires an STA thread for UI scripts.");

            ownedApplication = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
        }
    }
}
