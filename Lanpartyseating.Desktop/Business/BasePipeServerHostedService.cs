using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Lanpartyseating.Desktop.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lanpartyseating.Desktop.Business;

public abstract class BasePipeServerHostedService : BackgroundService
{
    protected readonly ILogger Logger;
    private NamedPipeServerStream? _server;

    protected abstract string PipeName { get; }

    protected BasePipeServerHostedService(ILogger logger)
    {
        Logger = logger;
    }

    protected bool IsConnected => _server?.IsConnected ?? false;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.Register(() => Logger.LogInformation("{PipeName}: Service is stopping.", PipeName));

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    InitializePipeServer();
                    Logger.LogInformation("{PipeName}: Waiting for client connection...", PipeName);

                    var waitTask = _server!.WaitForConnectionAsync(stoppingToken);
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

                    if (await Task.WhenAny(waitTask, timeoutTask) == timeoutTask)
                    {
                        Logger.LogDebug("{PipeName}: Timeout while waiting for a client connection. Reconnecting in 3 seconds...", PipeName);
                        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                    }
                    else
                    {
                        Logger.LogInformation("{PipeName}: Client connected.", PipeName);
                        await ProcessClientConnectionAsync(stoppingToken);

                        Logger.LogInformation("{PipeName}: Client disconnected, preparing for next connection...", PipeName);
                        if (_server!.IsConnected)
                        {
                            _server.Disconnect();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Logger.LogInformation("{PipeName}: Operation canceled by stoppingToken.", PipeName);
                    break;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "{PipeName}: An error occurred while waiting for a client connection.", PipeName);

                    if (_server != null && _server.IsConnected)
                    {
                        _server.Disconnect();
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("{PipeName}: Service execution was canceled.", PipeName);
        }
        finally
        {
            if (_server != null && _server.IsConnected)
            {
                _server.Disconnect();
            }
            Logger.LogInformation("{PipeName}: Service is fully stopped.", PipeName);
        }
    }

    private void InitializePipeServer()
    {
        _server?.Dispose();

        try
        {
            var pipeSecurity = new PipeSecurity();

            var everyoneIdentity = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            var pipeAccessRule = new PipeAccessRule(everyoneIdentity,
                PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow);
            pipeSecurity.AddAccessRule(pipeAccessRule);

            var currentUser = WindowsIdentity.GetCurrent();
            if (currentUser.User != null)
            {
                var userAccessRule = new PipeAccessRule(currentUser.User,
                    PipeAccessRights.FullControl,
                    AccessControlType.Allow);
                pipeSecurity.AddAccessRule(userAccessRule);
            }

            _server = NamedPipeServerStreamAcl.Create(
                PipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize: 4096,
                outBufferSize: 4096,
                pipeSecurity);

            Logger.LogDebug("{PipeName}: Pipe server initialized successfully", PipeName);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "{PipeName}: Failed to initialize pipe server", PipeName);
            throw;
        }
    }

    private async Task ProcessClientConnectionAsync(CancellationToken stoppingToken)
    {
        if (_server == null || !_server.IsConnected)
        {
            Logger.LogWarning("{PipeName}: Server is not connected or has been disposed.", PipeName);
            return;
        }

        try
        {
            using var reader = new StreamReader(_server, leaveOpen: true);
            await using var writer = new StreamWriter(_server, leaveOpen: true);
            Task<string?>? currentReadTask = null;

            while (!stoppingToken.IsCancellationRequested && _server.IsConnected)
            {
                string? json = null;

                try
                {
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    if (_server.CanRead)
                    {
                        currentReadTask ??= reader.ReadLineAsync();

                        if (currentReadTask.IsCompleted)
                        {
                            json = await currentReadTask;
                            currentReadTask = null;
                        }
                        else
                        {
                            await Task.Delay(50, stoppingToken);
                            continue;
                        }
                    }
                }
                catch (IOException ex)
                {
                    Logger.LogError(ex, "{PipeName}: Pipe connection was lost or pipe is broken.", PipeName);
                    break;
                }

                if (json != null)
                {
                    BaseMessage? baseMessage = JsonMessageSerializer.Deserialize<BaseMessage>(json);

                    if (baseMessage == null)
                    {
                        Logger.LogWarning("{PipeName}: Failed to deserialize message: {Message}", PipeName, json);
                        continue;
                    }

                    if (stoppingToken.IsCancellationRequested)
                        break;

                    await HandleMessageAsync(baseMessage, writer, stoppingToken);

                    if (stoppingToken.IsCancellationRequested)
                        break;
                }

                await Task.Delay(100, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("{PipeName}: Operation canceled.", PipeName);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "{PipeName}: An unexpected error occurred.", PipeName);
        }
    }

    protected abstract Task HandleMessageAsync(BaseMessage message, StreamWriter writer, CancellationToken stoppingToken);

    public async Task SendMessageAsync<T>(T message, CancellationToken cancellationToken) where T : BaseMessage
    {
        if (_server is null)
        {
            Logger.LogWarning("{PipeName}: Server is not initialized.", PipeName);
            return;
        }

        if (!_server.IsConnected)
        {
            Logger.LogWarning("{PipeName}: No client is connected to send a message.", PipeName);
            return;
        }

        if (cancellationToken.IsCancellationRequested)
            return;

        try
        {
            var serializedMessage = JsonMessageSerializer.Serialize(message);
            await using var writer = new StreamWriter(_server, leaveOpen: true);
            await writer.WriteLineAsync(serializedMessage);
            await writer.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "{PipeName}: Failed to send message to client.", PipeName);
        }
    }
}
