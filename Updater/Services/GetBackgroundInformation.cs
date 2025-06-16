using System.Threading.Tasks;
using CommonLib.Interfaces;
using CommonLib.Models;
using NLog;
using Updater.Interfaces;

namespace Updater.Services;

public class GetBackgroundInformation : IGetBackgroundInformation
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private readonly IStaticResourceService _staticResourceService;

    public GetBackgroundInformation(IStaticResourceService staticResourceService)
    {
        _staticResourceService = staticResourceService;
    }

    public async Task<(GithubStaticResources.InformationJson?, GithubStaticResources.UpdaterInformationJson?)> GetResources()
    {
        var resources = await _staticResourceService.GetResourcesUsingGithubApiAsync();
        return resources;
    }
}