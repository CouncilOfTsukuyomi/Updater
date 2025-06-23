using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using CommonLib.Interfaces;
using CommonLib.Models;
using NLog;
using ReactiveUI;
using Updater.Extensions;
using Updater.Interfaces;

namespace Updater.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private readonly IGetBackgroundInformation _getBackgroundInformation;
    private readonly IUpdateService _updateService;
    private readonly IDownloadAndInstallUpdates _downloadAndInstallUpdates;
    private readonly IAppArguments _appArguments;
    private readonly IConfigurationService _configurationService;
    private readonly IInstallUpdate _installUpdate;

    private readonly Random _random = new();

    private int _lastIndex1 = -1;
    private int _lastIndex2 = -1;
    
    private readonly string _repository = string.Empty;

    private GithubStaticResources.InformationJson? _infoJson;
    public GithubStaticResources.InformationJson? InfoJson
    {
        get => _infoJson;
        set => this.RaiseAndSetIfChanged(ref _infoJson, value);
    }

    private GithubStaticResources.UpdaterInformationJson? _updaterInfoJson;
    public GithubStaticResources.UpdaterInformationJson? UpdaterInfoJson
    {
        get => _updaterInfoJson;
        set => this.RaiseAndSetIfChanged(ref _updaterInfoJson, value);
    }

    private string[]? _backgroundImages;
    public string[]? BackgroundImages
    {
        get => _backgroundImages;
        set => this.RaiseAndSetIfChanged(ref _backgroundImages, value);
    }

    private string? _currentImage;
    public string? CurrentImage
    {
        get => _currentImage;
        set => this.RaiseAndSetIfChanged(ref _currentImage, value);
    }

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    private string _currentVersion = string.Empty;
    public string CurrentVersion
    {
        get => _currentVersion;
        set => this.RaiseAndSetIfChanged(ref _currentVersion, value);
    }

    private string _updatedVersion = string.Empty;
    public string UpdatedVersion
    {
        get => _updatedVersion;
        set => this.RaiseAndSetIfChanged(ref _updatedVersion, value);
    }

    // Progress properties
    private double _downloadProgress = 0;
    public double DownloadProgress
    {
        get => _downloadProgress;
        set => this.RaiseAndSetIfChanged(ref _downloadProgress, value);
    }

    private string _formattedSpeed = string.Empty;
    public string FormattedSpeed
    {
        get => _formattedSpeed;
        set => this.RaiseAndSetIfChanged(ref _formattedSpeed, value);
    }

    private string _formattedSize = string.Empty;
    public string FormattedSize
    {
        get => _formattedSize;
        set => this.RaiseAndSetIfChanged(ref _formattedSize, value);
    }

    private bool _isDownloading = false;
    public bool IsDownloading
    {
        get => _isDownloading;
        set => this.RaiseAndSetIfChanged(ref _isDownloading, value);
    }

    private IDisposable? _imageTimer;

    public ReactiveCommand<Unit, Unit> UpdateCommand { get; }

    private string _numberedVersionCurrent = string.Empty;
    private string _numberedVersionUpdated = string.Empty;

    public MainWindowViewModel(
        IGetBackgroundInformation getBackgroundInformation,
        IUpdateService updateService,
        IDownloadAndInstallUpdates downloadAndInstallUpdates,
        IAppArguments appArguments,
        IConfigurationService configurationService, 
        IInstallUpdate installUpdate)
    {
        _logger.Debug("Constructing MainWindowViewModel...");

        _getBackgroundInformation = getBackgroundInformation;
        _updateService = updateService;
        _downloadAndInstallUpdates = downloadAndInstallUpdates;
        _appArguments = appArguments;
        _configurationService = configurationService;
        _installUpdate = installUpdate;

        // Respect the enableSentry command-line argument if present
        if (_appArguments.EnableSentry)
        {
            DependencyInjection.EnableSentryLogging();
        }
        else
        {
            DependencyInjection.DisableSentryLogging();
        }

        // Extract version from first arg if present
        var externalCurrentVersion = _appArguments.Args.Length > 0
            ? _appArguments.Args[0]
            : null;

        if (!string.IsNullOrWhiteSpace(externalCurrentVersion))
        {
            _numberedVersionCurrent = externalCurrentVersion;
        }

        // Extract repository from second arg
        _repository = _appArguments.Args.Length > 1
            ? _appArguments.Args[1]
            : string.Empty;
        
        CurrentVersion = $"Current Version: v{_numberedVersionCurrent}";

        UpdateCommand = ReactiveCommand.CreateFromTask(PerformUpdateAsync);

        Begin();

        StatusText = "Waiting for Update...";
    }

    private async Task PerformUpdateAsync()
    {
        try
        {
            _logger.Info("=== UPDATE COMMAND EXECUTED ===");
            _logger.Debug("Update button clicked");
            
            _logger.Info("Setting IsDownloading to true");
            IsDownloading = true;
            
            // Reset progress indicators
            _logger.Info("Resetting progress indicators");
            DownloadProgress = 0;
            FormattedSpeed = string.Empty;
            FormattedSize = string.Empty;
            StatusText = "Starting update...";

            // Create progress reporter
            _logger.Info("Creating progress reporter");
            var progress = new Progress<DownloadProgress>(OnDownloadProgressChanged);

            _logger.Info("Calling DownloadAndInstallAsync with version: {Version}", _numberedVersionCurrent);
            var (success, downloadPath) = await _downloadAndInstallUpdates
                .DownloadAndInstallAsync(_numberedVersionCurrent, progress);

            _logger.Info("DownloadAndInstallAsync completed with success: {Success}", success);

            if (!success)
            {
                _logger.Warn("Download failed");
                StatusText = "Download Failed";
                IsDownloading = false;
                return;
            }

            _logger.Info("Download successful, starting installation");
            StatusText = "Download Complete!";
            await Task.Delay(TimeSpan.FromSeconds(2));
            StatusText = "Installing Update...";
            var installed = await _installUpdate.StartInstallationAsync(downloadPath);
            if (installed)
            {
                _logger.Info("Installation completed successfully");
                StatusText = "Installation Complete!";
                await Task.Delay(TimeSpan.FromSeconds(2));
                Environment.Exit(0);
            }
            else
            {
                _logger.Warn("Installation failed");
                StatusText = "Installation Failed";
                IsDownloading = false;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in PerformUpdateAsync: {Message}", ex.Message);
            StatusText = "Update Failed";
            IsDownloading = false;
        }
    }

    private void OnDownloadProgressChanged(DownloadProgress progress)
    {
        _logger.Debug("=== UI PROGRESS UPDATE ===");
        _logger.Debug("Received progress - Percent: {Percent}%, Status: {Status}", progress.PercentComplete, progress.Status);
        _logger.Debug("FormattedSpeed: {Speed}, FormattedSize: {Size}", progress.FormattedSpeed, progress.FormattedSize);
        
        // Update UI properties based on progress
        _logger.Debug("Updating DownloadProgress from {Old} to {New}", DownloadProgress, progress.PercentComplete);
        DownloadProgress = progress.PercentComplete;
        
        _logger.Debug("Updating FormattedSpeed from '{Old}' to '{New}'", FormattedSpeed, progress.FormattedSpeed);
        FormattedSpeed = progress.FormattedSpeed;
        
        _logger.Debug("Updating FormattedSize from '{Old}' to '{New}'", FormattedSize, progress.FormattedSize);
        FormattedSize = progress.FormattedSize;
        
        // Update status text with detailed information
        if (!string.IsNullOrEmpty(progress.Status))
        {
            _logger.Debug("Updating StatusText from '{Old}' to '{New}'", StatusText, progress.Status);
            StatusText = progress.Status;
        }
        
        _logger.Debug("=== END UI PROGRESS UPDATE ===");
    }

    private async Task Begin()
    {
        _logger.Debug("Begin() called for MainWindowViewModel");
        
        var (info, updater) = await _getBackgroundInformation.GetResources();
        InfoJson = info;
        UpdaterInfoJson = updater;

        if (UpdaterInfoJson?.Backgrounds?.Images != null)
        {
            BackgroundImages = UpdaterInfoJson.Backgrounds.Images;
        }
        StartImageRotation();

        // Pass the prerelease setting if your IUpdateService can handle it
        var latestVersion = await _updateService.GetMostRecentVersionAsync(_repository);
        UpdatedVersion = $"Updated Version: {latestVersion}";
        _numberedVersionUpdated = latestVersion;

        if (!CurrentVersion.Contains(latestVersion))
        {
            StatusText = "Update Needed...";
        }
    }

    private void StartImageRotation()
    {
        if (BackgroundImages == null || BackgroundImages.Length == 0) return;

        var initialIndex = _random.Next(BackgroundImages.Length);
        CurrentImage = BackgroundImages[initialIndex];
        _lastIndex1 = initialIndex;

        _imageTimer?.Dispose();

        _imageTimer = Observable.Interval(TimeSpan.FromSeconds(30))
            .Subscribe(_ =>
            {
                if (BackgroundImages.Length <= 2)
                {
                    CycleWithoutRandom();
                    return;
                }

                int newIndex;
                do
                {
                    newIndex = _random.Next(BackgroundImages.Length);
                } while (newIndex == _lastIndex1 || newIndex == _lastIndex2);

                CurrentImage = BackgroundImages[newIndex];
                _lastIndex2 = _lastIndex1;
                _lastIndex1 = newIndex;
            });
    }

    private void CycleWithoutRandom()
    {
        if (BackgroundImages == null) return;

        var currentIndex = Array.IndexOf(BackgroundImages, CurrentImage);
        var nextIndex = (currentIndex + 1) % BackgroundImages.Length;
        CurrentImage = BackgroundImages[nextIndex];
    }
}