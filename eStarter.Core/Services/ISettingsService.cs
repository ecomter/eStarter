using eStarter.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace eStarter.Services
{
    public interface ISettingsService
    {
        Task SaveTileConfigurationAsync(IEnumerable<AppEntry> apps);
        Task<IEnumerable<AppEntry>> LoadTileConfigurationAsync();

        Task SaveAppSettingsAsync(AppSettings settings);
        Task<AppSettings> LoadAppSettingsAsync();
    }
}
