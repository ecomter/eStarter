using System;
using System.Threading.Tasks;
using System.Windows;
using eStarter.Sdk;

namespace HelloNative;

public partial class App : Application
{
    internal static EStarterClient? Client { get; private set; }

    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        var appId = Environment.GetEnvironmentVariable("ESTARTER_APP_ID") ?? "hello.native";

        Client = new EStarterClient(appId, "1.0.0");
        await Client.ConnectAsync();

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Client?.Dispose();
        base.OnExit(e);
    }
}
