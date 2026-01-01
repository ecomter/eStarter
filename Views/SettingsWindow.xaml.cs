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

        private string GetString(string key)
        {
            return Application.Current.Resources[key] as string ?? key;
        }

        private async void ResetAppData_Click(object sender, RoutedEventArgs e)
        {
            var manager = new AppManager(new Services.AppInstaller());
            var result = ModernMsgBox.ShowMessage(GetString("Str_ResetAppDataPrompt"), GetString("Str_ConfirmTitle"), MessageBoxButton.YesNo, this);
            if (result == true)
            {
                try
                {
                    // delete the apps directory
                    var appsDir = manager.AppsDirectory;
                    if (System.IO.Directory.Exists(appsDir))
                    {
                        await System.Threading.Tasks.Task.Run(() => System.IO.Directory.Delete(appsDir, true));
                        ModernMsgBox.ShowMessage(GetString("Str_AppDataRemoved"), GetString("Str_SuccessTitle"), MessageBoxButton.OK, this);
                    }
                    else
                    {
                        ModernMsgBox.ShowMessage(GetString("Str_NoAppDataFound"), GetString("Str_InfoTitle"), MessageBoxButton.OK, this);
                    }
                }
                catch (System.Exception ex)
                {
                    ModernMsgBox.ShowMessage(string.Format(GetString("Str_ResetFailed"), ex.Message), GetString("Str_ErrorTitle"), MessageBoxButton.OK, this);
                }
            }
        }
    }
}
