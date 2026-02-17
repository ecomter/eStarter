using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using eStarter.Models;
using eStarter.Services;

namespace eStarter.Core
{
    public class AppManager
    {
        private readonly IAppInstaller _installer;

        public AppManager(IAppInstaller installer)
        {
            _installer = installer;
        }

        public string AppsDirectory
        {
            get
            {
                var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(baseDir, "eStarter", "apps");
            }
        }

        public async Task InstallAsync(string packagePath)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
                throw new ArgumentException("packagePath is required", nameof(packagePath));

            Directory.CreateDirectory(AppsDirectory);
            await _installer.InstallAsync(packagePath).ConfigureAwait(false);
        }

        public IEnumerable<string> GetInstalledApps()
        {
            if (!Directory.Exists(AppsDirectory))
                return Enumerable.Empty<string>();

            return Directory.EnumerateDirectories(AppsDirectory)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .Cast<string>();
        }

        /// <summary>
        /// Retrieves installed apps as rich AppEntry objects by reading manifest.json.
        /// </summary>
        public IEnumerable<AppEntry> GetInstalledAppEntries()
        {
            if (!Directory.Exists(AppsDirectory))
                yield break;

            foreach (var dir in Directory.EnumerateDirectories(AppsDirectory))
            {
                var dirName = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(dirName)) continue;

                var manifestPath = Path.Combine(dir, "manifest.json");
                AppEntry? entry = null;

                // Try to load from manifest
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        var json = File.ReadAllText(manifestPath);
                        var manifest = JsonSerializer.Deserialize<AppManifest>(json);
                        if (manifest != null)
                        {
                            entry = new AppEntry
                            {
                                Id = !string.IsNullOrWhiteSpace(manifest.Id) ? manifest.Id : dirName,
                                Name = !string.IsNullOrWhiteSpace(manifest.Name) ? manifest.Name : dirName,
                                Description = manifest.Description,
                                Publisher = manifest.Publisher ?? "Unknown",
                                Version = manifest.Version ?? "1.0.0",
                                Category = manifest.Category ?? "General",
                                Background = !string.IsNullOrWhiteSpace(manifest.Background) ? manifest.Background : "#FF0078D7",
                                ExePath = manifest.ExePath,
                                Arguments = manifest.Arguments,
                                InstallDate = Directory.GetCreationTime(dir)
                            };

                            if (Enum.TryParse<TileSize>(manifest.TileSize, true, out var size))
                            {
                                entry.TileSize = size;
                            }
                        }
                    }
                    catch
                    {
                        // Log error or ignore malformed manifest
                    }
                }

                // Fallback if no manifest or failed to load
                if (entry == null)
                {
                    entry = new AppEntry
                    {
                        Id = dirName,
                        Name = dirName,
                        Description = "Local Application",
                        Publisher = "Local",
                        InstallDate = Directory.GetCreationTime(dir),
                        Background = "#FF666666"
                    };
                }

                // Ensure ExePath is set if not provided by manifest
                if (string.IsNullOrEmpty(entry.ExePath))
                {
                    var exe = Directory.EnumerateFiles(dir, "*.exe", SearchOption.TopDirectoryOnly).FirstOrDefault();
                    if (exe != null)
                    {
                        entry.ExePath = Path.GetFileName(exe); // Store relative path
                    }
                }

                // Calculate size
                try
                {
                    entry.Size = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
                }
                catch { }

                yield return entry;
            }
        }

        /// <summary>
        /// 删除已安装应用的数据目录（同步）。如果目录不存在返回 false；删除失败会抛出具体异常。
        /// </summary>
        public bool RemoveApp(string appId)
        {
            if (string.IsNullOrWhiteSpace(appId))
                throw new ArgumentException("appId is required", nameof(appId));

            var dir = Path.Combine(AppsDirectory, appId);
            if (!Directory.Exists(dir))
                return false;

            try
            {
                Directory.Delete(dir, recursive: true);
                return true;
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException($"Failed to remove app data for '{appId}' due to an IO error.", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new InvalidOperationException($"Failed to remove app data for '{appId}' due to insufficient permissions.", ex);
            }
        }

        /// <summary>
        /// 异步删除已安装应用的数据目录。返回 true 表示已删除，false 表示目录不存在。
        /// 抛出 InvalidOperationException 表示删除时发生错误（例如权限或 IO 问题）。
        /// </summary>
        public async Task<bool> RemoveAppAsync(string appId)
        {
            // Wrap the synchronous deletion in a Task to avoid blocking callers
            return await Task.Run(() => RemoveApp(appId)).ConfigureAwait(false);
        }
    }
}
