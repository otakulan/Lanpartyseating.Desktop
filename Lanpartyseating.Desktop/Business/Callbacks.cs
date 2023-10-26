using System.Diagnostics;
using System.Runtime.InteropServices;
using Lanpartyseating.Desktop.Config;
using Lanpartyseating.Desktop.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Win32;
using Message = Phoenix.Message;

namespace Lanpartyseating.Desktop.Business;

public class Callbacks
{
    private readonly ILogger<Callbacks> _logger;
    private readonly Utils _utils;
    private readonly SeatingOptions _options;

    public Callbacks(ILogger<Callbacks> logger, IOptions<SeatingOptions> options, Utils utils)
    {
        _logger = logger;
        _utils = utils;
        _options = options.Value;
    }
    
    public void NewReservation(Message payload)
    {
        var payloadObject = payload.Payload.Unbox<NewReservation>();
        
        _logger.LogInformation($"New reservation created for station #{payloadObject.StationNumber}");
        if (!_utils.ForThisStation(payloadObject.StationNumber, Environment.MachineName)) return;
        
        _utils.LoginInteractiveSession(_options.GamerAccountUsername, _options.GamerAccountPassword);
    }
    
    public void TournamentStart(Message payload)
    {
        var payloadObject = payload.Payload.Unbox<TournamentStart>();
        
        _logger.LogInformation($"Tournament started for station #{payloadObject.StationNumber}");
        if (!_utils.ForThisStation(payloadObject.StationNumber, Environment.MachineName)) return;
        
        _utils.LoginInteractiveSession(_options.TournamentAccountUsername, _options.TournamentAccountPassword);
    }

    public void CancelReservation(Message payload)
    {
        var payloadObject = payload.Payload.Unbox<CancelReservation>();
        
        _logger.LogInformation($"Reservation cancelled for station #{payloadObject.StationNumber}");
        if (!_utils.ForThisStation(payloadObject.StationNumber, Environment.MachineName)) return;
        
        _utils.LogoffInteractiveSession();
    }
}