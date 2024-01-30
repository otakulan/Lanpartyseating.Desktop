using System.IO.Pipes;
using Lanpartyseating.Desktop.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Uwp.Notifications;

namespace Lanpartyseating.Desktop.Tray;

public class ToastNotificationService : BackgroundService
{
    private readonly ILogger<ToastNotificationService> _logger;
    private readonly TrayIcon _trayIcon;
    private const string PipeName = "Lanpartyseating.Desktop";

    public ToastNotificationService(ILogger<ToastNotificationService> logger, TrayIcon trayIcon)
    {
        _logger = logger;
        _trayIcon = trayIcon;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.Register(() => _logger.LogInformation($"{PipeName} ToastNotificationService is stopping."));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
                _logger.LogInformation($"Connecting to {PipeName} server...");

                await client.ConnectAsync(stoppingToken);
                _logger.LogInformation("Connected to server.");

                // Send the ReservationStateRequest message once after connecting
                await SendInitialMessageAsync(client, stoppingToken);

                // After sending the initial message, keep the connection open and listen for messages from the server
                await ListenForServerMessagesAsync(client, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Connection attempt was canceled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to or communicating with the named pipe server.");
            }
            finally
            {
                _logger.LogInformation("Client disconnected from server.");
            }
        }
    }
    
    private void ShowInitialTaskDialog(string heading, string text)
    {
        var page = new TaskDialogPage
        {
            Caption = "Welcome to Otakuthon PC Gaming",
            Heading = heading,
            Text = text,
            Icon = TaskDialogIcon.Information,
            Buttons = new TaskDialogButtonCollection { TaskDialogButton.OK }
        };

        TaskDialog.ShowDialog(page);
    }

    private async Task SendInitialMessageAsync(NamedPipeClientStream client, CancellationToken stoppingToken)
    {
        await using var writer = new StreamWriter(client, leaveOpen: true);
        var request = new ReservationStateRequest();
        var jsonRequest = JsonMessageSerializer.Serialize(request);
        await writer.WriteLineAsync(jsonRequest);
        await writer.FlushAsync(stoppingToken);
    }

    private async Task ListenForServerMessagesAsync(NamedPipeClientStream client, CancellationToken stoppingToken)
    {
        using var reader = new StreamReader(client);
        while (!stoppingToken.IsCancellationRequested && client.IsConnected)
        {
            try
            {
                // Asynchronously wait for a message from the server
                var jsonResponse = await reader.ReadLineAsync(stoppingToken);

                // If the read operation is canceled or returns null, break the loop
                if (jsonResponse == null) break;

                var baseMessage = JsonMessageSerializer.Deserialize<BaseMessage>(jsonResponse);
                ProcessReceivedMessage(baseMessage);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "The pipe was broken or disconnected.");
                break; // Exit the loop if the pipe is broken or disconnected
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while listening for server messages.");
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }

    private void ProcessReceivedMessage(BaseMessage message)
    {
        if (message is TextMessage textMessage)
        {
            // TODO: Make the protocol better and update tray icon tooltip
            ShowToast(textMessage.Content);
        }
        else if (message is ReservationStateResponse reservationStateResponse)
        {
            var minutesUntilEnd = (reservationStateResponse.ReservationEnd - DateTimeOffset.UtcNow).TotalMinutes;
            var formattedLocalEndTime = reservationStateResponse.ReservationEnd.ToLocalTime().ToString("t");
            
            // Create a dedicated STA thread for the task dialog
            var staThread = new Thread(() =>
            {
                _trayIcon.UpdateText($"PC Gaming - You will be logged out at {formattedLocalEndTime}");
                ShowInitialTaskDialog($"Your session will end {minutesUntilEnd:0} minutes after badge scan.",
                    $"You will automatically be logged out at {formattedLocalEndTime}.");
            });

            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
        }
    }

    private static void ShowToast(string message)
    {
        new ToastContentBuilder()
            .AddText(message)
            .Show(); // Ensure this runs on the UI thread if necessary
    }
}