using System.IO.Pipes;
using Lanpartyseating.Desktop.Business;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lanpartyseating.Desktop;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly PhoenixChannelReactorService _reactor;

    public Worker(ILogger<Worker> logger, PhoenixChannelReactorService reactor)
    {
        _logger = logger;
        _reactor = reactor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _reactor.Connect();
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            _reactor.Disconnect();
        }
        catch (TaskCanceledException)
        {
            // When the stopping token is canceled, for example, a call made from services.msc,
            // we shouldn't exit with a non-zero exit code. In other words, this is expected...
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Message}", ex.Message);

            // Terminates this process and returns an exit code to the operating system.
            // This is required to avoid the 'BackgroundServiceExceptionBehavior', which
            // performs one of two scenarios:
            // 1. When set to "Ignore": will do nothing at all, errors cause zombie services.
            // 2. When set to "StopHost": will cleanly stop the host, and log errors.
            //
            // In order for the Windows Service Management system to leverage configured
            // recovery options, we need to terminate the process with a non-zero exit code.
            Environment.Exit(1);
        }
    }
}