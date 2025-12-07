using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

            return Directory.EnumerateDirectories(AppsDirectory).Select(Path.GetFileName);
        }
    }
}
