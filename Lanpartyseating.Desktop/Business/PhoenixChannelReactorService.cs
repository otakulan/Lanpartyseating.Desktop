using Lanpartyseating.Desktop.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Phoenix;
using PhoenixTests.WebSocketImpl;
using ILogger = Phoenix.ILogger;

namespace Lanpartyseating.Desktop.Business;

public class PhoenixChannelReactorService
{
    private readonly IOptions<SeatingOptions> _options;
    private readonly ILogger<PhoenixChannelReactorService> _logger;
    private readonly Channel _desktopChannel;
    private readonly Socket _socket;
    public PhoenixChannelReactorService(IOptions<SeatingOptions> options,
        ILogger<PhoenixChannelReactorService> logger,
        Callbacks callbacks)
    {
        _options = options;
        _logger = logger;
        var socketOptions = new Socket.Options(new JsonMessageSerializer());
        var socketAddress = options.Value.WebsocketEndpoint;
        var socketFactory = new WebsocketSharpFactory();
        _socket = new Socket(socketAddress, null, socketFactory, socketOptions);
        
        _desktopChannel = _socket.Channel("desktop:all");
        _desktopChannel.On("new_reservation", callbacks.NewReservation);
        _desktopChannel.On("cancel_reservation", callbacks.CancelReservation);
        _desktopChannel.On("tournament_start", callbacks.TournamentStart);
        _desktopChannel.On("extend_reservation", callbacks.ExtendReservation);
    }
    
    public void Connect()
    {
        _logger.LogInformation($"Connecting to Phoenix channel at endpoint \"{_options.Value.WebsocketEndpoint}\"");
        _socket.Connect();
        _desktopChannel.Join();
    }
    
    public void Disconnect()
    {
        _desktopChannel.Leave();
        _socket.Disconnect();
    }
}