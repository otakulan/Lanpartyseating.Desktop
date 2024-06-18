using System.ComponentModel;
using Lanpartyseating.Desktop.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Lanpartyseating.Desktop.Business;
using Microsoft.Win32.SafeHandles;

namespace Lanpartyseating.Desktop;

public class NamedPipeServerHostedService : BackgroundService, INamedPipeServerService
{
    private readonly ILogger<NamedPipeServerHostedService> _logger;
    private readonly ReservationManager _reservationManager;
    private readonly ISessionManager _sessionManager;
    private const string PipeName = "Lanpartyseating.Desktop";
    private NamedPipeServerStream? _server;

    public NamedPipeServerHostedService(ILogger<NamedPipeServerHostedService> logger, ReservationManager reservationManager, ISessionManager sessionManager)
    {
        _logger = logger;
        _reservationManager = reservationManager;
        _sessionManager = sessionManager;
        _server = null;
    }
    
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern SafePipeHandle CreateNamedPipe(string lpName, uint dwOpenMode, uint dwPipeMode, uint nMaxInstances, uint nOutBufferSize, uint nInBufferSize, uint nDefaultTimeOut, SECURITY_ATTRIBUTES lpSecurityAttributes);

    [StructLayout(LayoutKind.Sequential)]
    public class SECURITY_ATTRIBUTES
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        public int bInheritHandle;
    }
    
    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(
        string StringSecurityDescriptor,
        uint StringSDRevision,
        out IntPtr SecurityDescriptor,
        out uint SecurityDescriptorSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr LocalFree(IntPtr hMem);
    
    // Pipe open modes
    const uint PIPE_ACCESS_DUPLEX = 0x00000003;
    const uint PIPE_ACCESS_INBOUND = 0x00000001;
    const uint PIPE_ACCESS_OUTBOUND = 0x00000002;

// Pipe modes
    const uint PIPE_TYPE_BYTE = 0x00000000;
    const uint PIPE_TYPE_MESSAGE = 0x00000004;
    const uint PIPE_READMODE_BYTE = 0x00000000;
    const uint PIPE_READMODE_MESSAGE = 0x00000002;
    const uint PIPE_WAIT = 0x00000000;
    const uint PIPE_NOWAIT = 0x00000001;

// Additional flags
    const uint FILE_FLAG_OVERLAPPED = 0x40000000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.Register(() => _logger.LogInformation("Service is stopping."));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                InitializePipeServer();
                _logger.LogInformation("Waiting for client connection...");

                var waitTask = _server.WaitForConnectionAsync(stoppingToken);
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); // Adjust the timeout as needed

                if (await Task.WhenAny(waitTask, timeoutTask) == timeoutTask)
                {
                    _logger.LogDebug("Timeout while waiting for a client connection. Reconnecting in 3 seconds...");
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                }
                else
                {
                    _logger.LogInformation("Client connected.");

                    await ProcessClientConnectionAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Operation canceled by stoppingToken.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while waiting for a client connection.");
            }
            finally
            {
                if (_server.IsConnected)
                {
                    _server.Disconnect(); // Ensure the server is disconnected after handling a connection
                }
            }
        }
    }
    
    private void InitializePipeServer()
    {
        // Dispose of the existing server if it's already been created
        _server?.Dispose();

        var pipeSecurity = new PipeSecurity();
        var authenticatedUsers = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
        var pipeAccessRule = new PipeAccessRule(authenticatedUsers, PipeAccessRights.ReadWrite, AccessControlType.Allow);
        pipeSecurity.AddAccessRule(pipeAccessRule);

        // Convert the PipeSecurity object to a SECURITY_ATTRIBUTES structure
        IntPtr sd = ConvertPipeSecurityToSecurityDescriptor(pipeSecurity);
        var sa = new SECURITY_ATTRIBUTES
        {
            nLength = Marshal.SizeOf(typeof(SECURITY_ATTRIBUTES)),
            lpSecurityDescriptor = sd,
            bInheritHandle = 1 // True
        };

        // Create the named pipe
        var pipeHandle = CreateNamedPipe(
            @"\\.\pipe\" + PipeName,
            PIPE_ACCESS_DUPLEX | FILE_FLAG_OVERLAPPED,
            PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT,
            1, // Max instances
            4096, // Out buffer size
            4096, // In buffer size
            0, // Default timeout
            sa);

        if (pipeHandle.IsInvalid)
        {
            // Access denied errors have been observed here intermittently.
            // Throwing here will cause the server to re-initialize in the run loop above.
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        // Create the NamedPipeServerStream instance
        _server = new NamedPipeServerStream(PipeDirection.InOut, false, true, pipeHandle);

        // Free the security descriptor memory
        if (sd != IntPtr.Zero)
        {
            LocalFree(sd);
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
            await using var writer = new StreamWriter(_server, leaveOpen: true);

            while (!stoppingToken.IsCancellationRequested && _server.IsConnected)
            {
                string json = null;

                try
                {
                    if (_server.CanRead)
                    {
                        json = await reader.ReadLineAsync();
                    }
                }
                catch (IOException ex)
                {
                    _logger.LogError(ex, "Pipe connection was lost or pipe is broken.");
                    break; // Exit the loop if the pipe is broken or the connection is lost
                }

                if (json != null)
                {
                    var baseMessage = JsonMessageSerializer.Deserialize<BaseMessage>(json);
                    if (baseMessage is ReservationStateRequest)
                    {
                        var response = new ReservationStateResponse
                        {
                            IsSessionActive = _reservationManager.IsReservationActive,
                            ReservationStart = _reservationManager.ReservationStart,
                            ReservationEnd = _reservationManager.ReservationEnd,
                        };
                        await writer.WriteLineAsync(JsonMessageSerializer.Serialize(response));
                        await writer.FlushAsync();
                    }
                    else if (baseMessage is ClearAutoLogonRequest)
                    {
                        _sessionManager.ClearAutoLogonCredentials();
                    }
                    else
                    {
                        _logger.LogWarning("Received an unknown message type.");
                    }

                    // Check for cancellation again after processing the message
                    stoppingToken.ThrowIfCancellationRequested();
                }

                // Introduce a short delay to prevent a tight loop when no data is available
                await Task.Delay(100, stoppingToken);
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

        try
        {
            await using var writer = new StreamWriter(_server, leaveOpen: true);
            await writer.WriteLineAsync(JsonMessageSerializer.Serialize(message));
            await writer.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to client.");
        }
    }
    
    public static IntPtr ConvertPipeSecurityToSecurityDescriptor(PipeSecurity pipeSecurity)
    {
        string stringSecurityDescriptor = pipeSecurity.GetSecurityDescriptorSddlForm(AccessControlSections.Access);

        IntPtr securityDescriptor = IntPtr.Zero;
        uint securityDescriptorSize = 0;
        if (!ConvertStringSecurityDescriptorToSecurityDescriptor(stringSecurityDescriptor, 1, out securityDescriptor, out securityDescriptorSize))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return securityDescriptor;
    }
}
