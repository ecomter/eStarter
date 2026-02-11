using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace eStarter.Services
{
    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }

    public sealed class NotificationItem
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public string Title { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public NotificationType Type { get; init; }
        public string AppId { get; init; } = "system";
        public DateTime Timestamp { get; init; } = DateTime.Now;
        public int DurationMs { get; init; } = 5000;
        public Action? OnClick { get; init; }
        public bool IsClosing { get; set; }

        public string Icon => Type switch
        {
            NotificationType.Success => "\uE10B", // Checkmark
            NotificationType.Warning => "\uE171", // Warning
            NotificationType.Error => "\uE10A",   // X
            _ => "\uE171"                         // Info
        };

        public string AccentColor => Type switch
        {
            NotificationType.Success => "#FF00B159",
            NotificationType.Warning => "#FFF09609",
            NotificationType.Error => "#FFE51400",
            _ => "#FF0078D7"
        };
    }

    /// <summary>
    /// Metro-style notification service with toast UI.
    /// </summary>
    public sealed class NotificationService
    {
        private readonly KernelService _kernelService;
        private readonly Dispatcher _dispatcher;
        private readonly ObservableCollection<NotificationItem> _notifications = new();

        public ObservableCollection<NotificationItem> ActiveNotifications => _notifications;

        public NotificationService(KernelService kernelService)
        {
            _kernelService = kernelService;
            _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

            // Register API handler for notifications
            RegisterApiHandler();
        }

        /// <summary>
        /// Show a notification toast.
        /// </summary>
        public Task ShowAsync(string title, string message, NotificationType type = NotificationType.Info, int durationMs = 5000, Action? onClick = null)
        {
            return ShowAsync(new NotificationItem
            {
                Title = title,
                Message = message,
                Type = type,
                DurationMs = durationMs,
                OnClick = onClick
            });
        }

        /// <summary>
        /// Show a notification from an app.
        /// </summary>
        public Task ShowFromAppAsync(string appId, string title, string message, NotificationType type = NotificationType.Info)
        {
            return ShowAsync(new NotificationItem
            {
                AppId = appId,
                Title = title,
                Message = message,
                Type = type
            });
        }

        /// <summary>
        /// Show a notification item.
        /// </summary>
        public async Task ShowAsync(NotificationItem notification)
        {
            await DispatchAsync(() =>
            {
                _notifications.Insert(0, notification);

                // Limit to 5 visible notifications
                while (_notifications.Count > 5)
                {
                    _notifications.RemoveAt(_notifications.Count - 1);
                }
            });

            // Auto-dismiss after duration
            if (notification.DurationMs > 0)
            {
                _ = Task.Delay(notification.DurationMs).ContinueWith(_ =>
                {
                    _ = DismissAsync(notification.Id);
                });
            }
        }

        /// <summary>
        /// Dismiss a notification.
        /// </summary>
        public async Task DismissAsync(string notificationId)
        {
            await DispatchAsync(() =>
            {
                for (int i = 0; i < _notifications.Count; i++)
                {
                    if (_notifications[i].Id == notificationId)
                    {
                        _notifications[i].IsClosing = true;
                        // Give animation time to play
                        Task.Delay(300).ContinueWith(_ =>
                        {
                            _ = DispatchAsync(() =>
                            {
                                for (int j = 0; j < _notifications.Count; j++)
                                {
                                    if (_notifications[j].Id == notificationId)
                                    {
                                        _notifications.RemoveAt(j);
                                        break;
                                    }
                                }
                            });
                        });
                        break;
                    }
                }
            });
        }

        /// <summary>
        /// Clear all notifications.
        /// </summary>
        public async Task ClearAllAsync()
        {
            await DispatchAsync(() => _notifications.Clear());
        }

        private void RegisterApiHandler()
        {
            _kernelService.Kernel.RegisterHandler(Core.Kernel.ApiCommand.ShowNotification, async (caller, request) =>
            {
                string title = "Notification";
                string content = string.Empty;
                var type = NotificationType.Info;

                if (request.Data?.TryGetProperty("title", out var titleEl) == true)
                    title = titleEl.GetString() ?? title;

                if (request.Data?.TryGetProperty("content", out var contentEl) == true)
                    content = contentEl.GetString() ?? string.Empty;

                if (request.Data?.TryGetProperty("type", out var typeEl) == true)
                {
                    var typeStr = typeEl.GetString()?.ToLower();
                    type = typeStr switch
                    {
                        "success" => NotificationType.Success,
                        "warning" => NotificationType.Warning,
                        "error" => NotificationType.Error,
                        _ => NotificationType.Info
                    };
                }

                await ShowFromAppAsync(caller.AppId, title, content, type);

                return Core.Kernel.ApiResponse.Success(request.RequestId);
            });
        }

        private Task DispatchAsync(Action action)
        {
            if (_dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }
            
            var tcs = new TaskCompletionSource();
            _dispatcher.BeginInvoke(() =>
            {
                action();
                tcs.SetResult();
            });
            return tcs.Task;
        }
    }
}
