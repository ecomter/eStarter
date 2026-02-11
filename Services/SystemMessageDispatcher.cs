using System;
using System.Text.Json;
using System.Windows;
using eStarter.Sdk.Ipc;
using eStarter.Sdk.Models;
using eStarter.ViewModels;
using eStarter.Views;

namespace eStarter.Services
{
    public class SystemMessageDispatcher
    {
        private readonly MainViewModel _mainViewModel;

        public SystemMessageDispatcher(MainViewModel viewModel)
        {
            _mainViewModel = viewModel;
        }

        public void Dispatch(IpcMessage message)
        {
            try
            {
                switch (message.Type)
                {
                    case IpcMessageType.Command:
                        HandleCommand(message);
                        break;
                    // Can handle Events or Responses here later
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Dispatcher] Error dispatching message: {ex.Message}");
            }
        }

        private void HandleCommand(IpcMessage message)
        {
            // Simple routing based on payload content or a specific 'Action' field if we added one.
            // For now, we try to deserialize structurally.

            // 1. Try Notification
            if (TryHandleNotification(message.Payload)) return;

            // 2. Try Launch App
            if (TryHandleLaunchApp(message.Payload)) return;
        }

        private bool TryHandleNotification(string json)
        {
            try
            {
                // Heuristic: Check for required properties to avoid false positives
                if (!json.Contains("\"Content\"") && !json.Contains("\"Title\"")) return false;

                var req = JsonSerializer.Deserialize<NotificationRequest>(json);
                if (req == null || string.IsNullOrWhiteSpace(req.Content)) return false;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ModernMsgBox.ShowMessage(req.Content, req.Title, MessageBoxButton.OK, Application.Current.MainWindow);
                });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TryHandleLaunchApp(string json)
        {
            try
            {
                if (!json.Contains("\"TargetAppId\"")) return false;

                var req = JsonSerializer.Deserialize<AppLaunchRequest>(json);
                if (req == null || string.IsNullOrWhiteSpace(req.TargetAppId)) return false;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    _mainViewModel.LaunchAppById(req.TargetAppId, req.Arguments);
                });
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
