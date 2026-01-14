using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Lanpartyseating.Desktop.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lanpartyseating.Desktop.Business;

public class CredentialProviderPipeServerHostedService : BackgroundService, ICredentialProviderPipeService
{
    private readonly ILogger<CredentialProviderPipeServerHostedService> _logger;
    private const string PipeName = "Lanpartyseating.Desktop";
    private NamedPipeServerStream? _server;

    public CredentialProviderPipeServerHostedService(ILogger<CredentialProviderPipeServerHostedService> logger)
    {
        _logger = logger;
        _server = null;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.Register(() => _logger.LogInformation("Credential provider pipe service is stopping."));

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    InitializePipeServer();
                    _logger.LogInformation("Credential provider pipe: Waiting for client connection...");

                    var waitTask = _server!.WaitForConnectionAsync(stoppingToken);
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

                    if (await Task.WhenAny(waitTask, timeoutTask) == timeoutTask)
                    {
                        _logger.LogDebug("Credential provider pipe: Timeout while waiting for a client connection. Reconnecting in 3 seconds...");
                        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                    }
                    else
                    {
                        _logger.LogInformation("Credential provider pipe: Client connected.");
                        await ProcessClientConnectionAsync(stoppingToken);

                        _logger.LogInformation("Credential provider pipe: Client disconnected, preparing for next connection...");
                        if (_server!.IsConnected)
                        {
                            _server.Disconnect();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Credential provider pipe: Operation canceled by stoppingToken.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Credential provider pipe: An error occurred while waiting for a client connection.");

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
            _logger.LogInformation("Credential provider pipe: Service execution was canceled.");
        }
        finally
        {
            if (_server != null && _server.IsConnected)
            {
                _server.Disconnect();
            }
            _logger.LogInformation("Credential provider pipe: Service is fully stopped.");
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

            _logger.LogDebug("Credential provider pipe server initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize credential provider pipe server");
            throw;
        }
    }

    private async Task ProcessClientConnectionAsync(CancellationToken stoppingToken)
    {
        if (_server == null || !_server.IsConnected)
        {
            _logger.LogWarning("Credential provider pipe: Server is not connected or has been disposed.");
            return;
        }

        try
        {
            using var reader = new StreamReader(_server, leaveOpen: true);
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
                    _logger.LogError(ex, "Credential provider pipe: Pipe connection was lost or pipe is broken.");
                    break;
                }

                if (json != null)
                {
                    BaseMessage? baseMessage = JsonMessageSerializer.Deserialize<BaseMessage>(json);

                    if (baseMessage == null)
                    {
                        _logger.LogWarning("Credential provider pipe: Failed to deserialize message: {Message}", json);
                        continue;
                    }

                    if (stoppingToken.IsCancellationRequested)
                        break;

                    if (baseMessage is CredentialProviderConnected credProviderConnected)
                    {
                        _logger.LogInformation("Credential provider connected from process {ProcessId} at {Timestamp}",
                            credProviderConnected.ProcessId, credProviderConnected.Timestamp);
                    }
                    else
                    {
                        _logger.LogWarning("Credential provider pipe: Received an unknown message type: {Type}", baseMessage.GetType().Name);
                    }

                    if (stoppingToken.IsCancellationRequested)
                        break;
                }

                await Task.Delay(100, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Credential provider pipe: Operation canceled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Credential provider pipe: An unexpected error occurred.");
        }
    }

    public async Task TriggerLoginAsync(string username, string password, string? domain = null)
    {
        _logger.LogInformation("Triggering credential provider login for user: {Username} (domain: {Domain}) - Password length: {PasswordLength}",
            username, string.IsNullOrEmpty(domain) ? "local" : domain, password?.Length ?? 0);

        if (_server is null)
        {
            _logger.LogWarning("Credential provider pipe: Server is not initialized.");
            return;
        }

        if (!_server.IsConnected)
        {
            _logger.LogWarning("Credential provider pipe: No client is connected to send a message.");
            return;
        }

        try
        {
            var triggerLoginRequest = new TriggerLoginRequest
            {
                Username = username,
                Password = password ?? "",
                Domain = domain
            };

            var serializedMessage = JsonMessageSerializer.Serialize(triggerLoginRequest);
            await using var writer = new StreamWriter(_server, leaveOpen: true);
            await writer.WriteLineAsync(serializedMessage);
            await writer.FlushAsync();

            _logger.LogInformation("Trigger login message with credentials sent to credential provider");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Credential provider pipe: Failed to send message to client.");
        }
    }
}
