using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using eStarter.Core;
using eStarter.Models;
using eStarter.Services;

namespace eStarter.ViewModels
{
    public class MainViewModel
    {
        public ObservableCollection<AppEntry> InstalledApps { get; } = new ObservableCollection<AppEntry>();
        public ICommand RefreshCommand { get; }
        public ICommand InstallCommand { get; }
        public ICommand LaunchCommand { get; }
        public ICommand ChangeTileSizeCommand { get; }
        public ICommand ChangeTileColorCommand { get; }

        private readonly AppManager _manager;
        private readonly ISettingsService _settingsService;

        public MainViewModel()
        {
            var installer = new AppInstaller();
            _manager = new AppManager(installer);
            _settingsService = new SettingsService();

            RefreshCommand = new RelayCommand(_ => Refresh());
            InstallCommand = new RelayCommand(async _ => await InstallAsync());
            LaunchCommand = new RelayCommand(param => LaunchApp((param as AppEntry)?.Id));
            ChangeTileSizeCommand = new RelayCommand(async param => await ChangeTileSizeAsync(param as AppEntry));
            ChangeTileColorCommand = new RelayCommand(async param => await ChangeTileColorAsync(param as AppEntry));

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            // Try to load saved configuration first
            var savedApps = await _settingsService.LoadTileConfigurationAsync();
            var savedList = savedApps.ToList();
            
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
        }

        private void Refresh()
        {
            InstalledApps.Clear();
            foreach (var app in _manager.GetInstalledApps())
                InstalledApps.Add(new AppEntry { Id = app, Name = app });

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
                    TileSize = TileSize.Medium
                });
                
                InstalledApps.Add(new AppEntry 
                { 
                    Id = "demo.calendar", 
                    Name = "Calendar", 
                    Description = "Stay organized", 
                    Background = "#FF1BA1E2",
                    TileSize = TileSize.Medium
                });
                
                InstalledApps.Add(new AppEntry 
                { 
                    Id = "demo.photos", 
                    Name = "Photos", 
                    Description = "Your memories", 
                    Background = "#FFD24726",
                    TileSize = TileSize.Wide
                });
                
                InstalledApps.Add(new AppEntry 
                { 
                    Id = "demo.music", 
                    Name = "Music", 
                    Description = "Groove to your favorites", 
                    Background = "#FFF09609",
                    TileSize = TileSize.Medium
                });
                
                InstalledApps.Add(new AppEntry 
                { 
                    Id = "demo.store", 
                    Name = "Store", 
                    Description = "Get apps", 
                    Background = "#FF00A1F1",
                    TileSize = TileSize.Medium
                });
                
                InstalledApps.Add(new AppEntry 
                { 
                    Id = "demo.news", 
                    Name = "News", 
                    Description = "Stay informed", 
                    BadgeCount = 12,
                    Background = "#FF7E3878",
                    TileSize = TileSize.Wide
                });
                
                InstalledApps.Add(new AppEntry 
                { 
                    Id = "demo.weather", 
                    Name = "Weather", 
                    Description = "72° Sunny", 
                    Background = "#FF00ABA9",
                    TileSize = TileSize.Medium
                });
                
                InstalledApps.Add(new AppEntry 
                { 
                    Id = "demo.settings", 
                    Name = "Settings", 
                    Description = "Personalize", 
                    Background = "#FF647687",
                    TileSize = TileSize.Small
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
                Refresh();
                await SaveSettingsAsync();
            }
        }

        private void LaunchApp(string? appId)
        {
            if (string.IsNullOrWhiteSpace(appId))
                return;

            var baseDir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "eStarter", "apps", appId);
            if (!Directory.Exists(baseDir))
                return;

            // Find an executable in the app folder (top-level)
            var exe = Directory.EnumerateFiles(baseDir, "*.exe", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (exe == null)
                return;

            try
            {
                var psi = new ProcessStartInfo(exe)
                {
                    WorkingDirectory = Path.GetDirectoryName(exe) ?? baseDir,
                    UseShellExecute = true
                };
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
    }
}
