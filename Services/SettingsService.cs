using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using eStarter.Models;

namespace eStarter.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly string _settingsPath;

        public SettingsService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var estarterPath = Path.Combine(appDataPath, "eStarter");
            Directory.CreateDirectory(estarterPath);
            _settingsPath = Path.Combine(estarterPath, "tiles.json");
        }

        public async Task SaveTileConfigurationAsync(IEnumerable<AppEntry> apps)
        {
            try
            {
                var json = JsonSerializer.Serialize(apps, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                await File.WriteAllTextAsync(_settingsPath, json);
            }
            catch (Exception ex)
            {
                // Log error - in production, use proper logging framework
                System.Diagnostics.Debug.WriteLine($"Failed to save tile configuration: {ex.GetType().Name} - {ex.Message}");
            }
        }

        public async Task<IEnumerable<AppEntry>> LoadTileConfigurationAsync()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                    return new List<AppEntry>();

                var json = await File.ReadAllTextAsync(_settingsPath);
                var apps = JsonSerializer.Deserialize<List<AppEntry>>(json);
                return apps ?? new List<AppEntry>();
            }
            catch (Exception ex)
            {
                // Log error and return empty list - in production, use proper logging framework
                System.Diagnostics.Debug.WriteLine($"Failed to load tile configuration: {ex.GetType().Name} - {ex.Message}");
                return new List<AppEntry>();
            }
        }
    }
}
