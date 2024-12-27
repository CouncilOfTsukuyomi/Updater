﻿using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using PenumbraModForwarder.Common.Consts;
using PenumbraModForwarder.Common.Interfaces;
using PenumbraModForwarder.FileMonitor.Interfaces;
using PenumbraModForwarder.FileMonitor.Models;
using Serilog;
using SevenZipExtractor;
using ILogger = Serilog.ILogger;

namespace PenumbraModForwarder.FileMonitor.Services;

public sealed class FileWatcher : IFileWatcher, IDisposable
{
    private readonly List<FileSystemWatcher> _watchers;
    private readonly ConcurrentDictionary<string, DateTime> _fileQueue;
    private readonly IFileStorage _fileStorage;
    private readonly IConfigurationService _configurationService;
    private readonly string _stateFilePath = $@"{ConfigurationConsts.ConfigurationPath}\fileQueueState.json";

    private bool _disposed;
    private CancellationTokenSource _cancellationTokenSource;
    private Task _processingTask;
    private Timer _persistenceTimer;
    private readonly ILogger _logger;

    private static readonly Regex PreDtRegex = new(@"\[?(?i)pre[\s\-]?dt\]?", RegexOptions.Compiled);

    public FileWatcher(IFileStorage fileStorage, IConfigurationService configurationService)
    {
        _fileStorage = fileStorage;
        _configurationService = configurationService;
        _fileQueue = new ConcurrentDictionary<string, DateTime>();
        _watchers = new List<FileSystemWatcher>();
        _logger = Log.ForContext<FileWatcher>();
    }

    public event EventHandler<FileMovedEvent> FileMoved;
    public event EventHandler<FilesExtractedEventArgs> FilesExtracted;

    public async Task StartWatchingAsync(IEnumerable<string> paths)
    {
        try
        {
            await LoadStateAsync();

            foreach (var path in paths.Distinct())
            {
                var watcher = new FileSystemWatcher(path)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                    EnableRaisingEvents = true,
                };

                foreach (var extension in FileExtensionsConsts.AllowedExtensions)
                {
                    watcher.Filters.Add($"*{extension}");
                }

                watcher.Created += OnCreated;
                _watchers.Add(watcher);
                _logger.Information("Started watching directory: {Path}", path);
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _processingTask = ProcessQueueAsync(_cancellationTokenSource.Token);

            // Initialize the persistence timer to save the queue state every minute
            _persistenceTimer = new Timer(_ => PersistState(), null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "An error occurred in StartWatchingAsync.");
            throw;
        }
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        _fileQueue.TryAdd(e.FullPath, DateTime.UtcNow);
        _logger.Information("File added to queue: {FullPath}", e.FullPath);
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var filesToProcess = _fileQueue.Keys.ToList();

                foreach (var filePath in filesToProcess)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (IsFileReady(filePath))
                    {
                        await ProcessFileAsync(filePath, cancellationToken);
                    }
                    else if (DateTime.UtcNow - _fileQueue[filePath] > TimeSpan.FromSeconds(5))
                    {
                        _logger.Information("File is not ready for processing, will retry: {FullPath}", filePath);
                    }
                }

                await Task.Delay(500, cancellationToken);
            }
        }
        catch (TaskCanceledException)
        {
            _logger.Information("Processing queue was canceled.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "An error occurred in ProcessQueueAsync.");
        }
    }

    private async Task ProcessFileAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!_fileStorage.Exists(filePath))
        {
            _fileQueue.TryRemove(filePath, out _);
            PersistState();
            _logger.Warning("File no longer exists: {FullPath}", filePath);
            return;
        }

        if (!IsFileReady(filePath))
        {
            _logger.Information("File is not ready for processing: {FullPath}", filePath);
            return;
        }

        var extension = Path.GetExtension(filePath)?.ToLowerInvariant();

        if (FileExtensionsConsts.ModFileTypes.Contains(extension))
        {
            var movedFilePath = MoveFile(filePath);
            _fileQueue.TryRemove(filePath, out _);
            PersistState();

            var fileName = Path.GetFileName(movedFilePath);
            FileMoved?.Invoke(this, new FileMovedEvent(fileName, movedFilePath, Path.GetFileNameWithoutExtension(movedFilePath)));
        }
        else if (FileExtensionsConsts.ArchiveFileTypes.Contains(extension))
        {
            var movedFilePath = MoveFile(filePath);
            _fileQueue.TryRemove(filePath, out _);
            PersistState();

            await ProcessArchiveFileAsync(movedFilePath, cancellationToken);
        }
        else
        {
            _logger.Warning("Unhandled file type: {FullPath}", filePath);
            _fileQueue.TryRemove(filePath, out _);
            PersistState();
        }
    }

    private async Task ProcessArchiveFileAsync(string archivePath, CancellationToken cancellationToken)
    {
        try
        {
            var extractedFiles = new List<string>();

            using (var archiveFile = new ArchiveFile(archivePath))
            {
                var skipPreDt = (bool)_configurationService.ReturnConfigValue(config => config.BackgroundWorker.SkipPreDt);

                // Get the base directory from config, so we can place files there.
                var baseDirectory = (string)_configurationService.ReturnConfigValue(c => c.BackgroundWorker.ModFolderPath);

                var modEntries = archiveFile.Entries.Where(entry =>
                {
                    var entryExtension = Path.GetExtension(entry.FileName)?.ToLowerInvariant();

                    // Check if the entry is a mod file
                    if (!FileExtensionsConsts.ModFileTypes.Contains(entryExtension))
                    {
                        return false;
                    }

                    // If skipPreDt is true, check if the file is under a Pre-Dt folder
                    if (skipPreDt)
                    {
                        var entryPath = entry.FileName;
                        var directories = entryPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

                        if (directories.Any(dir => PreDtRegex.IsMatch(dir)))
                        {
                            _logger.Information("Skipping file in Pre-Dt folder: {FileName}", entry.FileName);
                            return false;
                        }
                    }

                    return true;
                }).ToList();

                if (modEntries.Any())
                {
                    _logger.Information("Archive contains mod files: {ArchiveFileName}", Path.GetFileName(archivePath));

                    foreach (var entry in modEntries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Extract the mod file to the dynamic mod path
                        var destinationPath = Path.Combine(baseDirectory, entry.FileName);
                        var destinationDir = Path.GetDirectoryName(destinationPath);
                        _fileStorage.CreateDirectory(destinationDir);

                        _logger.Information("Extracting mod file: {FileName} to {DestinationPath}", entry.FileName, destinationPath);

                        entry.Extract(destinationPath);
                        extractedFiles.Add(destinationPath);

                        _logger.Information("Extraction complete for: {FileName}", entry.FileName);
                    }
                }
                else
                {
                    _logger.Information("No mod files found in archive: {ArchiveFileName}", Path.GetFileName(archivePath));
                }
            }

            // Give time for the extraction process to complete before any further steps
            await Task.Delay(100, cancellationToken);

            if (extractedFiles.Any())
            {
                var archiveFileName = Path.GetFileName(archivePath);
                FilesExtracted?.Invoke(this, new FilesExtractedEventArgs(archiveFileName, extractedFiles));
            }

            var shouldDelete = (bool)_configurationService.ReturnConfigValue(config => config.BackgroundWorker.AutoDelete);

            if (shouldDelete)
            {
                DeleteFileWithRetry(archivePath);
                _logger.Information("Deleted archive after extraction: {ArchiveFileName}", Path.GetFileName(archivePath));
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Information("Processing of archive was canceled: {ArchiveFileName}", Path.GetFileName(archivePath));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error processing archive: {ArchiveFileName}", Path.GetFileName(archivePath));
        }
    }

    private string MoveFile(string filePath)
    {
        // Use the config-based mod path here instead of a fixed directory
        var modPath = (string)_configurationService.ReturnConfigValue(c => c.BackgroundWorker.ModFolderPath);

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        var destinationFolder = Path.Combine(modPath, fileNameWithoutExtension);
        _fileStorage.CreateDirectory(destinationFolder);
        var destinationPath = Path.Combine(destinationFolder, Path.GetFileName(filePath));

        // Copy and delete to simulate a move
        _fileStorage.CopyFile(filePath, destinationPath, overwrite: true);
        DeleteFileWithRetry(filePath);

        _logger.Information("File moved: {SourcePath} to {DestinationPath}", filePath, destinationPath);

        return destinationPath;
    }

    private void DeleteFileWithRetry(string filePath, int maxAttempts = 3, int delayMilliseconds = 500)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                _fileStorage.Delete(filePath);
                _logger.Information("Deleted file on attempt {Attempt}: {FilePath}", attempt, filePath);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                _logger.Warning("Attempt {Attempt} to delete file failed: {FilePath}. Retrying in {Delay}ms...", attempt, filePath, delayMilliseconds);
                Thread.Sleep(delayMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete file: {FilePath}", filePath);
                throw;
            }
        }

        // Final attempt
        try
        {
            _fileStorage.Delete(filePath);
            _logger.Information("Deleted file on final attempt: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to delete file after multiple attempts: {FilePath}", filePath);
            throw;
        }
    }

    private bool IsFileReady(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private void PersistState()
    {
        try
        {
            var serializedQueue = JsonConvert.SerializeObject(_fileQueue);
            _fileStorage.Write(_stateFilePath, serializedQueue);
            _logger.Information("File queue state persisted.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to persist file queue state.");
        }
    }

    private async Task LoadStateAsync()
    {
        try
        {
            if (_fileStorage.Exists(_stateFilePath))
            {
                var serializedQueue = _fileStorage.Read(_stateFilePath);
                var deserializedQueue = JsonConvert.DeserializeObject<ConcurrentDictionary<string, DateTime>>(serializedQueue);
                if (deserializedQueue != null)
                {
                    foreach (var item in deserializedQueue)
                    {
                        if (_fileStorage.Exists(item.Key))
                        {
                            _fileQueue.TryAdd(item.Key, item.Value);
                        }
                        else
                        {
                            _logger.Warning("File from saved state no longer exists: {FullPath}", item.Key);
                        }
                    }
                }
                _logger.Information("File queue state loaded.");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load file queue state.");
        }

        await Task.CompletedTask;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            try
            {
                PersistState();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "An error occurred while persisting state during disposal.");
            }

            _persistenceTimer?.Dispose();

            foreach (var watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            _watchers.Clear();
            _logger.Information("All FileWatchers disposed.");

            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }

            if (_processingTask != null)
            {
                try
                {
                    _processingTask.Wait();
                }
                catch (AggregateException ae)
                {
                    ae.Handle(ex => ex is TaskCanceledException);
                }
                _processingTask = null;
            }
        }
        _disposed = true;
    }
}