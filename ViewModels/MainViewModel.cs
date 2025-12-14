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

namespace eStarter.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<AppEntry> InstalledApps { get; } = new ObservableCollection<AppEntry>();
        public ICommand RefreshCommand { get; }
        public ICommand InstallCommand { get; }
        public ICommand LaunchCommand { get; }
        public ICommand ChangeTileSizeCommand { get; }
        public ICommand ChangeTileColorCommand { get; }
        public ICommand ChangeThemeCommand { get; }
        public ICommand ChangeAccentColorCommand { get; }

        private readonly AppManager _manager;
        private readonly ISettingsService _settingsService;
        private AppSettings _currentSettings = new AppSettings();

        public AppSettings CurrentSettings
        {
            get => _currentSettings;
            set { _currentSettings = value; OnPropertyChanged(); }
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

            // Initialize async but handle exceptions properly
            Task.Run(async () => await InitializeAsync());
        }

        private async Task InitializeAsync()
        {
            // Load App Settings
            CurrentSettings = await _settingsService.LoadAppSettingsAsync();
            Application.Current.Dispatcher.Invoke(() => ApplyTheme(CurrentSettings));

            // Try to load saved configuration first
            var savedApps = await _settingsService.LoadTileConfigurationAsync();
            var savedList = savedApps.ToList();
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (savedList.Any())
                {
                    foreach (var app in savedList)
                        InstalledApps.Add(app);
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
                    Name = "Mail", 
                    Description = "Your messages", 
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
                    Name = "Calendar", 
                    Description = "Stay organized", 
                    Background = "#FF1BA1E2",
                    TileSize = TileSize.Medium,
                    Publisher = "Microsoft Corporation",
                    Category = "Productivity"
                });
                
                InstalledApps.Add(new AppEntry 
                { 
                    Id = "demo.photos", 
                    Name = "Photos", 
                    Description = "Your memories", 
                    Background = "#FFD24726",
                    TileSize = TileSize.Wide,
                    Publisher = "Microsoft Corporation",
                    Category = "Photo & Video"
                });
                
                InstalledApps.Add(new AppEntry 
                { 
                    Id = "demo.music", 
                    Name = "Music", 
                    Description = "Groove to your favorites", 
                    Background = "#FFF09609",
                    TileSize = TileSize.Medium,
                    Publisher = "Microsoft Corporation",
                    Category = "Music"
                });
                
                InstalledApps.Add(new AppEntry 
                { 
                    Id = "demo.store", 
                    Name = "Store", 
                    Description = "Get apps", 
                    Background = "#FF00A1F1",
                    TileSize = TileSize.Medium,
                    Publisher = "Microsoft Corporation",
                    Category = "Shopping"
                });
                
                InstalledApps.Add(new AppEntry 
                { 
                    Id = "demo.news", 
                    Name = "News", 
                    Description = "Stay informed", 
                    BadgeCount = 12,
                    Background = "#FF7E3878",
                    TileSize = TileSize.Wide,
                    Publisher = "Microsoft Corporation",
                    Category = "News & Weather"
                });
                
                InstalledApps.Add(new AppEntry 
                { 
                    Id = "demo.weather", 
                    Name = "Weather", 
                    Description = "72° Sunny", 
                    Background = "#FF00ABA9",
                    TileSize = TileSize.Medium,
                    Publisher = "Microsoft Corporation",
                    Category = "News & Weather"
                });

                InstalledApps.Add(new AppEntry
                {
                    Id = "demo.settings",
                    Name = "Settings",
                    Description = "Personalize",
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
                    Name = "About",
                    Description = "About this app",
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
                    Name = "Settings",
                    Description = "Personalize the app",
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
                return;

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
                return;

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
