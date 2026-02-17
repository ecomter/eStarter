using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace eStarter.Core.Kernel
{
    /// <summary>
    /// Kernel-level system settings manager.
    /// Manages global OS policies (permissions, notifications, theme, language) with
    /// persistence and event propagation so all subsystems react to changes.
    /// </summary>
    public sealed class SystemSettingsManager
    {
        private readonly Kernel _kernel;
        private readonly string _storagePath;
        private SystemPolicies _policies;

        /// <summary>Raised when any system policy changes. Args: (key, oldValue, newValue)</summary>
        public event Action<string, object?, object?>? PolicyChanged;

        /// <summary>Raised when a global permission policy changes. Args: (permission, allowed)</summary>
        public event Action<Permission, bool>? GlobalPermissionPolicyChanged;

        public SystemPolicies Policies => _policies;

        public SystemSettingsManager(Kernel kernel, string? storagePath = null)
        {
            _kernel = kernel;
            _storagePath = storagePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "eStarter", "system-policies.json");

            _policies = Load();
        }

        /// <summary>
        /// Set a global permission policy. When disabled, revokes the permission from ALL running processes.
        /// </summary>
        public void SetGlobalPermissionPolicy(Permission permission, bool allowed)
        {
            var changed = false;

            if (permission.HasFlag(Permission.Location))
            {
                changed |= _policies.AllowLocation != allowed;
                _policies.AllowLocation = allowed;
            }
            if (permission.HasFlag(Permission.Camera))
            {
                changed |= _policies.AllowCamera != allowed;
                _policies.AllowCamera = allowed;
            }
            if (permission.HasFlag(Permission.Microphone))
            {
                changed |= _policies.AllowMicrophone != allowed;
                _policies.AllowMicrophone = allowed;
            }
            if (permission.HasFlag(Permission.FileSystem))
            {
                changed |= _policies.AllowFileSystem != allowed;
                _policies.AllowFileSystem = allowed;
            }
            if (permission.HasFlag(Permission.Network))
            {
                changed |= _policies.AllowNetwork != allowed;
                _policies.AllowNetwork = allowed;
            }
            if (permission.HasFlag(Permission.Ipc))
            {
                changed |= _policies.AllowIpc != allowed;
                _policies.AllowIpc = allowed;
            }

            if (!changed) return;

            // Enforce: revoke from all running processes when policy disables a permission
            if (!allowed)
            {
                foreach (var process in _kernel.GetAllProcesses())
                {
                    _kernel.RevokePermission(process.AppId, permission);
                }
                Debug.WriteLine($"[SystemSettings] Global policy DISABLED: {permission} — revoked from {_kernel.GetAllProcesses().Length} processes");
            }

            GlobalPermissionPolicyChanged?.Invoke(permission, allowed);
            _ = SaveAsync();
        }

        /// <summary>
        /// Check if a global permission policy allows the given permission.
        /// Called by the kernel before granting any per-app permission.
        /// </summary>
        public bool IsGloballyAllowed(Permission permission)
        {
            if (permission.HasFlag(Permission.Location) && !_policies.AllowLocation) return false;
            if (permission.HasFlag(Permission.Camera) && !_policies.AllowCamera) return false;
            if (permission.HasFlag(Permission.Microphone) && !_policies.AllowMicrophone) return false;
            if ((permission & Permission.FileSystem) != 0 && !_policies.AllowFileSystem) return false;
            if ((permission & Permission.Network) != 0 && !_policies.AllowNetwork) return false;
            if ((permission & Permission.Ipc) != 0 && !_policies.AllowIpc) return false;
            return true;
        }

        /// <summary>
        /// Update notification policy.
        /// </summary>
        public void SetNotificationPolicy(bool enabled, bool lockScreen, bool sounds, bool quietHours)
        {
            var old = _policies.NotificationsEnabled;
            _policies.NotificationsEnabled = enabled;
            _policies.LockScreenNotifications = lockScreen;
            _policies.NotificationSounds = sounds;
            _policies.QuietHoursEnabled = quietHours;

            if (old != enabled)
                PolicyChanged?.Invoke("NotificationsEnabled", old, enabled);

            _ = SaveAsync();
        }

        /// <summary>
        /// Update theme policy.
        /// </summary>
        public void SetTheme(string theme, string accentColor)
        {
            var oldTheme = _policies.Theme;
            _policies.Theme = theme;
            _policies.AccentColor = accentColor;

            if (oldTheme != theme)
                PolicyChanged?.Invoke("Theme", oldTheme, theme);

            _ = SaveAsync();
        }

        /// <summary>
        /// Update language policy.
        /// </summary>
        public void SetLanguage(string languageCode)
        {
            var old = _policies.Language;
            _policies.Language = languageCode;

            if (old != languageCode)
                PolicyChanged?.Invoke("Language", old, languageCode);

            _ = SaveAsync();
        }

        /// <summary>
        /// Get per-app permission breakdown: returns what each running app has granted.
        /// </summary>
        public (string AppId, Permission Granted, Permission Denied)[] GetPerAppPermissions()
        {
            var processes = _kernel.GetAllProcesses();
            var result = new (string, Permission, Permission)[processes.Length];
            for (int i = 0; i < processes.Length; i++)
            {
                result[i] = (processes[i].AppId, processes[i].Permissions.Granted, processes[i].Permissions.Denied);
            }
            return result;
        }

        private SystemPolicies Load()
        {
            try
            {
                if (File.Exists(_storagePath))
                {
                    var json = File.ReadAllText(_storagePath);
                    return JsonSerializer.Deserialize<SystemPolicies>(json) ?? new SystemPolicies();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemSettings] Load error: {ex.Message}");
            }
            return new SystemPolicies();
        }

        private async Task SaveAsync()
        {
            try
            {
                var dir = Path.GetDirectoryName(_storagePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(_policies, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_storagePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemSettings] Save error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Persistent system-wide policies.
    /// </summary>
    public sealed class SystemPolicies
    {
        // Permission policies
        public bool AllowLocation { get; set; } = false;
        public bool AllowCamera { get; set; } = true;
        public bool AllowMicrophone { get; set; } = true;
        public bool AllowFileSystem { get; set; } = true;
        public bool AllowNetwork { get; set; } = true;
        public bool AllowIpc { get; set; } = true;

        // Notification policies
        public bool NotificationsEnabled { get; set; } = true;
        public bool LockScreenNotifications { get; set; } = true;
        public bool NotificationSounds { get; set; } = true;
        public bool QuietHoursEnabled { get; set; } = false;

        // Appearance
        public string Theme { get; set; } = "Dark";
        public string AccentColor { get; set; } = "#FF0078D7";
        public string Language { get; set; } = "en-US";
    }
}
