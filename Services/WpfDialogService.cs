using System.Threading.Tasks;
using System.Windows;

namespace eStarter.Services
{
    /// <summary>
    /// WPF implementation of IDialogService using ModernMsgBox.
    /// </summary>
    public sealed class WpfDialogService : IDialogService
    {
        public Task<DialogResult> ShowMessageAsync(string message, string title, DialogButton buttons = DialogButton.OK)
        {
            var wpfButton = buttons switch
            {
                DialogButton.OKCancel => MessageBoxButton.OKCancel,
                DialogButton.YesNo => MessageBoxButton.YesNo,
                _ => MessageBoxButton.OK
            };

            var owner = Application.Current?.MainWindow;
            Views.ModernMsgBox.ShowMessage(message, title, wpfButton, owner);

            return Task.FromResult(DialogResult.OK);
        }

        public Task<bool> ShowPermissionRequestAsync(string appId, string permissionName, string appName)
        {
            var result = Views.PermissionDialog.ShowPermissionRequest(
                appId,
                Core.Kernel.Permission.None,
                appName);

            return Task.FromResult(result == true);
        }
    }
}
