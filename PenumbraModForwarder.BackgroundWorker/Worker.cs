using PenumbraModForwarder.BackgroundWorker.Interfaces;
using Serilog;
using ILogger = Serilog.ILogger;

namespace PenumbraModForwarder.BackgroundWorker;

public class Worker : BackgroundService
{
    private readonly ILogger _logger;
    private readonly IWebSocketServer _webSocketServer;
    private readonly IStartupService _startupService;
    private readonly int _port;
    private readonly IHostApplicationLifetime _lifetime;
    private bool _initialized;

    public Worker(
        IWebSocketServer webSocketServer,
        IStartupService startupService,
        int port,
        IHostApplicationLifetime lifetime)
    {
        _logger = Log.ForContext<Worker>();
        _webSocketServer = webSocketServer;
        _startupService = startupService;
        _port = port;
        _lifetime = lifetime;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.Information("Starting WebSocket Server on port {Port}", _port);
        _webSocketServer.Start(_port);

        _logger.Information("Launching file watcher...");
        // Begin listening for a "shutdown" command in parallel
        _ = Task.Run(() => ListenForShutdownCommand(cancellationToken), cancellationToken);

        // Proceed with normal startup
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (!_initialized && _webSocketServer.HasConnectedClients())
                {
                    await _startupService.InitializeAsync();
                    _initialized = true;
                }

                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Information("Worker stopping gracefully...");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred in worker");
            throw;
        }
    }

    private async Task ListenForShutdownCommand(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Only proceed if there's input available
            if (Console.In.Peek() > -1)
            {
                var line = await Console.In.ReadLineAsync();
                if (line != null &&
                    line.Equals("shutdown", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Information("Received 'shutdown' command via standard input, stopping application...");
                    _lifetime.StopApplication();
                    break;
                }
            }

            // Wait briefly before checking again
            await Task.Delay(500, stoppingToken);
        }
    }
}