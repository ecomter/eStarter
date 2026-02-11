using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using eStarter.Services;

namespace eStarter.Views
{
    public partial class SystemStatusBar : UserControl
    {
        private readonly DispatcherTimer _timer;

        public event Action? SettingsClicked;
        public event Action? AppsClicked;
        public event Action? NotificationsClicked;

        public SystemStatusBar()
        {
            InitializeComponent();

            // Setup clock timer
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();

            // Initial update
            UpdateClock();
            UpdateKernelStatus();

            // Subscribe to kernel events
            if (KernelService.Instance.IsRunning)
            {
                KernelService.Instance.ProcessStarted += _ => Dispatcher.BeginInvoke(UpdateProcessCount);
                KernelService.Instance.ProcessTerminated += _ => Dispatcher.BeginInvoke(UpdateProcessCount);
                
                // Subscribe to notifications
                KernelService.Instance.Notifications.ActiveNotifications.CollectionChanged += (s, e) =>
                {
                    Dispatcher.BeginInvoke(UpdateNotificationBadge);
                };
            }

            Loaded += (s, e) =>
            {
                UpdateProcessCount();
                UpdateNotificationBadge();
            };
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            UpdateClock();
        }

        private void UpdateClock()
        {
            var now = DateTime.Now;
            ClockText.Text = now.ToString("HH:mm");
            DateText.Text = now.ToString("MMM d");
        }

        private void UpdateKernelStatus()
        {
            var isRunning = KernelService.Instance.IsRunning;
            KernelStatusIndicator.Background = new SolidColorBrush(
                isRunning ? Color.FromRgb(0, 177, 89) : Color.FromRgb(229, 20, 0));
            KernelStatusIndicator.ToolTip = isRunning ? "System Running" : "System Stopped";
        }

        private void UpdateProcessCount()
        {
            var count = KernelService.Instance.Kernel.GetAllProcesses().Length;
            ProcessCountText.Text = count == 1 ? "1 app running" : $"{count} apps running";
        }

        private void UpdateNotificationBadge()
        {
            var hasNotifications = KernelService.Instance.Notifications.ActiveNotifications.Count > 0;
            NotificationBadge.Visibility = hasNotifications ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsClicked?.Invoke();
        }

        private void AppsButton_Click(object sender, RoutedEventArgs e)
        {
            AppsClicked?.Invoke();
        }

        private void NotificationButton_Click(object sender, RoutedEventArgs e)
        {
            NotificationsClicked?.Invoke();
        }
    }
}
