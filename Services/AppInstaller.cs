using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;
using eStarter.Models;

namespace eStarter.Services
{
    public class AppInstaller : IAppInstaller
    {
        public async Task InstallAsync(string packagePath)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
                throw new ArgumentException("packagePath is required", nameof(packagePath));

            if (!File.Exists(packagePath))
                throw new FileNotFoundException("Package not found", packagePath);

            // Determine app ID: read manifest.json from the archive first,
            // fall back to the file name without extension.
            var appId = Path.GetFileNameWithoutExtension(packagePath);
            try
            {
                using var zip = ZipFile.OpenRead(packagePath);
                var manifestEntry = zip.GetEntry("manifest.json");
                if (manifestEntry != null)
                {
                    using var stream = manifestEntry.Open();
                    var manifest = await JsonSerializer.DeserializeAsync<AppManifest>(stream).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(manifest?.Id))
                        appId = manifest.Id;
                }
            }
            catch
            {
                // Could not read manifest — use filename as appId.
            }

            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var targetRoot = Path.Combine(baseDir, "eStarter", "apps", appId);

            Directory.CreateDirectory(targetRoot);

            // Extract zip into targetRoot (overwrite existing files).
            await Task.Run(() => ZipFile.ExtractToDirectory(packagePath, targetRoot, true)).ConfigureAwait(false);
        }
    }
}
