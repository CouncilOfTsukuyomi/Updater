﻿using System;
using System.Threading;
using Avalonia;
using Avalonia.ReactiveUI;
using CommonLib.Interfaces;
using CommonLib.Services;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Updater.Extensions;
using Updater.Interfaces;
using Updater.Services;

namespace Updater;

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

                // Build the service provider but do not heavily validate args here;
                // that logic will be done in App.OnFrameworkInitializationCompleted.
                services.AddSingleton<IAppArguments>(new AppArguments(args));
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