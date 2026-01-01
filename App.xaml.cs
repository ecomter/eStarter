using System.Configuration;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
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

        // Simulate loading / initialization
        // In a real app, you would load settings, check for updates, etc. here
        await Task.Delay(3000);

        // Show Main Window
        var mainWindow = new MainWindow();
        Application.Current.MainWindow = mainWindow; // Fix: Set as main window so ViewModels can find it
        mainWindow.Show();

        // Close Splash Screen
        splash.Close();
    }
}

