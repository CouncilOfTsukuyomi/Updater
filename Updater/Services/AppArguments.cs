using Updater.Interfaces;

namespace Updater.Services;

public class AppArguments : IAppArguments
{
    public AppArguments(string[] args)
    {
        Args = args;

        if (args.Length > 0)
        {
            VersionNumber = args[0];
        }

        if (args.Length > 1)
        {
            GitHubRepo = args[1];
        }
    }

    public string[] Args { get; }

    public string VersionNumber { get; set; } = string.Empty;
    public string GitHubRepo { get; set; } = string.Empty;
    public string InstallationPath { get; set; } = string.Empty;
    public string ProgramToRunAfterInstallation { get; set; } = string.Empty;
}