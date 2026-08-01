using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using EsapiRunnerHub.Privacy;

namespace EsapiRunnerHub
{
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var window = new MainWindow(e.Args ?? Array.Empty<string>());
            MainWindow = window;
            window.Show();
        }

        private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            WriteCrash(exception, "domain_unhandled_exception");
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            WriteCrash(e.Exception, "task_unobserved_exception");
            e.SetObserved();
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var report = WriteCrash(e.Exception, "ui_unhandled_exception");
            MessageBox.Show("An unexpected error was isolated. The Hub can remain open.\n\nTechnical report:\n" + report,
                "ESAPI Runner Hub", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private static string WriteCrash(Exception exception, string eventCode)
        {
            try
            {
                TechnicalLog.Current.Write("ERROR", eventCode, string.Empty, exception);
                return new CrashReporter(Path.GetDirectoryName(TechnicalLog.Current.FilePath)).Write(exception);
            }
            catch (Exception)
            {
                return "Technical report could not be written.";
            }
        }
    }
}
