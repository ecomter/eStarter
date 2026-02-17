using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using eStarter.Core.Kernel;
using eStarter.Models;
using eStarter.Services;

namespace eStarter.ViewModels
{
    /// <summary>
    /// Settings category for navigation.
    /// </summary>
    public class SettingsCategory : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string Id { get; init; } = string.Empty;
        public string Icon { get; init; } = "\uE115";
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// ViewModel for the Settings page.
    /// Settings acts as a system-level app with elevated privileges:
    /// privacy toggles directly control kernel permission policies,
    /// theme/language changes propagate through the kernel SystemSettingsManager.
    /// </summary>
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private SettingsCategory? _selectedCategory;
        private readonly ISettingsService _settingsService;
        private AppSettings _settings = new();
        private string _userName = Environment.UserName;
        private string _userPicturePath = string.Empty;
        private long _storageUsedBytes;
        private long _storageTotalBytes;

        public ObservableCollection<SettingsCategory> Categories { get; } = new();
        public ObservableCollection<string> AccentColors { get; } = new()
        {
            "#FF0078D7", "#FF1BA1E2", "#FFD24726", "#FFF09609",
            "#FF00A1F1", "#FF7E3878", "#FF00ABA9", "#FF647687",
            "#FFE51400", "#FFE3008C", "#FF00B294", "#FF8CBF26",
            "#FF00B159", "#FF6A00FF", "#FFFF8C00", "#FF00CED1"
        };
        public ObservableCollection<TimeZoneInfo> TimeZones { get; } = new(TimeZoneInfo.GetSystemTimeZones());
        public ObservableCollection<string> Languages { get; } = new() { "English", "中文" };
        public ObservableCollection<InstalledAppInfo> InstalledApps { get; } = new();

        // Commands
        public ICommand SelectCategoryCommand { get; }
        public ICommand ChangeThemeCommand { get; }
        public ICommand ChangeAccentColorCommand { get; }
        public ICommand BrowsePictureCommand { get; }
        public ICommand ResetDataCommand { get; }
        public ICommand ClearCacheCommand { get; }

        public SettingsCategory? SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (_selectedCategory != value)
                {
                    if (_selectedCategory != null) _selectedCategory.IsSelected = false;
                    _selectedCategory = value;
                    if (_selectedCategory != null) _selectedCategory.IsSelected = true;
                    OnPropertyChanged();
                }
            }
        }

        public AppSettings Settings
        {
            get => _settings;
            set { _settings = value; OnPropertyChanged(); }
        }

        public string UserName
        {
            get => _userName;
            set { _userName = value; OnPropertyChanged(); }
        }

        public string UserPicturePath
        {
            get => _userPicturePath;
            set { _userPicturePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(UserPicture)); }
        }

        public ImageSource? UserPicture
        {
            get
            {
                if (string.IsNullOrEmpty(_userPicturePath) || !System.IO.File.Exists(_userPicturePath))
                    return null;
                try
                {
                    return new BitmapImage(new Uri(_userPicturePath));
                }
                catch { return null; }
            }
        }

        public bool IsDarkTheme
        {
            get => Settings.Theme == "Dark";
            set
            {
                if (value)
                {
                    Settings.Theme = "Dark";
                    ApplyTheme();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsLightTheme));
                    SaveSettings();
                }
            }
        }

        public bool IsLightTheme
        {
            get => Settings.Theme == "Light";
            set
            {
                if (value)
                {
                    Settings.Theme = "Light";
                    ApplyTheme();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsDarkTheme));
                    SaveSettings();
                }
            }
        }

        public bool ShowNotifications
        {
            get => Settings.ShowNotifications;
            set
            {
                Settings.ShowNotifications = value;
                SyncNotificationPolicy();
                OnPropertyChanged();
                SaveSettings();
            }
        }

        public bool ShowTime
        {
            get => Settings.ShowTime;
            set { Settings.ShowTime = value; OnPropertyChanged(); SaveSettings(); }
        }

        public bool EnableAnimations
        {
            get => Settings.EnableAnimations;
            set { Settings.EnableAnimations = value; OnPropertyChanged(); SaveSettings(); }
        }

        // Notification settings — synced to kernel notification policy
        public bool ShowLockScreenNotifications
        {
            get => Settings.ShowLockScreenNotifications;
            set
            {
                Settings.ShowLockScreenNotifications = value;
                SyncNotificationPolicy();
                OnPropertyChanged();
                SaveSettings();
            }
        }

        public bool PlayNotificationSounds
        {
            get => Settings.PlayNotificationSounds;
            set
            {
                Settings.PlayNotificationSounds = value;
                SyncNotificationPolicy();
                OnPropertyChanged();
                SaveSettings();
            }
        }

        public bool QuietHoursEnabled
        {
            get => Settings.QuietHoursEnabled;
            set
            {
                Settings.QuietHoursEnabled = value;
                SyncNotificationPolicy();
                OnPropertyChanged();
                SaveSettings();
            }
        }

        // Privacy settings — these directly control kernel global permission policies.
        // When toggled off, the kernel revokes the permission from ALL running processes.
        public bool AllowLocation
        {
            get => Settings.AllowLocation;
            set
            {
                Settings.AllowLocation = value;
                ApplyGlobalPermissionPolicy(Permission.Location, value);
                OnPropertyChanged();
                SaveSettings();
            }
        }

        public bool AllowCamera
        {
            get => Settings.AllowCamera;
            set
            {
                Settings.AllowCamera = value;
                ApplyGlobalPermissionPolicy(Permission.Camera, value);
                OnPropertyChanged();
                SaveSettings();
            }
        }

        public bool AllowMicrophone
        {
            get => Settings.AllowMicrophone;
            set
            {
                Settings.AllowMicrophone = value;
                ApplyGlobalPermissionPolicy(Permission.Microphone, value);
                OnPropertyChanged();
                SaveSettings();
            }
        }

        public bool AllowFileSystem
        {
            get => Settings.AllowFileSystem;
            set
            {
                Settings.AllowFileSystem = value;
                ApplyGlobalPermissionPolicy(Permission.FileSystem, value);
                OnPropertyChanged();
                SaveSettings();
            }
        }

        public bool AllowNetwork
        {
            get => Settings.AllowNetwork;
            set
            {
                Settings.AllowNetwork = value;
                ApplyGlobalPermissionPolicy(Permission.Network, value);
                OnPropertyChanged();
                SaveSettings();
            }
        }

        public bool AllowIpc
        {
            get => Settings.AllowIpc;
            set
            {
                Settings.AllowIpc = value;
                ApplyGlobalPermissionPolicy(Permission.Ipc, value);
                OnPropertyChanged();
                SaveSettings();
            }
        }

        // Time settings
        public bool AutoSetTime
        {
            get => Settings.AutoSetTime;
            set { Settings.AutoSetTime = value; OnPropertyChanged(); SaveSettings(); }
        }

        public bool AutoSetTimeZone
        {
            get => Settings.AutoSetTimeZone;
            set { Settings.AutoSetTimeZone = value; OnPropertyChanged(); SaveSettings(); }
        }

        public TimeZoneInfo SelectedTimeZone
        {
            get
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(Settings.TimeZoneId); }
                catch { return TimeZoneInfo.Local; }
            }
            set
            {
                if (value != null && Settings.TimeZoneId != value.Id)
                {
                    Settings.TimeZoneId = value.Id;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public string SelectedLanguage
        {
            get => Settings.Language == "zh-CN" ? "中文" : "English";
            set
            {
                var code = value == "中文" ? "zh-CN" : "en-US";
                if (Settings.Language != code)
                {
                    Settings.Language = code;
                    ApplyLanguage(code);
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public string SelectedAccentColor
        {
            get => Settings.AccentColor;
            set
            {
                if (Settings.AccentColor != value)
                {
                    Settings.AccentColor = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public long StorageUsedBytes
        {
            get => _storageUsedBytes;
            set { _storageUsedBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(StorageUsedText)); }
        }

        public long StorageTotalBytes
        {
            get => _storageTotalBytes;
            set { _storageTotalBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(StorageTotalText)); }
        }

        public string StorageUsedText => FormatBytes(StorageUsedBytes);
        public string StorageTotalText => FormatBytes(StorageTotalBytes);

        public int RunningAppsCount => KernelService.Instance.IsRunning 
            ? KernelService.Instance.Kernel.GetAllProcesses().Length 
            : 0;

        public SettingsViewModel()
        {
            _settingsService = new SettingsService();

            SelectCategoryCommand = new RelayCommand(param => SelectCategory(param as SettingsCategory));
            ChangeThemeCommand = new RelayCommand(param => ChangeTheme(param as string));
            ChangeAccentColorCommand = new RelayCommand(param => ChangeAccentColor(param as string));
            BrowsePictureCommand = new RelayCommand(_ => BrowsePicture());
            ResetDataCommand = new RelayCommand(_ => ResetData());
            ClearCacheCommand = new RelayCommand(_ => ClearCache());

            InitializeCategories();
            LoadSettings();
            LoadStorageInfo();
            LoadInstalledApps();
        }

        private void InitializeCategories()
        {
            Categories.Add(new SettingsCategory
            {
                Id = "Personalize",
                Icon = "\uE1FC",
                Title = "Personalize",
                Description = "Lock screen, user tile, colors"
            });

            Categories.Add(new SettingsCategory
            {
                Id = "Users",
                Icon = "\uE13D",
                Title = "Users",
                Description = "Your account, sign-in options"
            });

            Categories.Add(new SettingsCategory
            {
                Id = "Notifications",
                Icon = "\uE1EF",
                Title = "Notifications",
                Description = "App notifications, quiet hours"
            });

            Categories.Add(new SettingsCategory
            {
                Id = "Apps",
                Icon = "\uE179",
                Title = "Apps",
                Description = "Installed apps, defaults"
            });

            Categories.Add(new SettingsCategory
            {
                Id = "Privacy",
                Icon = "\uE1F7",
                Title = "Privacy",
                Description = "Permissions, app access"
            });

            Categories.Add(new SettingsCategory
            {
                Id = "TimeLanguage",
                Icon = "\uE1C4",
                Title = "Time & language",
                Description = "Date, time, region, language"
            });

            Categories.Add(new SettingsCategory
            {
                Id = "EaseOfAccess",
                Icon = "\uE16C",
                Title = "Ease of Access",
                Description = "Narrator, high contrast"
            });

            Categories.Add(new SettingsCategory
            {
                Id = "Update",
                Icon = "\uE117",
                Title = "Update & recovery",
                Description = "System updates, reset"
            });

            Categories.Add(new SettingsCategory
            {
                Id = "About",
                Icon = "\uE171",
                Title = "About",
                Description = "System info, version"
            });

            // Select first category
            if (Categories.Count > 0)
            {
                SelectedCategory = Categories[0];
            }
        }

        private void SelectCategory(SettingsCategory? category)
        {
            if (category != null)
            {
                SelectedCategory = category;
            }
        }

        private async void LoadSettings()
        {
            Settings = await _settingsService.LoadAppSettingsAsync();
            UserPicturePath = Settings.UserPicturePath;

            // Sync settings from kernel policies (kernel is the source of truth)
            SyncFromKernelPolicies();

            // Apply theme on load
            ApplyTheme();
            ApplyAccentColor();

            OnPropertyChanged(nameof(IsDarkTheme));
            OnPropertyChanged(nameof(IsLightTheme));
            OnPropertyChanged(nameof(ShowNotifications));
            OnPropertyChanged(nameof(ShowTime));
            OnPropertyChanged(nameof(EnableAnimations));
            OnPropertyChanged(nameof(ShowLockScreenNotifications));
            OnPropertyChanged(nameof(PlayNotificationSounds));
            OnPropertyChanged(nameof(QuietHoursEnabled));
            OnPropertyChanged(nameof(AllowLocation));
            OnPropertyChanged(nameof(AllowCamera));
            OnPropertyChanged(nameof(AllowMicrophone));
            OnPropertyChanged(nameof(AllowFileSystem));
            OnPropertyChanged(nameof(AllowNetwork));
            OnPropertyChanged(nameof(AllowIpc));
            OnPropertyChanged(nameof(AutoSetTime));
            OnPropertyChanged(nameof(AutoSetTimeZone));
            OnPropertyChanged(nameof(SelectedTimeZone));
            OnPropertyChanged(nameof(SelectedLanguage));
            OnPropertyChanged(nameof(SelectedAccentColor));
        }

        /// <summary>
        /// On startup, merge kernel SystemPolicies into AppSettings so UI reflects kernel state.
        /// </summary>
        private void SyncFromKernelPolicies()
        {
            if (!KernelService.Instance.IsRunning) return;

            try
            {
                var policies = KernelService.Instance.SystemSettings.Policies;

                Settings.AllowLocation = policies.AllowLocation;
                Settings.AllowCamera = policies.AllowCamera;
                Settings.AllowMicrophone = policies.AllowMicrophone;
                Settings.AllowFileSystem = policies.AllowFileSystem;
                Settings.AllowNetwork = policies.AllowNetwork;
                Settings.AllowIpc = policies.AllowIpc;

                Settings.ShowNotifications = policies.NotificationsEnabled;
                Settings.ShowLockScreenNotifications = policies.LockScreenNotifications;
                Settings.PlayNotificationSounds = policies.NotificationSounds;
                Settings.QuietHoursEnabled = policies.QuietHoursEnabled;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] Kernel sync error: {ex.Message}");
            }
        }

        private async void SaveSettings()
        {
            Settings.UserPicturePath = UserPicturePath;
            await _settingsService.SaveAppSettingsAsync(Settings);
        }

        private void ChangeTheme(string? theme)
        {
            if (!string.IsNullOrEmpty(theme))
            {
                Settings.Theme = theme;
                ApplyTheme();
                OnPropertyChanged(nameof(IsDarkTheme));
                OnPropertyChanged(nameof(IsLightTheme));
                SaveSettings();
            }
        }

        private void ChangeAccentColor(string? color)
        {
            if (!string.IsNullOrEmpty(color))
            {
                Settings.AccentColor = color;
                ApplyAccentColor();
                OnPropertyChanged(nameof(SelectedAccentColor));
                SaveSettings();
            }
        }

        private void ApplyTheme()
        {
            // Write theme to kernel
            if (KernelService.Instance.IsRunning)
            {
                KernelService.Instance.SystemSettings.SetTheme(Settings.Theme, Settings.AccentColor);
            }

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                if (Settings.Theme == "Light")
                {
                    Application.Current.Resources["MetroBackgroundBrush"] = new SolidColorBrush(Colors.White);
                    Application.Current.Resources["MetroForegroundBrush"] = new SolidColorBrush(Colors.Black);
                    Application.Current.Resources["MetroSeparatorBrush"] = new SolidColorBrush(Color.FromArgb(51, 0, 0, 0));
                }
                else
                {
                    Application.Current.Resources["MetroBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(31, 31, 31));
                    Application.Current.Resources["MetroForegroundBrush"] = new SolidColorBrush(Colors.White);
                    Application.Current.Resources["MetroSeparatorBrush"] = new SolidColorBrush(Color.FromArgb(51, 255, 255, 255));
                }
            });
        }

        private void ApplyAccentColor()
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                try
                {
                    if (ColorConverter.ConvertFromString(Settings.AccentColor) is Color accentColor)
                    {
                        Application.Current.Resources["MetroAccentColor"] = accentColor;
                        Application.Current.Resources["MetroAccentBrush"] = new SolidColorBrush(accentColor);
                    }
                }
                catch { }
            });
        }

        private void ApplyLanguage(string langCode)
        {
            // Write language to kernel
            if (KernelService.Instance.IsRunning)
            {
                KernelService.Instance.SystemSettings.SetLanguage(langCode);
            }

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                try
                {
                    var dictName = langCode == "zh-CN" ? "Strings.zh-CN.xaml" : "Strings.xaml";
                    var uri = new Uri($"pack://application:,,,/Resources/{dictName}", UriKind.Absolute);
                    var newDict = new ResourceDictionary { Source = uri };
                    var mergedDicts = Application.Current.Resources.MergedDictionaries;

                    // Find and remove existing string dictionary
                    ResourceDictionary? existing = null;
                    foreach (var dict in mergedDicts)
                    {
                        if (dict.Source?.OriginalString?.Contains("Strings") == true)
                        {
                            existing = dict;
                            break;
                        }
                    }
                    if (existing != null) mergedDicts.Remove(existing);
                    mergedDicts.Add(newDict);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to apply language: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Push a global permission policy change into the kernel.
        /// When toggled off, the kernel revokes that permission from every running process.
        /// </summary>
        private void ApplyGlobalPermissionPolicy(Permission permission, bool allowed)
        {
            if (!KernelService.Instance.IsRunning) return;

            try
            {
                KernelService.Instance.SystemSettings.SetGlobalPermissionPolicy(permission, allowed);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] Permission policy error: {ex.Message}");
            }
        }

        /// <summary>
        /// Sync current notification settings into the kernel notification policy.
        /// </summary>
        private void SyncNotificationPolicy()
        {
            if (!KernelService.Instance.IsRunning) return;

            try
            {
                KernelService.Instance.SystemSettings.SetNotificationPolicy(
                    Settings.ShowNotifications,
                    Settings.ShowLockScreenNotifications,
                    Settings.PlayNotificationSounds,
                    Settings.QuietHoursEnabled);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsVM] Notification policy error: {ex.Message}");
            }
        }

        private void BrowsePicture()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp|All files|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                UserPicturePath = dialog.FileName;
            }
        }

        private void ResetData()
        {
            if (!KernelService.Instance.IsRunning) return;

            // Terminate all running processes through kernel
            foreach (var process in KernelService.Instance.Kernel.GetAllProcesses())
            {
                KernelService.Instance.Kernel.UnregisterProcess(process.AppId);
            }
        }

        private void ClearCache()
        {
            if (KernelService.Instance.IsRunning)
            {
                // Clear all app caches through kernel file system
                var apps = KernelService.Instance.Kernel.GetAllProcesses();
                foreach (var app in apps)
                {
                    KernelService.Instance.FileSystem.ClearAppCache(app.AppId);
                }
            }
            LoadStorageInfo();
        }

        private void LoadStorageInfo()
        {
            try
            {
                var baseDir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "eStarter");

                if (System.IO.Directory.Exists(baseDir))
                {
                    StorageUsedBytes = GetDirectorySize(baseDir);
                }

                // Get drive total space
                var drive = new System.IO.DriveInfo(System.IO.Path.GetPathRoot(baseDir) ?? "C:");
                StorageTotalBytes = drive.TotalSize;
            }
            catch { }
        }

        private void LoadInstalledApps()
        {
            InstalledApps.Clear();
            var manager = new Core.AppManager(new AppInstaller());
            foreach (var app in manager.GetInstalledAppEntries())
            {
                var (appData, cache, temp) = KernelService.Instance.IsRunning
                    ? KernelService.Instance.FileSystem.GetAppStorageUsage(app.Id)
                    : (0, 0, 0);

                InstalledApps.Add(new InstalledAppInfo
                {
                    Id = app.Id,
                    Name = app.Name,
                    Publisher = app.Publisher,
                    Version = app.Version,
                    StorageBytes = appData + cache + temp
                });
            }
        }

        private static long GetDirectorySize(string path)
        {
            long size = 0;
            try
            {
                foreach (var file in System.IO.Directory.GetFiles(path, "*", System.IO.SearchOption.AllDirectories))
                {
                    size += new System.IO.FileInfo(file).Length;
                }
            }
            catch { }
            return size;
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class InstalledAppInfo
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Publisher { get; init; } = string.Empty;
        public string Version { get; init; } = string.Empty;
        public long StorageBytes { get; init; }
        public string StorageText => $"{StorageBytes / 1024.0 / 1024.0:F2} MB";
    }
}
