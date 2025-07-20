
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
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

    private string _applicationName = string.Empty;
    public string ApplicationName
    {
        get => _applicationName;
        set => this.RaiseAndSetIfChanged(ref _applicationName, value);
    }

    private List<VersionInfo> _allVersions = new();
    public List<VersionInfo> AllVersions
    {
        get => _allVersions;
        set => this.RaiseAndSetIfChanged(ref _allVersions, value);
    }

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

    private string _estimatedTimeRemaining = string.Empty;
    public string EstimatedTimeRemaining
    {
        get => _estimatedTimeRemaining;
        set => this.RaiseAndSetIfChanged(ref _estimatedTimeRemaining, value);
    }

    private string _networkStatus = "Connected";
    public string NetworkStatus
    {
        get => _networkStatus;
        set => this.RaiseAndSetIfChanged(ref _networkStatus, value);
    }

    private string _operatingSystem = string.Empty;
    public string OperatingSystem
    {
        get => _operatingSystem;
        set => this.RaiseAndSetIfChanged(ref _operatingSystem, value);
    }

    private string _systemArchitecture = string.Empty;
    public string SystemArchitecture
    {
        get => _systemArchitecture;
        set => this.RaiseAndSetIfChanged(ref _systemArchitecture, value);
    }

    private string _availableDiskSpace = string.Empty;
    public string AvailableDiskSpace
    {
        get => _availableDiskSpace;
        set => this.RaiseAndSetIfChanged(ref _availableDiskSpace, value);
    }

    private string _installationPath = string.Empty;
    public string InstallationPath
    {
        get => _installationPath;
        set => this.RaiseAndSetIfChanged(ref _installationPath, value);
    }

    private IDisposable? _imageTimer;

    public ReactiveCommand<Unit, Unit> UpdateCommand { get; }
    public ReactiveCommand<Unit, Unit> ViewReleaseNotesCommand { get; }

    private string _numberedVersionCurrent = string.Empty;
    private string _numberedVersionUpdated = string.Empty;

    public bool HasVersions => AllVersions.Count > 0;
    public bool HasMultipleVersions => AllVersions.Count > 1;
    public int VersionCount => AllVersions.Count;
    public int TotalChangeCount => AllVersions.Sum(v => v.Changes.Count);
    
    public string ChangelogTitle => HasMultipleVersions 
        ? $"What's New:"
        : AllVersions.FirstOrDefault()?.Version != null 
            ? $"What's New in {CleanVersionString(AllVersions.First().Version)}:"
            : "What's New:";

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
        
        if (_appArguments.EnableSentry)
        {
            DependencyInjection.EnableSentryLogging();
        }
        else
        {
            DependencyInjection.DisableSentryLogging();
        }
        
        var externalCurrentVersion = _appArguments.Args.Length > 0
            ? _appArguments.Args[0]
            : null;

        if (!string.IsNullOrWhiteSpace(externalCurrentVersion))
        {
            _numberedVersionCurrent = externalCurrentVersion;
        }
        
        _repository = _appArguments.Args.Length > 1
            ? _appArguments.Args[1]
            : string.Empty;

        ApplicationName = ExtractApplicationName(_repository);
        
        CurrentVersion = $"Current Version: v{_numberedVersionCurrent}";

        UpdateCommand = ReactiveCommand.CreateFromTask(PerformUpdateAsync);
        ViewReleaseNotesCommand = ReactiveCommand.Create(ViewReleaseNotes);

        InitializeSystemInformation();
        Begin();

        StatusText = "Loading...";
    }

    private void InitializeSystemInformation()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                OperatingSystem = $"Windows {Environment.OSVersion.Version}";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                OperatingSystem = "Linux";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                OperatingSystem = "macOS";
            }

            SystemArchitecture = RuntimeInformation.OSArchitecture.ToString();

            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory) ?? "C:\\");
                var freeSpaceGB = drive.AvailableFreeSpace / (1024 * 1024 * 1024);
                AvailableDiskSpace = $"{freeSpaceGB:F1} GB available";
            }
            catch
            {
                AvailableDiskSpace = "Unknown";
            }

            InstallationPath = Environment.CurrentDirectory;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize system information");
        }
    }

    private void ViewReleaseNotes()
    {
        try
        {
            _logger.Info("Opening release notes for repository: {Repository}", _repository);
        
            if (string.IsNullOrWhiteSpace(_repository))
            {
                _logger.Warn("Repository is empty, cannot open release notes");
                return;
            }
        
            var releaseUrl = $"https://github.com/{_repository}/releases";
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = releaseUrl,
                    UseShellExecute = true
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", releaseUrl);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", releaseUrl);
            }
        
            _logger.Info("Successfully opened release notes URL: {Url}", releaseUrl);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open release notes");
        }
    }

    private string ExtractApplicationName(string repository)
    {
        if (string.IsNullOrWhiteSpace(repository))
            return "Unknown Application";

        var lastSlashIndex = repository.LastIndexOf('/');
        if (lastSlashIndex >= 0 && lastSlashIndex < repository.Length - 1)
        {
            return repository.Substring(lastSlashIndex + 1);
        }

        return repository;
    }

    private static string CleanVersionString(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return version;
            
        var cleaned = version.Trim();
        if (cleaned.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned.Substring(1);
        }

        return cleaned;
    }

    private async Task PerformUpdateAsync()
    {
        try
        {
            _logger.Info("=== UPDATE COMMAND EXECUTED ===");
            _logger.Debug("Update process started");
            
            _logger.Info("Setting IsDownloading to true");
            IsDownloading = true;
            
            _logger.Info("Resetting progress indicators");
            DownloadProgress = 0;
            FormattedSpeed = string.Empty;
            FormattedSize = string.Empty;
            StatusText = "Starting update...";
            EstimatedTimeRemaining = string.Empty;
            
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
        
        _logger.Debug("Updating DownloadProgress from {Old} to {New}", DownloadProgress, progress.PercentComplete);
        DownloadProgress = progress.PercentComplete;
        
        _logger.Debug("Updating FormattedSpeed from '{Old}' to '{New}'", FormattedSpeed, progress.FormattedSpeed);
        FormattedSpeed = progress.FormattedSpeed;
        
        _logger.Debug("Updating FormattedSize from '{Old}' to '{New}'", FormattedSize, progress.FormattedSize);
        FormattedSize = progress.FormattedSize;
        
        if (!string.IsNullOrEmpty(progress.Status))
        {
            _logger.Debug("Updating StatusText from '{Old}' to '{New}'", StatusText, progress.Status);
            StatusText = progress.Status;
        }

        if (progress.PercentComplete > 0 && !string.IsNullOrEmpty(progress.FormattedSpeed))
        {
            var remainingPercent = 100 - progress.PercentComplete;
            if (remainingPercent > 0)
            {
                EstimatedTimeRemaining = $"~{remainingPercent * 2:F0}s remaining";
            }
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
    
        var latestVersion = await _updateService.GetMostRecentVersionAsync(_repository);
        UpdatedVersion = $"Updated Version: {latestVersion}";
        _numberedVersionUpdated = latestVersion;
    
        if (!CurrentVersion.Contains(latestVersion))
        {
            _logger.Info("Update available - Current: {Current}, Latest: {Latest}", _numberedVersionCurrent, latestVersion);
        
            await LoadChangelogAsync();
        
            StatusText = "Update Available - Click 'Update Now' to begin";
        }
        else
        {
            _logger.Info("No update needed - versions match");
            StatusText = "Up to date!";
        }
    }

    private async Task LoadChangelogAsync()
    {
        try
        {
            _logger.Debug("Loading changelog information");
        
            var allVersionsSinceCurrentValue = await _updateService.GetAllVersionInfoSinceCurrentAsync(_numberedVersionCurrent, _repository);
        
            if (allVersionsSinceCurrentValue?.Any() == true)
            {
                AllVersions = allVersionsSinceCurrentValue
                    .OrderByDescending(v => v.PublishedAt)
                    .ToList();
            
                _logger.Debug("Retrieved and sorted {VersionCount} versions with {TotalChanges} total changes", 
                    AllVersions.Count, 
                    AllVersions.Sum(v => v.Changes.Count));
            }
            else
            {
                var versionInfo = await _updateService.GetMostRecentVersionInfoAsync(_repository);
                if (versionInfo != null)
                {
                    AllVersions = new List<VersionInfo> { versionInfo };
                    _logger.Debug("Fallback: Retrieved single version info with {ChangeCount} changes", versionInfo.Changes.Count);
                }
            }
        
            this.RaisePropertyChanged(nameof(HasVersions));
            this.RaisePropertyChanged(nameof(HasMultipleVersions));
            this.RaisePropertyChanged(nameof(VersionCount));
            this.RaisePropertyChanged(nameof(TotalChangeCount));
            this.RaisePropertyChanged(nameof(ChangelogTitle));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load changelog information");
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