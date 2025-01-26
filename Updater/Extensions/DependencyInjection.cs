using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PenumbraModForwarder.Common.Extensions;
using PenumbraModForwarder.Common.Interfaces;
using PenumbraModForwarder.Common.Services;
using Updater.Interfaces;
using Updater.Services;
using Updater.ViewModels;
using Updater.Views;

namespace Updater.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddAutoMapper(cfg =>
        {
            cfg.AddProfile<ConvertConfiguration>();
        });
        
        services.SetupLogging();
        services.AddHttpClient<IStaticResourceService, StaticResourceService>();
        services.AddSingleton<IGetBackgroundInformation, GetBackgroundInformation>();
        services.AddSingleton<IFileStorage, FileStorage>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IUpdateService, UpdateService>();

        services.AddSingleton<IAria2Service>(_ => new Aria2Service(AppContext.BaseDirectory));
        
        services.AddSingleton<IDownloadAndInstallUpdates>(serviceProvider =>
        {
            var aria2Service = serviceProvider.GetRequiredService<IAria2Service>();
            var updateService = serviceProvider.GetRequiredService<IUpdateService>();
            var appArgs = serviceProvider.GetRequiredService<IAppArguments>();

            return new DownloadAndInstallUpdates(aria2Service, updateService, appArgs);
        });
        
        services.AddSingleton<IInstallUpdate, InstallUpdate>();
        
        // Views
        services.AddSingleton<ErrorWindow>();
        services.AddSingleton<MainWindow>();
        
        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<ErrorWindowViewModel>();

        return services;
    }
    
    private static void SetupLogging(this IServiceCollection services)
    {
        Logging.ConfigureLogging(services, "Updater");
    }

    public static void EnableSentryLogging()
    {
        Console.WriteLine("EnableSentryLogging");
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .AddEnvironmentVariables()
            .Build();

        var sentryDns = configuration["SENTRY_DSN"];
        if (string.IsNullOrWhiteSpace(sentryDns))
        {
            Console.WriteLine("No SENTRY_DSN provided. Skipping Sentry enablement.");
            return;
        }

        Logging.EnableSentry(sentryDns, "Updater");
    }

    public static void DisableSentryLogging()
    {
        Console.WriteLine("DisableSentryLogging");
        Logging.DisableSentry("Updater");
    }
}