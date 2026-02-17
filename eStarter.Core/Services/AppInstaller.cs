using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

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

            var fileName = Path.GetFileNameWithoutExtension(packagePath);
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var targetRoot = Path.Combine(baseDir, "eStarter", "apps", fileName);

            Directory.CreateDirectory(targetRoot);

            // Extract zip into targetRoot
            await Task.Run(() => ZipFile.ExtractToDirectory(packagePath, targetRoot, true)).ConfigureAwait(false);
        }
    }
}
