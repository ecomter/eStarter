using eStarter.Models;

namespace eStarter.Services
{
    public interface ISettingsService
    {
        Task SaveTileConfigurationAsync(IEnumerable<AppEntry> apps);
        Task<IEnumerable<AppEntry>> LoadTileConfigurationAsync();
    }
}
