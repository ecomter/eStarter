using System.Configuration;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using eStarter.Services;
using eStarter.Views;

namespace eStarter;
/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        // Show Splash Screen
        var splash = new eStarter.Views.SplashScreen();
        splash.Show();

        // Initialize Kernel Service
        await Task.Run(() =>
        {
            KernelService.Instance.Start();
        });

        // Additional initialization delay
        await Task.Delay(2000);

        // Show Main Window
        var mainWindow = new MainWindow();
        Application.Current.MainWindow = mainWindow;
        mainWindow.Show();

        // Close Splash Screen
        splash.Close();

        // Show welcome notification
        await KernelService.Instance.Notifications.ShowAsync(
            "eStarter", 
            "System ready",
            NotificationType.Success,
            3000);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Cleanup Kernel Service
        KernelService.Instance.Dispose();
        base.OnExit(e);
    }
}

