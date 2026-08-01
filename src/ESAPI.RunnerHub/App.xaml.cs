using System;
using System.Windows;

namespace EsapiRunnerHub
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var window = new MainWindow(e.Args ?? Array.Empty<string>());
            MainWindow = window;
            window.Show();
        }
    }
}

