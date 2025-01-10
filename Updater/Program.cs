using System;
using System.Threading;
using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Updater.Extensions;
using Updater.Interfaces;
using Updater.Services;

namespace Updater
{
    public class Program
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        [STAThread]
        public static void Main(string[] args)
        {
            bool isNewInstance;
            using (var mutex = new Mutex(true, "Updater", out isNewInstance))
            {
                if (!isNewInstance)
                {
                    Console.WriteLine("Another instance of Updater is already running. Exiting...");
                    return;
                }

                try
                {
                    var services = new ServiceCollection();
                    var appArgs = new AppArguments(args);

                    // Expecting arguments in order:
                    // [0] VersionNumber, [1] GitHubRepo, [2] InstallationPath,
                    // [3] enableSentry, [4] ProgramToRunAfterInstallation
                    if (args.Length < 5)
                    {
                        _logger.Fatal("Invalid number of arguments passed. Exiting...");
                        Environment.Exit(1);
                    }

                    appArgs.VersionNumber = args[0];
                    appArgs.GitHubRepo = args[1];
                    appArgs.InstallationPath = args[2];

                    if (!bool.TryParse(args[3], out var enableSentry))
                    {
                        _logger.Error("Could not parse enableSentry as boolean. Defaulting to false.");
                        enableSentry = false;
                    }
                    appArgs.EnableSentry = enableSentry;

                    appArgs.ProgramToRunAfterInstallation = args[4];

                    services.AddSingleton<IAppArguments>(appArgs);
                    services.AddApplicationServices();

                    ServiceProvider = services.BuildServiceProvider();

                    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
                }
                catch (Exception ex)
                {
                    _logger.Fatal(ex, "Application failed to start");
                    Environment.Exit(1);
                }
            }
        }

        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace()
                .UseReactiveUI();
    }
}