using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
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
        // Catch any unhandled exceptions during startup
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            System.Diagnostics.Debug.WriteLine($"[FATAL] Unhandled: {ex}");
            MessageBox.Show(ex?.ToString() ?? "Unknown fatal error",
                "eStarter Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (s, args) =>
        {
            System.Diagnostics.Debug.WriteLine($"[FATAL] Dispatcher: {args.Exception}");
            MessageBox.Show(args.Exception.ToString(),
                "eStarter Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            System.Diagnostics.Debug.WriteLine($"[FATAL] Task: {args.Exception}");
        };

        try
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FATAL] Startup failed: {ex}");
            MessageBox.Show($"Startup failed:\n\n{ex}",
                "eStarter Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Cleanup Kernel Service
        KernelService.Instance.Dispose();
        base.OnExit(e);
    }
}

