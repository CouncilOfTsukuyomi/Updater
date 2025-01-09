using System.Threading.Tasks;

namespace Updater.Interfaces;

public interface IInstallUpdate
{
    Task<bool> StartInstallationAsync(string downloadedPath);
}