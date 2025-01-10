using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Updater.Interfaces;
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
                // Retrieve args from environment, or from IAppArguments, as preferred.
                var args = Environment.GetCommandLineArgs();
                _logger.Debug("Command-line arguments: {Args}", string.Join(", ", args));

                // Perform more thorough argument checks here.
                // Example: We expect 5 arguments.
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
                    // Acquire the stored IAppArguments and parse them as needed.
                    var appArgs = (IAppArguments)_serviceProvider.GetService(typeof(IAppArguments))!;

                    // 1) VersionNumber
                    appArgs.VersionNumber = args[0];
                    // 2) GitHubRepo
                    appArgs.GitHubRepo = args[1];
                    // 3) InstallationPath
                    appArgs.InstallationPath = args[2];
                    
                    // 4) enableSentry
                    if (!bool.TryParse(args[3], out bool enableSentry))
                    {
                        _logger.Warn("Could not parse enableSentry as boolean. Defaulting to false.");
                        enableSentry = false;
                    }
                    appArgs.EnableSentry = enableSentry;

                    // 5) ProgramToRunAfterInstallation
                    appArgs.ProgramToRunAfterInstallation = args[4];

                    // If everything is valid, show the main window.
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