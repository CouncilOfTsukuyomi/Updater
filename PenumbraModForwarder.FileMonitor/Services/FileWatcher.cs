﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PenumbraModForwarder.Common.Consts;
using PenumbraModForwarder.Common.Interfaces;
using PenumbraModForwarder.FileMonitor.Interfaces;
using PenumbraModForwarder.FileMonitor.Models;
using Serilog;

namespace PenumbraModForwarder.FileMonitor.Services;
public class FileWatcher : IFileWatcher
{
    private readonly List<FileSystemWatcher> _watchers;
    private readonly ConcurrentDictionary<string, DateTime> _fileQueue;
    private readonly IFileStorage _fileStorage;
    private readonly string _destDirectory = ConfigurationConsts.ModsPath;
    private readonly string _stateFilePath = "fileQueueState.json";
    private bool _disposed;

    public FileWatcher(IFileStorage fileStorage)
    {
        _fileStorage = fileStorage;
        _fileQueue = new ConcurrentDictionary<string, DateTime>();
        _watchers = new List<FileSystemWatcher>();
    }

    public event EventHandler<FileMovedEvent> FileMoved;

    public async Task StartWatchingAsync(IEnumerable<string> paths, CancellationToken cancellationToken)
    {
        await LoadStateAsync();

        foreach (var path in paths)
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

            Log.Information("Started watching directory: {Path}", path);
        }

        await Task.Run(() => ProcessQueueAsync(cancellationToken), cancellationToken);
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        _fileQueue.TryAdd(e.FullPath, DateTime.UtcNow);
        Log.Information("File added to queue: {FullPath}", e.FullPath);
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            foreach (var file in _fileQueue)
            {
                if (IsFileReady(file.Key))
                {
                    ProcessFile(file.Key);
                }
                else if (DateTime.UtcNow - file.Value > TimeSpan.FromSeconds(5))
                {
                    Log.Information("File is not ready for processing, will retry: {FullPath}", file.Key);
                }
            }
            await Task.Delay(500, cancellationToken);
        }
    }

    private void ProcessFile(string filePath)
    {
        if (_fileStorage.Exists(filePath))
        {
            if (IsFileReady(filePath))
            {
                MoveFile(filePath);
            }
            else
            {
                Log.Information("File is not ready for processing: {FullPath}", filePath);
            }
        }
        else
        {
            _fileQueue.TryRemove(filePath, out _);
            Log.Warning("File no longer exists: {FullPath}", filePath);
        }
    }

    private void MoveFile(string filePath)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        var destinationFolder = Path.Combine(_destDirectory, fileNameWithoutExtension);

        _fileStorage.CreateDirectory(destinationFolder);

        var destinationPath = Path.Combine(destinationFolder, Path.GetFileName(filePath));
        File.Move(filePath, destinationPath);
        _fileQueue.TryRemove(filePath, out _);

        FileMoved?.Invoke(this, new FileMovedEvent(filePath, destinationPath, fileNameWithoutExtension));
        Log.Information("File moved: {SourcePath} to {DestinationPath}", filePath, destinationPath);
    }

    private bool IsFileReady(string filePath)
    {
        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public async Task PersistStateAsync()
    {
        try
        {
            var serializedQueue = JsonConvert.SerializeObject(_fileQueue);
            await File.WriteAllTextAsync(_stateFilePath, serializedQueue);
            Log.Information("File queue state persisted.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to persist file queue state.");
        }
    }

    public async Task LoadStateAsync()
    {
        try
        {
            if (File.Exists(_stateFilePath))
            {
                var serializedQueue = await File.ReadAllTextAsync(_stateFilePath);
                var deserializedQueue = JsonConvert.DeserializeObject<ConcurrentDictionary<string, DateTime>>(serializedQueue);

                if (deserializedQueue != null)
                {
                    foreach (var item in deserializedQueue)
                    {
                        _fileQueue.TryAdd(item.Key, item.Value);
                    }
                }
                Log.Information("File queue state loaded.");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load file queue state.");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            foreach (var watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            Log.Information("All FileWatchers disposed.");
        }

        _disposed = true;
    }
}
