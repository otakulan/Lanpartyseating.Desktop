using Lanpartyseating.Desktop.Contracts;
using Microsoft.Extensions.Logging;
using Message = Phoenix.Message;

namespace Lanpartyseating.Desktop.Business;

public class Callbacks
{
    private readonly ILogger<Callbacks> _logger;
    private readonly Utils _utils;
    private readonly ISessionManager _sessionManager;
    private readonly Timekeeper _timekeeper;

    public Callbacks(ILogger<Callbacks> logger,
        Utils utils,
        ISessionManager sessionManager,
        Timekeeper timekeeper)
    {
        _logger = logger;
        _utils = utils;
        _sessionManager = sessionManager;
        _timekeeper = timekeeper;
    }
    
    public void NewReservation(Message payload)
    {
        var newReservation = payload.Payload.Unbox<NewReservation>();
        
        _logger.LogInformation($"New reservation created for station #{newReservation.StationNumber}");
        if (!_utils.ForThisStation(newReservation.StationNumber, Environment.MachineName)) return;
        
        _timekeeper.StartSession(newReservation.Start, newReservation.End);
    }
    
    public void TournamentStart(Message payload)
    {
        var payloadObject = payload.Payload.Unbox<TournamentStart>();
        
        _logger.LogInformation($"Tournament started for station #{payloadObject.StationNumber}");
        if (!_utils.ForThisStation(payloadObject.StationNumber, Environment.MachineName)) return;
        
        _sessionManager.SignInTournamentAccount();
    }

    public void CancelReservation(Message payload)
    {
        var payloadObject = payload.Payload.Unbox<CancelReservation>();
        
        _logger.LogInformation($"Reservation cancelled for station #{payloadObject.StationNumber}");
        if (!_utils.ForThisStation(payloadObject.StationNumber, Environment.MachineName)) return;
        
        _timekeeper.EndSession();
    }

    public void ExtendReservation(Message payload)
    {
        var extendReservation = payload.Payload.Unbox<ExtendReservation>();

        _logger.LogInformation($"Reservation extended for station #{extendReservation.StationNumber}");
        if (!_utils.ForThisStation(extendReservation.StationNumber, Environment.MachineName)) return;

        _timekeeper.ExtendSession(extendReservation.End);
    }
}