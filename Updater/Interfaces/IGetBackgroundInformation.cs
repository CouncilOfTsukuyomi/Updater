using System.Threading.Tasks;
using CommonLib.Models;

namespace Updater.Interfaces;

public interface IGetBackgroundInformation
{
    Task<(GithubStaticResources.InformationJson?, GithubStaticResources.UpdaterInformationJson?)> GetResources();
}