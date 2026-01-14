using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Lanpartyseating.Desktop.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lanpartyseating.Desktop.Business;

public class TrayPipeServerHostedService : BackgroundService, ITrayPipeService
{
    private readonly ILogger<TrayPipeServerHostedService> _logger;
    private readonly ReservationManager _reservationManager;
    private readonly ISessionManager _sessionManager;
    private const string PipeName = "Lanpartyseating.Desktop.Tray";
    private NamedPipeServerStream? _server;

    public TrayPipeServerHostedService(
        ILogger<TrayPipeServerHostedService> logger,
        ReservationManager reservationManager,
        ISessionManager sessionManager)
    {
        _logger = logger;
        _reservationManager = reservationManager;
        _sessionManager = sessionManager;
        _server = null;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.Register(() => _logger.LogInformation("Tray pipe service is stopping."));

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    InitializePipeServer();
                    _logger.LogInformation("Tray pipe: Waiting for client connection...");

                    var waitTask = _server!.WaitForConnectionAsync(stoppingToken);
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

                    if (await Task.WhenAny(waitTask, timeoutTask) == timeoutTask)
                    {
                        _logger.LogDebug("Tray pipe: Timeout while waiting for a client connection. Reconnecting in 3 seconds...");
                        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                    }
                    else
                    {
                        _logger.LogInformation("Tray pipe: Client connected.");
                        await ProcessClientConnectionAsync(stoppingToken);

                        _logger.LogInformation("Tray pipe: Client disconnected, preparing for next connection...");
                        if (_server!.IsConnected)
                        {
                            _server.Disconnect();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Tray pipe: Operation canceled by stoppingToken.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tray pipe: An error occurred while waiting for a client connection.");

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
            _logger.LogInformation("Tray pipe: Service execution was canceled.");
        }
        finally
        {
            if (_server != null && _server.IsConnected)
            {
                _server.Disconnect();
            }
            _logger.LogInformation("Tray pipe: Service is fully stopped.");
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

            _logger.LogDebug("Tray pipe server initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize tray pipe server");
            throw;
        }
    }

    private async Task ProcessClientConnectionAsync(CancellationToken stoppingToken)
    {
        if (_server == null || !_server.IsConnected)
        {
            _logger.LogWarning("Tray pipe: Server is not connected or has been disposed.");
            return;
        }

        try
        {
            using var reader = new StreamReader(_server, leaveOpen: true);
            StreamWriter? writer = null;

            try
            {
                writer = new StreamWriter(_server, leaveOpen: true);
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
                            if (currentReadTask == null)
                            {
                                currentReadTask = reader.ReadLineAsync();
                            }

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
                        _logger.LogError(ex, "Tray pipe: Pipe connection was lost or pipe is broken.");
                        break;
                    }

                    if (json != null)
                    {
                        BaseMessage? baseMessage = JsonMessageSerializer.Deserialize<BaseMessage>(json);

                        if (baseMessage == null)
                        {
                            _logger.LogWarning("Tray pipe: Failed to deserialize message: {Message}", json);
                            continue;
                        }

                        if (stoppingToken.IsCancellationRequested)
                            break;

                        if (baseMessage is ReservationStateRequest)
                        {
                            var response = new ReservationStateResponse
                            {
                                IsSessionActive = _reservationManager.IsReservationActive,
                                ReservationStart = _reservationManager.ReservationStart,
                                ReservationEnd = _reservationManager.ReservationEnd,
                            };
                            await writer.WriteLineAsync(JsonMessageSerializer.Serialize(response));
                            await writer.FlushAsync(stoppingToken);
                        }
                        else if (baseMessage is ClearAutoLogonRequest)
                        {
                            _sessionManager.ClearAutoLogonCredentials();
                        }
                        else
                        {
                            _logger.LogWarning("Tray pipe: Received an unknown message type: {Type}", baseMessage.GetType().Name);
                        }

                        if (stoppingToken.IsCancellationRequested)
                            break;
                    }

                    await Task.Delay(100, stoppingToken);
                }
            }
            finally
            {
                if (writer != null)
                {
                    try
                    {
                        await writer.DisposeAsync();
                    }
                    catch (IOException ex) when (ex.Message.Contains("Pipe is broken"))
                    {
                        _logger.LogDebug("Tray pipe: Pipe was broken during writer disposal, this is expected during shutdown.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Tray pipe: Error disposing StreamWriter.");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Tray pipe: Operation canceled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tray pipe: An unexpected error occurred.");
        }
    }

    public async Task SendMessageAsync<T>(T message, CancellationToken cancellationToken) where T : BaseMessage
    {
        if (_server is null)
        {
            _logger.LogWarning("Tray pipe: Server is not initialized.");
            return;
        }

        if (!_server.IsConnected)
        {
            _logger.LogWarning("Tray pipe: No client is connected to send a message.");
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
            _logger.LogError(ex, "Tray pipe: Failed to send message to client.");
        }
    }
}
