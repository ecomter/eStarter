using System.Windows;
using System.Reflection;

namespace eStarter.Views
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            VersionText.Text = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenLicense(object sender, RoutedEventArgs e)
        {
            // could open a license file; placeholder
            ModernMsgBox.ShowMessage("MIT License\n\nThis is a placeholder.", "License", MessageBoxButton.OK, this);
        }
    }
}
