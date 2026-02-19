using System;
using System.Windows;
using System.Windows.Media;
using eStarter.Core.Hosting;

namespace eStarter.Views
{
    public partial class WasmViewerWindow : Window
    {
        private readonly WasmAppHost _host;
        private bool _closedByHost;

        public WasmViewerWindow(WasmAppHost host, string appName, int memoryLimitMb)
        {
            InitializeComponent();

            _host = host;
            TitleText.Text = appName;
            SubtitleText.Text = $"{host.AppId}  ·  eStarter WASM Sandbox";
            MemoryText.Text = memoryLimitMb > 0 ? $"{memoryLimitMb} MB" : "Unlimited";

            _host.LogReceived   += OnLogReceived;
            _host.StateChanged  += OnStateChanged;
            _host.Exited        += OnHostExited;

            SetStatus(AppHostState.Starting);
        }

        private void OnStateChanged(AppHostState state)
        {
            Dispatcher.BeginInvoke(() => SetStatus(state));
        }

        private void OnLogReceived(string message)
        {
            Dispatcher.BeginInvoke(() =>
            {
                LogOutput.Inlines.Add(
                    new System.Windows.Documents.Run(message + "\n"));
                LogScroller.ScrollToEnd();
            });
        }

        private void OnHostExited(object? sender, AppHostExitedEventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                SetStatus(e.Exception == null ? AppHostState.Stopped : AppHostState.Faulted);
                StopButton.IsEnabled = false;
                if (e.Exception != null)
                    AppendLog($"[Error] {e.Exception.Message}");
                AppendLog($"[Exited] code={e.ExitCode}");
            });
        }

        private void SetStatus(AppHostState state)
        {
            (StatusText.Text, StatusDot.Fill) = state switch
            {
                AppHostState.Starting => ("Starting…", new SolidColorBrush(Colors.Gold)),
                AppHostState.Running  => ("Running",   new SolidColorBrush(Color.FromRgb(0, 177, 89))),
                AppHostState.Stopping => ("Stopping…", new SolidColorBrush(Colors.Gold)),
                AppHostState.Stopped  => ("Stopped",   new SolidColorBrush(Color.FromRgb(136, 136, 136))),
                AppHostState.Faulted  => ("Faulted",   new SolidColorBrush(Color.FromRgb(232, 17, 35))),
                _                     => ("Unknown",   new SolidColorBrush(Colors.Gray))
            };
        }

        private void AppendLog(string message)
        {
            LogOutput.Inlines.Add(
                new System.Windows.Documents.Run(message + "\n")
                { Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)) });
            LogScroller.ScrollToEnd();
        }

        private async void Stop_Click(object sender, RoutedEventArgs e)
        {
            StopButton.IsEnabled = false;
            SetStatus(AppHostState.Stopping);
            await _host.StopAsync();
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            LogOutput.Inlines.Clear();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _host.LogReceived  -= OnLogReceived;
            _host.StateChanged -= OnStateChanged;
            _host.Exited       -= OnHostExited;
        }
    }
}
