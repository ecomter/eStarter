using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using eStarter.Services;

namespace eStarter.Views.Settings
{
    public partial class UpdatePanel : UserControl
    {
        public UpdatePanel()
        {
            InitializeComponent();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var result = ModernMsgBox.ShowMessage(
                "This will terminate all running processes and remove all app data and settings. Are you sure?",
                "Reset App Data",
                MessageBoxButton.YesNo,
                Window.GetWindow(this));

            if (result == true)
            {
                try
                {
                    // Terminate all running processes through the kernel
                    if (KernelService.Instance.IsRunning)
                    {
                        var processes = KernelService.Instance.Kernel.GetAllProcesses();
                        foreach (var process in processes)
                        {
                            KernelService.Instance.Kernel.UnregisterProcess(process.AppId);
                        }
                        System.Diagnostics.Debug.WriteLine($"[UpdatePanel] Terminated {processes.Length} processes via kernel");
                    }

                    var baseDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "eStarter");

                    if (Directory.Exists(baseDir))
                    {
                        Directory.Delete(baseDir, true);
                    }

                    ModernMsgBox.ShowMessage(
                        "App data has been reset. The app will now restart.",
                        "Reset Complete",
                        MessageBoxButton.OK,
                        Window.GetWindow(this));

                    // Restart the application
                    var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        System.Diagnostics.Process.Start(exePath);
                        Application.Current.Shutdown();
                    }
                }
                catch (Exception ex)
                {
                    ModernMsgBox.ShowMessage(
                        $"Failed to reset: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        Window.GetWindow(this));
                }
            }
        }
    }
}
