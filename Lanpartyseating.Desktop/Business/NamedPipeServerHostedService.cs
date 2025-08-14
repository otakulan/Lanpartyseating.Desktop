using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Lanpartyseating.Desktop.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lanpartyseating.Desktop.Business;

public class NamedPipeServerHostedService : BackgroundService, INamedPipeServerService
{
    private readonly ILogger<NamedPipeServerHostedService> _logger;
    private readonly ReservationManager _reservationManager;
    private readonly ISessionManager _sessionManager;
    private const string PipeName = "Lanpartyseating.Desktop";
    private NamedPipeServerStream? _server;
    
    // Store current credentials for credential provider requests
    private string? _currentUsername;
    private string? _currentPassword;
    private string? _currentDomain;

    public NamedPipeServerHostedService(ILogger<NamedPipeServerHostedService> logger, ReservationManager reservationManager, ISessionManager sessionManager)
    {
        _logger = logger;
        _reservationManager = reservationManager;
        _sessionManager = sessionManager;
        _server = null;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.Register(() => _logger.LogInformation("Service is stopping."));

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    InitializePipeServer();
                    _logger.LogInformation("Waiting for client connection...");

                    var waitTask = _server!.WaitForConnectionAsync(stoppingToken);
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); // Adjust the timeout as needed

                    if (await Task.WhenAny(waitTask, timeoutTask) == timeoutTask)
                    {
                        _logger.LogDebug("Timeout while waiting for a client connection. Reconnecting in 3 seconds...");
                        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                    }
                    else
                    {
                        _logger.LogInformation("Client connected.");

                        // Process the client connection - this will block until client disconnects
                        await ProcessClientConnectionAsync(stoppingToken);
                        
                        // After client disconnects, we need to disconnect and recreate the pipe
                        _logger.LogInformation("Client disconnected, preparing for next connection...");
                        if (_server!.IsConnected)
                        {
                            _server.Disconnect();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Operation canceled by stoppingToken.");
                    break; // Exit the while loop when operation is canceled
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while waiting for a client connection.");
                    
                    // Disconnect and wait before retrying
                    if (_server != null && _server.IsConnected)
                    {
                        _server.Disconnect();
                    }
                    
                    // Wait a bit before retrying to prevent tight error loops
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Service execution was canceled.");
        }
        finally
        {
            // Clean up the connection when service stops
            if (_server!.IsConnected)
            {
                _server.Disconnect();
            }
            _logger.LogInformation("Service is fully stopped.");
        }
    }
    
    private void InitializePipeServer()
    {
        // Dispose of the existing server if it's already been created
        _server?.Dispose();

        try
        {
            // Create pipe security that allows multiple processes to connect
            var pipeSecurity = new PipeSecurity();
            
            // Allow Everyone to read/write to the pipe
            var everyoneIdentity = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            var pipeAccessRule = new PipeAccessRule(everyoneIdentity, 
                PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance, 
                AccessControlType.Allow);
            pipeSecurity.AddAccessRule(pipeAccessRule);
            
            // Also explicitly allow the current user full control
            var currentUser = WindowsIdentity.GetCurrent();
            if (currentUser.User != null)
            {
                var userAccessRule = new PipeAccessRule(currentUser.User,
                    PipeAccessRights.FullControl,
                    AccessControlType.Allow);
                pipeSecurity.AddAccessRule(userAccessRule);
            }

            // Create the pipe with proper security using ACL method
            _server = NamedPipeServerStreamAcl.Create(
                PipeName,
                PipeDirection.InOut,
                254, // maxNumberOfServerInstances
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize: 4096,
                outBufferSize: 4096,
                pipeSecurity);
                
            _logger.LogDebug("Named pipe server initialized successfully with security permissions");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize named pipe server");
            throw;
        }
    }
    
    private async Task ProcessClientConnectionAsync(CancellationToken stoppingToken)
    {
        if (_server == null || !_server.IsConnected)
        {
            _logger.LogWarning("Server is not connected or has been disposed.");
            return; // Exit early if the server is not ready
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
                    // Check for cancellation before potentially blocking I/O operation
                    if (stoppingToken.IsCancellationRequested)
                        break;
                        
                    if (_server.CanRead)
                    {
                        // Only start a new read if we don't have one running
                        if (currentReadTask == null)
                        {
                            currentReadTask = reader.ReadLineAsync();
                        }
                        
                        // Check if the current read task has completed
                        if (currentReadTask.IsCompleted)
                        {
                            json = await currentReadTask;
                            currentReadTask = null; // Reset for next iteration
                        }
                        else
                        {
                            // Read task is still running, just wait a bit and check for cancellation
                            await Task.Delay(50, stoppingToken);
                            continue;
                        }
                    }
                }
                catch (IOException ex)
                {
                    _logger.LogError(ex, "Pipe connection was lost or pipe is broken.");
                    break; // Exit the loop if the pipe is broken or the connection is lost
                }

                if (json != null)
                {
                    BaseMessage? baseMessage = JsonMessageSerializer.Deserialize<BaseMessage>(json);
                    
                    if (baseMessage == null)
                    {
                        _logger.LogWarning("Failed to deserialize message: {Message}", json);
                        continue;
                    }
                    
                    // Check for cancellation before processing messages
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
                    else if (baseMessage is CredentialProviderConnected credProviderConnected)
                    {
                        _logger.LogInformation("Credential provider connected from process {ProcessId} at {Timestamp}", 
                            credProviderConnected.ProcessId, credProviderConnected.Timestamp);
                    }
                    else if (baseMessage is CredentialRequest credRequest)
                    {
                        _logger.LogInformation("Received credential request from process {ProcessId}", credRequest.ProcessId);
                        
                        // Check for cancellation before processing credential request
                        if (stoppingToken.IsCancellationRequested)
                            break;
                        
                        // Send current credentials if available
                        if (!string.IsNullOrEmpty(_currentUsername) && !string.IsNullOrEmpty(_currentPassword))
                        {
                            var credentialResponse = new CredentialResponse
                            {
                                Username = _currentUsername,
                                Password = _currentPassword,
                                Domain = _currentDomain,
                                Success = true
                            };
                            var responseMessage = JsonMessageSerializer.Serialize(credentialResponse);
                            await writer.WriteLineAsync(responseMessage);
                            await writer.FlushAsync(stoppingToken);
                            _logger.LogInformation("Sent credentials to credential provider process {ProcessId}", credRequest.ProcessId);
                        }
                        else
                        {
                            var errorResponse = new CredentialResponse
                            {
                                Username = "",
                                Password = "",
                                Success = false,
                                ErrorMessage = "No credentials available"
                            };
                            var responseMessage = JsonMessageSerializer.Serialize(errorResponse);
                            await writer.WriteLineAsync(responseMessage);
                            await writer.FlushAsync(stoppingToken);
                            _logger.LogWarning("No credentials available for credential provider request from process {ProcessId}", credRequest.ProcessId);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Received an unknown message type.");
                    }

                    // Check for cancellation after processing the message
                    if (stoppingToken.IsCancellationRequested)
                        break;
                }

                // Introduce a short delay to prevent a tight loop when no data is available
                await Task.Delay(100, stoppingToken);
            }
            }
            finally
            {
                // Safely dispose of the writer, handling broken pipe scenarios
                if (writer != null)
                {
                    try
                    {
                        await writer.DisposeAsync();
                    }
                    catch (IOException ex) when (ex.Message.Contains("Pipe is broken"))
                    {
                        _logger.LogDebug("Pipe was broken during writer disposal, this is expected during shutdown.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error disposing StreamWriter.");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation canceled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred.");
        }
    }

    public async Task SendMessageAsync<T>(T message, CancellationToken cancellationToken) where T : BaseMessage
    {
        if (_server is null)
        {
            throw new InvalidOperationException("The server is not connected.");
        }

        if (!_server.IsConnected)
        {
            _logger.LogWarning("No client is connected to send a message.");
            return;
        }

        // Check for cancellation before starting I/O operations
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
            _logger.LogError(ex, "Failed to send message to client.");
        }
    }

    public void StoreCredentials(string username, string password, string domain)
    {
        _currentUsername = username;
        _currentPassword = password;
        _currentDomain = domain;
        _logger.LogInformation("Stored credentials for user: {Username} (domain: {Domain}) - Password length: {PasswordLength}", 
            username, string.IsNullOrEmpty(domain) ? "local" : domain, password?.Length ?? 0);
    }

    public async Task TriggerLoginAsync()
    {
        _logger.LogInformation("Triggering credential provider login");
        
        var triggerLoginRequest = new TriggerLoginRequest();
        await SendMessageAsync(triggerLoginRequest, CancellationToken.None);
        
        _logger.LogInformation("Trigger login message sent to credential provider");
    }
}
