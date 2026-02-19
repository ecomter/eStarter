using System.Windows;
using eStarter.Core.Kernel;

namespace eStarter.Views
{
    public partial class PermissionDialog : Window
    {
        public bool IsAllowed { get; private set; }
        
        public string AppId { get; }
        public Permission RequestedPermission { get; }

        private string GetStr(string key)
            => Application.Current.Resources[key] as string ?? key;

        public PermissionDialog(string appId, Permission permission, string? appName = null)
        {
            InitializeComponent();

            AppId = appId;
            RequestedPermission = permission;

            AppNameText.Text = appName ?? appId;
            PermissionText.Text = GetPermissionDisplayName(permission);
            PermissionIcon.Text = GetPermissionIcon(permission);
            TitleText.Text = GetStr("Str_PermDialogTitle");
            RequestingText.Text = GetStr("Str_PermDialogRequesting");
            AllowButton.Content = GetStr("Str_PermDialogAllow");
            DenyButton.Content = GetStr("Str_PermDialogDeny");
        }

        private void AllowButton_Click(object sender, RoutedEventArgs e)
        {
            IsAllowed = true;
            DialogResult = true;
            Close();
        }

        private void DenyButton_Click(object sender, RoutedEventArgs e)
        {
            IsAllowed = false;
            DialogResult = false;
            Close();
        }

        private static string GetPermissionDisplayName(Permission permission)
        {
            var key = "Str_Perm_" + permission switch
            {
                Permission.FileRead => "FileRead",
                Permission.FileWrite => "FileWrite",
                Permission.FileDelete => "FileDelete",
                Permission.FileSystem => "FileSystem",
                Permission.NetworkAccess => "NetworkAccess",
                Permission.NetworkListen => "NetworkListen",
                Permission.Network => "Network",
                Permission.Notification => "Notification",
                Permission.Clipboard => "Clipboard",
                Permission.Dialog => "Dialog",
                Permission.Overlay => "Overlay",
                Permission.UI => "UI",
                Permission.ProcessLaunch => "ProcessLaunch",
                Permission.ProcessKill => "ProcessKill",
                Permission.SystemSettings => "SystemSettings",
                Permission.SystemInfo => "SystemInfo",
                Permission.System => "System",
                Permission.IpcSend => "IpcSend",
                Permission.IpcReceive => "IpcReceive",
                Permission.IpcBroadcast => "IpcBroadcast",
                Permission.Ipc => "Ipc",
                Permission.Camera => "Camera",
                Permission.Microphone => "Microphone",
                Permission.Location => "Location",
                Permission.Hardware => "Hardware",
                _ => ""
            };

            if (!string.IsNullOrEmpty(key) && Application.Current.Resources[key] is string localized)
                return localized;

            // Fallback to English
            return permission switch
            {
                Permission.FileRead => "Read Files",
                Permission.FileWrite => "Write Files",
                Permission.FileDelete => "Delete Files",
                Permission.FileSystem => "Full File System Access",
                Permission.NetworkAccess => "Network Access",
                Permission.NetworkListen => "Network Listen",
                Permission.Network => "Full Network Access",
                Permission.Notification => "Show Notifications",
                Permission.Clipboard => "Access Clipboard",
                Permission.Dialog => "Show Dialogs",
                Permission.Overlay => "Show Overlays",
                Permission.UI => "Full UI Access",
                Permission.ProcessLaunch => "Launch Other Apps",
                Permission.ProcessKill => "Terminate Other Apps",
                Permission.SystemSettings => "Modify System Settings",
                Permission.SystemInfo => "Access System Information",
                Permission.System => "Full System Access",
                Permission.IpcSend => "Send Messages",
                Permission.IpcReceive => "Receive Messages",
                Permission.IpcBroadcast => "Broadcast Messages",
                Permission.Ipc => "Full IPC Access",
                Permission.Camera => "Access Camera",
                Permission.Microphone => "Access Microphone",
                Permission.Location => "Access Location",
                Permission.Hardware => "Full Hardware Access",
                _ => permission.ToString()
            };
        }

        private static string GetPermissionIcon(Permission permission)
        {
            // Segoe UI Symbol icons
            if ((permission & Permission.FileSystem) != 0) return "\uE188"; // Folder
            if ((permission & Permission.Network) != 0) return "\uE12B";    // Globe
            if ((permission & Permission.UI) != 0) return "\uE1EF";         // Window
            if ((permission & Permission.System) != 0) return "\uE115";     // Settings
            if ((permission & Permission.Ipc) != 0) return "\uE134";        // Message
            if ((permission & Permission.Hardware) != 0) return "\uE1C9";   // Hardware
            return "\uE192"; // Shield
        }

        /// <summary>
        /// Show permission dialog and return result.
        /// </summary>
        public static bool? ShowPermissionRequest(string appId, Permission permission, string? appName = null, Window? owner = null)
        {
            var dialog = new PermissionDialog(appId, permission, appName);
            
            if (owner != null)
            {
                dialog.Owner = owner;
            }
            else if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsLoaded)
            {
                dialog.Owner = Application.Current.MainWindow;
            }

            return dialog.ShowDialog();
        }
    }
}
