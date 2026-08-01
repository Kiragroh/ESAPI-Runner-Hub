using System;
using System.Windows;

namespace EsapiRunnerHub
{
    public partial class MainWindow : Window
    {
        public MainWindow()
            : this(Array.Empty<string>())
        {
        }

        public MainWindow(string[] arguments)
        {
            InitializeComponent();
        }
    }
}

