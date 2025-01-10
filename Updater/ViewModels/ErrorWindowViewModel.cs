using NLog;

namespace Updater.ViewModels;

public class ErrorWindowViewModel : ViewModelBase
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public string ErrorMessage { get; set; } = "Invalid arguments were provided.";

    public ErrorWindowViewModel()
    {
        _logger.Error("Displayed ErrorWindowViewModel due to invalid arguments.");
    }
}