using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CommonLib.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Updater.ViewModels;
using Updater.Views;
using MainWindow = Updater.Views.MainWindow;

namespace Updater;

public partial class App : Application
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly IServiceProvider _serviceProvider;

    public App()
    {
        try
        {
            _serviceProvider = Program.ServiceProvider;
            AvaloniaXamlLoader.Load(this);
        }
        catch (Exception ex)
        {
            _logger.Fatal(ex, "Failed to initialize ServiceProvider");
            Environment.Exit(1);
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Retrieve command-line arguments.
            var args = Environment.GetCommandLineArgs();
            _logger.Info("Command-line arguments: {Args}", string.Join(", ", args));

            _logger.Debug("Ammount of CLI Args: {Number}", args.Length);
            // Expecting 6 total arguments: 
            // [0]=DLL path, [1]=version, [2]=repo, [3]=install path, [4]=enableSentry bool, [5]=programToRunAfterInstall
            if (args.Length < 5)
            {
                _logger.Error("Invalid number of arguments passed. Showing error window instead.");
                desktop.MainWindow = new ErrorWindow
                {
                    DataContext = ActivatorUtilities.CreateInstance<ErrorWindowViewModel>(_serviceProvider)
                };
            }
            else
            {
                var appArgs = (IAppArguments)_serviceProvider.GetService(typeof(IAppArguments))!;

                appArgs.VersionNumber = args[1];
                appArgs.GitHubRepo = args[2];
                appArgs.InstallationPath = args[3];

                if (!bool.TryParse(args[4], out bool enableSentry))
                {
                    _logger.Warn("Could not parse enableSentry as boolean. Defaulting to false.");
                    enableSentry = false;
                }
                appArgs.EnableSentry = enableSentry;

                // Only parse args[5] if it's present; otherwise default to empty or another fallback.
                if (args.Length > 5)
                {
                    appArgs.ProgramToRunAfterInstallation = args[5];
                }
                else
                {
                    appArgs.ProgramToRunAfterInstallation = string.Empty;
                }

                _logger.Debug("All arguments look good. Showing main window...");
                desktop.MainWindow = new MainWindow
                {
                    DataContext = ActivatorUtilities.CreateInstance<MainWindowViewModel>(_serviceProvider)
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}