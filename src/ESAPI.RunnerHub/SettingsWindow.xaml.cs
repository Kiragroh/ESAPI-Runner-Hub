using System;
using System.Windows;
using EsapiRunnerHub.Configuration;
using EsapiRunnerHub.ViewModels;
using Microsoft.Win32;

namespace EsapiRunnerHub
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel viewModel;

        public SettingsWindow(HubConfiguration configuration, string settingsPath)
        {
            InitializeComponent();
            viewModel = new SettingsViewModel(configuration, settingsPath);
            DataContext = viewModel;
        }

        public string SavedSettingsPath { get { return viewModel.SettingsPath; } }

        private void Add_Click(object sender, RoutedEventArgs e) { viewModel.AddApplication(); }
        private void Delete_Click(object sender, RoutedEventArgs e) { viewModel.DeleteSelectedApplication(); }

        private void BrowseApi_Click(object sender, RoutedEventArgs e)
        {
            var path = BrowseFile("ESAPI API assembly|VMS.TPS.Common.Model.API.dll|DLL files|*.dll");
            if (path != null) viewModel.AssignApiAssembly(path);
        }

        private void BrowseTypes_Click(object sender, RoutedEventArgs e)
        {
            var path = BrowseFile("ESAPI Types assembly|VMS.TPS.Common.Model.Types.dll|DLL files|*.dll");
            if (path != null) viewModel.AssignTypesAssembly(path);
        }

        private void BrowseExecutable_Click(object sender, RoutedEventArgs e)
        {
            var path = BrowseFile("Supported targets|*.exe;*.esapi.dll;*.cs|Executable applications|*.exe|Eclipse plug-ins|*.esapi.dll;*.cs");
            if (path != null) viewModel.AssignExecutable(path);
        }

        private void BrowseDirectory_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Select application working directory";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    viewModel.AssignWorkingDirectory(dialog.SelectedPath);
                }
            }
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog { Filter = "INI settings|*.ini", FileName = "settings.ini", OverwritePrompt = true };
            if (dialog.ShowDialog(this) == true)
            {
                viewModel.SettingsPath = dialog.FileName;
                Save();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e) { Save(); }

        private void Save()
        {
            string error;
            if (!viewModel.TrySave(out error))
            {
                MessageBox.Show(this, error, "Settings not saved", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
        }

        private string BrowseFile(string filter)
        {
            var dialog = new OpenFileDialog { Filter = filter, CheckFileExists = true };
            return dialog.ShowDialog(this) == true ? dialog.FileName : null;
        }
    }
}
