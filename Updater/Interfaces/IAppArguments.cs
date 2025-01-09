namespace Updater.Interfaces;

public interface IAppArguments
{
    string[] Args { get; }
    public string VersionNumber { get; set; }
    public string GitHubRepo { get; set; }
}