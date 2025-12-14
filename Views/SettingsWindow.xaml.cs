using System.Windows;
using eStarter.Core;

namespace eStarter.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        private async void ResetAppData_Click(object sender, RoutedEventArgs e)
        {
            var manager = new AppManager(new Services.AppInstaller());
            var result = ModernMsgBox.ShowMessage("This will remove all installed app data. Continue?", "Confirm", MessageBoxButton.YesNo, this);
            if (result == true)
            {
                try
                {
                    // delete the apps directory
                    var appsDir = manager.AppsDirectory;
                    if (System.IO.Directory.Exists(appsDir))
                    {
                        await System.Threading.Tasks.Task.Run(() => System.IO.Directory.Delete(appsDir, true));
                        ModernMsgBox.ShowMessage("All app data removed.", "Success", MessageBoxButton.OK, this);
                    }
                    else
                    {
                        ModernMsgBox.ShowMessage("No app data found.", "Info", MessageBoxButton.OK, this);
                    }
                }
                catch (System.Exception ex)
                {
                    ModernMsgBox.ShowMessage($"Failed to remove app data: {ex.Message}", "Error", MessageBoxButton.OK, this);
                }
            }
        }
    }
}
