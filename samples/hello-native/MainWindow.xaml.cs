using System;
using System.Windows;
using System.Windows.Media;
using eStarter.Sdk;

namespace HelloNative;

public partial class MainWindow : Window
{
    private EStarterClient? _client;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _client = App.Client;

        if (_client == null)
        {
            ModeText.Text = "Mode: standalone (no host)";
            ConnectedText.Text = "Status: not connected";
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(232, 17, 35));
            Log("⚠ No eStarter host detected.");
            Log("  Launch this app from eStarter to enable API calls.");
            return;
        }

        ModeText.Text = $"Mode: {(_client.IsHostedMode ? "hosted (stdio JSON-RPC)" : "pipe")}";
        ConnectedText.Text = $"Status: {(_client.IsConnected ? "connected" : "disconnected")}";
        StatusDot.Fill = _client.IsConnected
            ? new SolidColorBrush(Color.FromRgb(0, 177, 89))
            : new SolidColorBrush(Color.FromRgb(232, 17, 35));

        if (!_client.IsConnected)
        {
            Log("⚠ Not connected to eStarter host.");
            return;
        }

        Log("✅ Connected to eStarter host.");
        Log($"   AppId: {_client.AppId}");
        Log("");

        // Auto-run initial diagnostics.
        await RunPingAsync();
        await RunTimeAsync();
        await RunPermissionCheckAsync();
    }

    private async void Ping_Click(object sender, RoutedEventArgs e)
    {
        if (_client?.IsConnected != true) { Log("❌ Not connected."); return; }
        await RunPingAsync();
    }

    private async void RequestPerm_Click(object sender, RoutedEventArgs e)
    {
        if (_client?.IsConnected != true) { Log("❌ Not connected."); return; }
        await RunRequestPermissionAsync();
    }

    private async void SysInfo_Click(object sender, RoutedEventArgs e)
    {
        if (_client?.IsConnected != true) { Log("❌ Not connected."); return; }
        await RunSystemInfoAsync();
    }

    // ── API calls ─────────────────────────────────────────────────────

    private async Task RunPingAsync()
    {
        Log("── Ping ──");
        var ok = await _client!.System.PingAsync();
        Log(ok ? "  🏓 Pong! Host is alive." : "  ❌ Ping failed.");
        Log("");
    }

    private async Task RunTimeAsync()
    {
        Log("── Server Time ──");
        var time = await _client!.System.GetTimeAsync();
        if (time.Success)
        {
            var dt = DateTimeOffset.FromUnixTimeMilliseconds(time.Data).LocalDateTime;
            Log($"  🕐 {dt:yyyy-MM-dd HH:mm:ss}");
        }
        else
        {
            Log($"  ❌ {time.Error}");
        }
        Log("");
    }

    private async Task RunPermissionCheckAsync()
    {
        Log("── Current Permissions ──");
        string[] perms = ["FileRead", "FileWrite", "Notification", "SystemInfo", "NetworkAccess", "Clipboard"];
        foreach (var p in perms)
        {
            var has = await _client!.Permissions.HasAsync(p);
            Log($"  {(has ? "✅" : "🚫")} {p}");
        }
        Log("");
    }

    private async Task RunRequestPermissionAsync()
    {
        Log("── Request Permission: Clipboard ──");

        // Check before.
        var before = await _client!.Permissions.HasAsync("Clipboard");
        Log($"  Before: {(before ? "already granted" : "not granted")}");

        // Request.
        var granted = await _client.Permissions.RequestAsync("Clipboard");
        Log($"  Request result: {(granted ? "✅ granted" : "❌ denied")}");

        // Check after.
        var after = await _client.Permissions.HasAsync("Clipboard");
        Log($"  After: {(after ? "✅ granted" : "🚫 not granted")}");

        Log("");

        // Also request NetworkAccess.
        Log("── Request Permission: NetworkAccess ──");
        var netBefore = await _client.Permissions.HasAsync("NetworkAccess");
        Log($"  Before: {(netBefore ? "already granted" : "not granted")}");

        var netGranted = await _client.Permissions.RequestAsync("NetworkAccess");
        Log($"  Request result: {(netGranted ? "✅ granted" : "❌ denied")}");

        var netAfter = await _client.Permissions.HasAsync("NetworkAccess");
        Log($"  After: {(netAfter ? "✅ granted" : "🚫 not granted")}");

        Log("");
    }

    private async Task RunSystemInfoAsync()
    {
        Log("── System Info ──");
        var info = await _client!.System.GetInfoAsync();
        if (info.Success && info.Data != null)
        {
            Log($"  OS:       {info.Data.Os}");
            Log($"  Version:  {info.Data.Version}");
            Log($"  Procs:    {info.Data.ProcessCount}");
            Log($"  Uptime:   {TimeSpan.FromSeconds(info.Data.UptimeSeconds):hh\\:mm\\:ss}");
        }
        else
        {
            Log($"  ❌ {info.Error}");
        }
        Log("");
    }

    // ── Logging ───────────────────────────────────────────────────────

    private void Log(string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => Log(message));
            return;
        }

        LogOutput.Inlines.Add(new System.Windows.Documents.Run(message + "\n"));
        LogScroller.ScrollToEnd();

        // Also forward to stderr for ProcessHost's ErrorDataReceived.
        Console.Error.WriteLine($"[HelloNative] {message}");
    }
}
