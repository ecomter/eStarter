using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using eStarter.Services;

namespace eStarter.Views
{
    public partial class NotificationHost : UserControl
    {
        public NotificationHost()
        {
            InitializeComponent();

            // Bind to KernelService notifications
            if (KernelService.Instance.IsRunning)
            {
                NotificationList.ItemsSource = KernelService.Instance.Notifications.ActiveNotifications;
            }

            Loaded += (s, e) =>
            {
                if (KernelService.Instance.IsRunning)
                {
                    NotificationList.ItemsSource = KernelService.Instance.Notifications.ActiveNotifications;
                }
            };
        }

        private async void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string notificationId)
            {
                await KernelService.Instance.Notifications.DismissAsync(notificationId);
            }
        }

        private void Toast_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is NotificationItem notification)
            {
                notification.OnClick?.Invoke();
            }
        }
    }
}
