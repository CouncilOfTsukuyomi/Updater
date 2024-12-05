﻿using PenumbraModForwarder.FileMonitor.Models;

namespace PenumbraModForwarder.FileMonitor.Interfaces;

public interface IFileWatcher : IDisposable
{
    Task StartWatchingAsync(IEnumerable<string> paths, CancellationToken cancellationToken);
    event EventHandler<FileMovedEvent> FileMoved;
    Task PersistStateAsync();
    Task LoadStateAsync();
}