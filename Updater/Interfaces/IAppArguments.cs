namespace Updater.Interfaces;

public interface IAppArguments
{
    string[] Args { get; }
    public string VersionNumber { get; set; }
    public string GitHubRepo { get; set; }
    public string InstallationPath { get; set; }
    public string ProgramToRunAfterInstallation { get; set; }
    public bool EnableSentry { get; set; }
}