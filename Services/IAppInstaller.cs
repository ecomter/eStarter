using System.Threading.Tasks;

namespace eStarter.Services
{
    public interface IAppInstaller
    {
        Task InstallAsync(string packagePath);
    }
}
