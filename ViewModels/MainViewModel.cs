using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using eStarter.Core;
using eStarter.Models;
using eStarter.Services;
using eStarter.Views;
using System;

namespace eStarter.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<AppEntry> InstalledApps { get; } = new ObservableCollection<AppEntry>();
        public ObservableCollection<AppEntry> SearchResults { get; } = new ObservableCollection<AppEntry>();
        public ObservableCollection<TimeZoneInfo> AvailableTimeZones { get; } = new ObservableCollection<TimeZoneInfo>(TimeZoneInfo.GetSystemTimeZones());
        public ObservableCollection<string> AvailableLanguages { get; } = new ObservableCollection<string> { "English", "中文" };

        public ICommand RefreshCommand { get; }
        public ICommand InstallCommand { get; }
        public ICommand LaunchCommand { get; }
        public ICommand ChangeTileSizeCommand { get; }
        public ICommand ChangeTileColorCommand { get; }
        public ICommand ChangeThemeCommand { get; }
        public ICommand ChangeAccentColorCommand { get; }
        public ICommand CloseSearchCommand { get; }
        public ICommand OpenSearchCommand { get; }
        public ICommand ToggleTimeCommand { get; }

        private readonly AppManager _manager;
        private readonly ISettingsService _settingsService;
        private AppSettings _currentSettings = new AppSettings();
        private string _searchText = string.Empty;
        private bool _isSearchOpen;
        private string _userName = System.Environment.UserName;
        private string _currentTime = System.DateTime.Now.ToString("t");
        private System.Windows.Threading.DispatcherTimer _timer;

        private string GetString(string key) => Application.Current.Resources[key] as string ?? key;

        public AppSettings CurrentSettings
        {
            get => _currentSettings;
            set 
            { 
                _currentSettings = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(ShowTime));
                OnPropertyChanged(nameof(SelectedTimeZone));
                OnPropertyChanged(nameof(SelectedLanguage));
            }
        }

        public string SelectedLanguage
        {
            get => CurrentSettings.Language == "zh-CN" ? "中文" : "English";
            set
            {
                var langCode = value == "中文" ? "zh-CN" : "en-US";
                if (CurrentSettings.Language != langCode)
                {
                    CurrentSettings.Language = langCode;
                    OnPropertyChanged();
                    ApplyLanguage(langCode);
                    _ = _settingsService.SaveAppSettingsAsync(CurrentSettings);
                }
            }
        }

        public TimeZoneInfo SelectedTimeZone
        {
            get
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(CurrentSettings.TimeZoneId);
                }
                catch
                {
                    return TimeZoneInfo.Local;
                }
            }
            set
            {
                if (value != null && CurrentSettings.TimeZoneId != value.Id)
                {
                    CurrentSettings.TimeZoneId = value.Id;
                    OnPropertyChanged();
                    _ = _settingsService.SaveAppSettingsAsync(CurrentSettings);
                    UpdateCurrentTime();
                }
            }
        }

        public string UserName
        {
            get => _userName;
            set { _userName = value; OnPropertyChanged(); }
        }

        public string CurrentTime
        {
            get => _currentTime;
            set { _currentTime = value; OnPropertyChanged(); }
        }

        public bool ShowTime
        {
            get => CurrentSettings.ShowTime;
            set
            {
                if (CurrentSettings.ShowTime != value)
                {
                    CurrentSettings.ShowTime = value;
                    OnPropertyChanged();
                    _ = _settingsService.SaveAppSettingsAsync(CurrentSettings);
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    PerformSearch();
                }
            }
        }

        public bool IsSearchOpen
        {
            get => _isSearchOpen;
            set
            {
                if (_isSearchOpen != value)
                {
                    _isSearchOpen = value;
                    OnPropertyChanged();
                    if (!value)
                    {
                        SearchText = string.Empty;
                        SearchResults.Clear();
                    }
                }
            }
        }

        public ObservableCollection<string> AvailableAccentColors { get; } = new ObservableCollection<string>
        {
            "#FF0078D7", "#FF1BA1E2", "#FFD24726", "#FFF09609", 
            "#FF00A1F1", "#FF7E3878", "#FF00ABA9", "#FF647687",
            "#FFE51400", "#FFE3008C", "#FF00B294", "#FF8CBF26"
        };

        public MainViewModel()
        {
            var installer = new AppInstaller();
            _manager = new AppManager(installer);
            _settingsService = new SettingsService();

            RefreshCommand = new RelayCommand(_ => Refresh());
            InstallCommand = new RelayCommand(async _ => await InstallAsync());
            LaunchCommand = new RelayCommand(param => LaunchApp(param as AppEntry));
            ChangeTileSizeCommand = new RelayCommand(async param => await ChangeTileSizeAsync(param as AppEntry));
            ChangeTileColorCommand = new RelayCommand(async param => await ChangeTileColorAsync(param as AppEntry));
            ChangeThemeCommand = new RelayCommand(async param => await ChangeThemeAsync(param as string));
            ChangeAccentColorCommand = new RelayCommand(async param => await ChangeAccentColorSettingAsync(param as string));
            CloseSearchCommand = new RelayCommand(_ => IsSearchOpen = false);
            OpenSearchCommand = new RelayCommand(_ => IsSearchOpen = true);
            ToggleTimeCommand = new RelayCommand(_ => ShowTime = !ShowTime);

            // Setup Timer
            _timer = new System.Windows.Threading.DispatcherTimer();
            _timer.Interval = System.TimeSpan.FromSeconds(1);
            _timer.Tick += (s, e) => UpdateCurrentTime();
            _timer.Start();

            // Initialize async but handle exceptions properly
            Task.Run(async () => await InitializeAsync());
        }

        private void UpdateCurrentTime()
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(CurrentSettings.TimeZoneId);
                var time = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.Local, tz);
                CurrentTime = time.ToString("t");
            }
            catch
            {
                CurrentTime = DateTime.Now.ToString("t");
            }
        }

        private void PerformSearch()
        {
            SearchResults.Clear();
            if (string.IsNullOrWhiteSpace(SearchText))
                return;

            var query = SearchText.ToLower();
            foreach (var app in InstalledApps)
            {
                if ((app.Name?.ToLower().Contains(query) == true) || 
                    (app.Description?.ToLower().Contains(query) == true))
                {
                    SearchResults.Add(app);
                }
            }
        }

        private async Task InitializeAsync()
        {
            // Load App Settings
            CurrentSettings = await _settingsService.LoadAppSettingsAsync();
            Application.Current.Dispatcher.Invoke(() => 
            {
                ApplyTheme(CurrentSettings);
                ApplyLanguage(CurrentSettings.Language);
            });

            // Try to load saved configuration first
            var savedApps = await _settingsService.LoadTileConfigurationAsync();
            var savedList = savedApps.ToList();
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (savedList.Any())
                {
                    foreach (var app in savedList)
                        InstalledApps.Add(app);
                    
                    // Update names for demo apps in case they were saved in a different language
                    UpdateDemoAppNames();
                }
                else
                {
                    // No saved config, load defaults
                    Refresh();
                }
            });
        }

        private async Task ChangeThemeAsync(string? theme)
        {
            if (string.IsNullOrEmpty(theme)) return;
            CurrentSettings.Theme = theme;
            ApplyTheme(CurrentSettings);
            await _settingsService.SaveAppSettingsAsync(CurrentSettings);
        }

        private async Task ChangeAccentColorSettingAsync(string? color)
        {
            if (string.IsNullOrEmpty(color)) return;
            CurrentSettings.AccentColor = color;
            ApplyTheme(CurrentSettings);
            await _settingsService.SaveAppSettingsAsync(CurrentSettings);
        }

        private void ApplyLanguage(string language)
        {
            var dictName = language == "zh-CN" ? "Strings.zh-CN.xaml" : "Strings.xaml";
            var uri = new Uri($"pack://application:,,,/Resources/{dictName}", UriKind.Absolute);
            
            try
            {
                var newDict = new ResourceDictionary { Source = uri };
                var mergedDicts = Application.Current.Resources.MergedDictionaries;
                
                // Find existing string resource dictionary and replace it
                // We assume it's the second one based on App.xaml structure, but safer to check Source
                // However, Source property might be null for some dicts.
                // A better way is to remove any dict that looks like a string resource and add the new one.
                
                var existing = mergedDicts.FirstOrDefault(d => d.Source != null && (d.Source.OriginalString.Contains("StringResources") || d.Source.OriginalString.Contains("Strings")));
                if (existing != null)
                {
                    mergedDicts.Remove(existing);
                }
                mergedDicts.Add(newDict);

                // Update demo app names to match new language
                UpdateDemoAppNames();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load language dictionary: {ex.Message}");
            }
        }

        private void UpdateDemoAppNames()
        {
            foreach (var app in InstalledApps)
            {
                if (app.Id.StartsWith("demo."))
                {
                    string nameKey = "";
                    string descKey = "";
                    
                    switch (app.Id)
                    {
                        case "demo.mail": nameKey = "Str_DemoMailName"; descKey = "Str_DemoMailDesc"; break;
                        case "demo.calendar": nameKey = "Str_DemoCalendarName"; descKey = "Str_DemoCalendarDesc"; break;
                        case "demo.photos": nameKey = "Str_DemoPhotosName"; descKey = "Str_DemoPhotosDesc"; break;
                        case "demo.music": nameKey = "Str_DemoMusicName"; descKey = "Str_DemoMusicDesc"; break;
                        case "demo.store": nameKey = "Str_DemoStoreName"; descKey = "Str_DemoStoreDesc"; break;
                        case "demo.news": nameKey = "Str_DemoNewsName"; descKey = "Str_DemoNewsDesc"; break;
                        case "demo.weather": nameKey = "Str_DemoWeatherName"; descKey = "Str_DemoWeatherDesc"; break;
                        case "demo.settings": nameKey = "Str_DemoSettingsName"; descKey = "Str_DemoSettingsDesc"; break;
                        case "demo.about": nameKey = "Str_DemoAboutName"; descKey = "Str_DemoAboutDesc"; break;
                    }

                    if (!string.IsNullOrEmpty(nameKey)) app.Name = GetString(nameKey);
                    if (!string.IsNullOrEmpty(descKey)) app.Description = GetString(descKey);
                }
            }
        }

        private void ApplyTheme(AppSettings settings)
        {
            try
            {
                // Apply Accent Color
                if (ColorConverter.ConvertFromString(settings.AccentColor) is Color accentColor)
                {
                    Application.Current.Resources["MetroAccentColor"] = accentColor;
                    Application.Current.Resources["MetroAccentBrush"] = new SolidColorBrush(accentColor);
                }

                // Apply Theme (Dark/Light)
                if (settings.Theme == "Light")
                {
                    Application.Current.Resources["MetroBackgroundBrush"] = new SolidColorBrush(Colors.White);
                    Application.Current.Resources["MetroForegroundBrush"] = new SolidColorBrush(Colors.Black);
                    Application.Current.Resources["MetroSeparatorBrush"] = new SolidColorBrush(Color.FromArgb(51, 0, 0, 0)); // #33000000
                }
                else
                {
                    Application.Current.Resources["MetroBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(31, 31, 31)); // #FF1F1F1F
                    Application.Current.Resources["MetroForegroundBrush"] = new SolidColorBrush(Colors.White);
                    Application.Current.Resources["MetroSeparatorBrush"] = new SolidColorBrush(Color.FromArgb(51, 255, 255, 255)); // #33FFFFFF
                }
            }
            catch
            {
                // Ignore invalid colors
            }
        }

        private void Refresh()
        {
            InstalledApps.Clear();
            // Load installed apps from manifest or directory scan
            foreach (var app in _manager.GetInstalledAppEntries())
                InstalledApps.Add(app);

            // Add demo tiles if empty with varied sizes and colors (Metro style)
            if (!InstalledApps.Any())
            {
                InstalledApps.Add(new AppEntry 
                { 
                    Id = "demo.mail", 
                    Name = GetString("Str_DemoMailName"), 
                    Description = GetString("Str_DemoMailDesc"), 
                    BadgeCount = 5, 
                    Background = "#FF0078D7",
                    TileSize = TileSize.Medium,
                    Publisher = "Microsoft Corporation",
                    Category = "Productivity",
                    Version = "1.2.0"
                });
                
                InstalledApps.Add(new AppEntry 
                { 
                    Id = "demo.calendar", 
                    Name = GetString("Str_DemoCalendarName"), 
                    Description = GetString("Str_DemoCalendarDesc"), 
                    Background = "#FF1BA1E2",
                    TileSize = TileSize.Medium,
                    Publisher = "Microsoft Corporation",
                    Category = "Productivity"
                });
                
                InstalledApps.Add(new AppEntry 
                { 
                    Id = "demo.photos", 
                    Name = GetString("Str_DemoPhotosName"), 
                    Description = GetString("Str_DemoPhotosDesc"), 
                    Background = "#FFD24726",
                    TileSize = TileSize.Wide,
                    Publisher = "Microsoft Corporation",
                    Category = "Photo & Video"
                });
                
                InstalledApps.Add(new AppEntry 
                { 
                    Id = "demo.music", 
                    Name = GetString("Str_DemoMusicName"), 
                    Description = GetString("Str_DemoMusicDesc"), 
                    Background = "#FFF09609",
                    TileSize = TileSize.Medium,
                    Publisher = "Microsoft Corporation",
                    Category = "Music"
                });
                
                InstalledApps.Add(new AppEntry 
                { 
                    Id = "demo.store", 
                    Name = GetString("Str_DemoStoreName"), 
                    Description = GetString("Str_DemoStoreDesc"), 
                    Background = "#FF00A1F1",
                    TileSize = TileSize.Medium,
                    Publisher = "Microsoft Corporation",
                    Category = "Shopping"
                });
                
                InstalledApps.Add(new AppEntry 
                { 
                    Id = "demo.news", 
                    Name = GetString("Str_DemoNewsName"), 
                    Description = GetString("Str_DemoNewsDesc"), 
                    BadgeCount = 12,
                    Background = "#FF7E3878",
                    TileSize = TileSize.Wide,
                    Publisher = "Microsoft Corporation",
                    Category = "News & Weather"
                });
                
                InstalledApps.Add(new AppEntry 
                { 
                    Id = "demo.weather", 
                    Name = GetString("Str_DemoWeatherName"), 
                    Description = GetString("Str_DemoWeatherDesc"), 
                    Background = "#FF00ABA9",
                    TileSize = TileSize.Medium,
                    Publisher = "Microsoft Corporation",
                    Category = "News & Weather"
                });

                InstalledApps.Add(new AppEntry
                {
                    Id = "demo.settings",
                    Name = GetString("Str_DemoSettingsName"),
                    Description = GetString("Str_DemoSettingsDesc"), 
                    Background = "#FF647687",
                    TileSize = TileSize.Medium,
                    Publisher = "System",
                    Category = "System"
                });
            }

            // Ensure About and Settings tiles exist so user can always access them
            if (!InstalledApps.Any(x => x.Id == "demo.about"))
            {
                InstalledApps.Add(new AppEntry
                {
                    Id = "demo.about",
                    Name = GetString("Str_DemoAboutName"),
                    Description = GetString("Str_DemoAboutDesc"), 
                    Background = "#FF2D2D30",
                    TileSize = TileSize.Medium,
                    Publisher = "System",
                    Category = "System"
                });
            }

            if (!InstalledApps.Any(x => x.Id == "demo.settings"))
            {
                InstalledApps.Add(new AppEntry
                {
                    Id = "demo.settings",
                    Name = GetString("Str_DemoSettingsName"),
                    Description = GetString("Str_DemoSettingsDesc"), 
                    Background = "#FF647687",
                    TileSize = TileSize.Medium,
                    Publisher = "System",
                    Category = "System"
                });
            }
        }

        private async Task InstallAsync()
        {
            // For MVP: look for a package in the application's folder named "sample.app.zip"
            var appDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var candidate = Path.Combine(appDir ?? string.Empty, "sample.app.zip");
            if (File.Exists(candidate))
            {
                await _manager.InstallAsync(candidate);
                Application.Current.Dispatcher.Invoke(() => Refresh());
                await SaveSettingsAsync();
            }
        }

        private void LaunchApp(AppEntry? app)
        {
            if (app == null || string.IsNullOrWhiteSpace(app.Id))
                return;

            // If it's the About tile, navigate to AboutPage inside main window
            if (app.Id == "demo.about")
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    var mw = Application.Current?.MainWindow as MainWindow;
                    mw?.ShowPage(new Views.AboutPage());
                });
                return;
            }

            // If it's the Settings tile, navigate to SettingsPage inside main window
            if (app.Id == "demo.settings" || app.Id == "system.settings")
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    var mw = Application.Current?.MainWindow as MainWindow;
                    mw?.ShowPage(new Views.SettingsPage());
                });
                return;
            }

            var baseDir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "eStarter", "apps", app.Id);
            if (!Directory.Exists(baseDir))
            {
                // Show feedback for demo apps
                if (app.Id.StartsWith("demo."))
                {
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        ModernMsgBox.ShowMessage(
                            string.Format(GetString("Str_DemoAppMsg"), app.Name), 
                            GetString("Str_DemoAppTitle"), 
                            MessageBoxButton.OK, 
                            Application.Current.MainWindow);
                    });
                }
                return;
            }

            // Use explicit ExePath from manifest if available, otherwise search
            string? exePath = null;
            if (!string.IsNullOrEmpty(app.ExePath))
            {
                exePath = Path.Combine(baseDir, app.ExePath);
            }
            
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                exePath = Directory.EnumerateFiles(baseDir, "*.exe", SearchOption.TopDirectoryOnly).FirstOrDefault();
            }

            if (exePath == null)
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    ModernMsgBox.ShowMessage(
                        string.Format(GetString("Str_LaunchErrorMsg"), app.Name), 
                        GetString("Str_LaunchErrorTitle"), 
                        MessageBoxButton.OK, 
                        Application.Current.MainWindow);
                });
                return;
            }

            try
            {
                var psi = new ProcessStartInfo(exePath)
                {
                    WorkingDirectory = Path.GetDirectoryName(exePath) ?? baseDir,
                    UseShellExecute = true
                };

                if (!string.IsNullOrEmpty(app.Arguments))
                {
                    psi.Arguments = app.Arguments;
                }

                Process.Start(psi);
            }
            catch
            {
                // swallow for MVP -- real app should log or surface error
            }
        }

        private async Task ChangeTileSizeAsync(AppEntry? app)
        {
            if (app == null) return;

            // Cycle through tile sizes
            app.TileSize = app.TileSize switch
            {
                TileSize.Small => TileSize.Medium,
                TileSize.Medium => TileSize.Wide,
                TileSize.Wide => TileSize.Large,
                TileSize.Large => TileSize.Small,
                _ => TileSize.Medium
            };

            await SaveSettingsAsync();
        }

        private async Task ChangeTileColorAsync(AppEntry? app)
        {
            if (app == null) return;

            // Cycle through Windows accent colors
            var colors = new[] 
            { 
                "#FF0078D7", "#FF1BA1E2", "#FFD24726", "#FFF09609", 
                "#FF00A1F1", "#FF7E3878", "#FF00ABA9", "#FF647687",
                "#FFE51400", "#FFE3008C", "#FF00B294", "#FF8CBF26"
            };

            var currentIndex = System.Array.IndexOf(colors, app.Background);
            app.Background = colors[(currentIndex + 1) % colors.Length];

            await SaveSettingsAsync();
        }

        private async Task SaveSettingsAsync()
        {
            await _settingsService.SaveTileConfigurationAsync(InstalledApps);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
