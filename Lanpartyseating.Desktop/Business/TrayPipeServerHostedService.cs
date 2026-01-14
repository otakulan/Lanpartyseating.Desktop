using Lanpartyseating.Desktop.Abstractions;
using Microsoft.Extensions.Logging;

namespace Lanpartyseating.Desktop.Business;

public class TrayPipeServerHostedService : BasePipeServerHostedService, ITrayPipeService
{
    private readonly ReservationManager _reservationManager;
    private readonly ISessionManager _sessionManager;

    protected override string PipeName => "Lanpartyseating.Desktop.Tray";

    public TrayPipeServerHostedService(
        ILogger<TrayPipeServerHostedService> logger,
        ReservationManager reservationManager,
        ISessionManager sessionManager)
        : base(logger)
    {
        _reservationManager = reservationManager;
        _sessionManager = sessionManager;
    }

    protected override async Task HandleMessageAsync(BaseMessage message, StreamWriter writer, CancellationToken stoppingToken)
    {
        switch (message)
        {
            case ReservationStateRequest:
                var response = new ReservationStateResponse
                {
                    IsSessionActive = _reservationManager.IsReservationActive,
                    ReservationStart = _reservationManager.ReservationStart,
                    ReservationEnd = _reservationManager.ReservationEnd,
                };
                await writer.WriteLineAsync(JsonMessageSerializer.Serialize(response));
                await writer.FlushAsync(stoppingToken);
                break;

            case ClearAutoLogonRequest:
                _sessionManager.ClearAutoLogonCredentials();
                break;

            default:
                Logger.LogWarning("{PipeName}: Received an unknown message type: {Type}", PipeName, message.GetType().Name);
                break;
        }
    }
}
