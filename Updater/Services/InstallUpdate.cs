using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using NLog;
using PenumbraModForwarder.Common.Interfaces;
using Updater.Interfaces;

namespace Updater.Services;

public class InstallUpdate : IInstallUpdate
{
    private readonly IAppArguments _appArguments;
    private readonly IFileStorage _fileStorage;
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public InstallUpdate(IAppArguments appArguments, IFileStorage fileStorage)
    {
        _appArguments = appArguments;
        _fileStorage = fileStorage;
    }

    public async Task<bool> StartInstallationAsync(string downloadedPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_appArguments.InstallationPath))
            {
                _logger.Error("InstallationPath is empty. Please provide a valid path.");
                return false;
            }

            _logger.Debug($"Copying folder from {downloadedPath} to {_appArguments.InstallationPath}");

            if (!_fileStorage.Exists(_appArguments.InstallationPath))
            {
                _fileStorage.CreateDirectory(_appArguments.InstallationPath);
            }

            CopyDirectory(downloadedPath, _appArguments.InstallationPath);
            await Task.Delay(400);

            if (string.IsNullOrWhiteSpace(_appArguments.ProgramToRunAfterInstallation))
            {
                _logger.Info("No ProgramToRunAfterInstallation set; skipping execution.");
                return true;
            }

            var programPath = Path.Combine(_appArguments.InstallationPath, _appArguments.ProgramToRunAfterInstallation);
            if (_fileStorage.Exists(programPath))
            {
                _logger.Debug($"Starting program: {programPath}");
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = programPath,
                        UseShellExecute = true
                    },
                    EnableRaisingEvents = true
                };

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logger.Debug("[STDOUT]: {Data}", e.Data);
                    }
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logger.Error("[STDERR]: {Data}", e.Data);
                    }
                };
                process.Exited += (sender, e) =>
                {
                    _logger.Info("exited with code {ExitCode}", process.ExitCode);
                };

                process.Start();
                return true;
            }

            _logger.Error($"Program not found at: {programPath}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Installation failed");
            throw;
        }
    }

    private void CopyDirectory(string sourceDir, string destinationDir)
    {
        // Copy all files in the current directory
        foreach (var filePath in Directory.EnumerateFiles(sourceDir))
        {
            var fileName = Path.GetFileName(filePath);
            var destinationFilePath = Path.Combine(destinationDir, fileName);

            _fileStorage.CopyFile(filePath, destinationFilePath, true);
        }

        // Recursively copy each subdirectory
        foreach (var directoryPath in Directory.EnumerateDirectories(sourceDir))
        {
            var directoryName = Path.GetFileName(directoryPath);
            var destinationSubDir = Path.Combine(destinationDir, directoryName);

            if (!_fileStorage.Exists(destinationSubDir))
            {
                _fileStorage.CreateDirectory(destinationSubDir);
            }

            CopyDirectory(directoryPath, destinationSubDir);
        }
    }
}