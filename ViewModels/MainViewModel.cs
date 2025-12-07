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

        private readonly AppManager _manager;

        public MainViewModel()
        {
            var installer = new AppInstaller();
            _manager = new AppManager(installer);

            RefreshCommand = new RelayCommand(_ => Refresh());
            InstallCommand = new RelayCommand(async _ => await InstallAsync());
            LaunchCommand = new RelayCommand(param => LaunchApp((param as AppEntry)?.Id));

            Refresh();
        }

        private void Refresh()
        {
            InstalledApps.Clear();
            foreach (var app in _manager.GetInstalledApps())
                InstalledApps.Add(new AppEntry { Id = app, Name = app });

            // Add demo tile if empty
            if (!InstalledApps.Any())
                InstalledApps.Add(new AppEntry { Id = "demo.calc", Name = "Calculator", Description = "Demo calculator", BadgeCount = 3, Background = "#FF4CAF50" });
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
    }
}
