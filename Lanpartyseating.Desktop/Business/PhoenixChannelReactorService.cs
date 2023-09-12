using Microsoft.Extensions.Options;
using Phoenix;
using PhoenixTests.WebSocketImpl;

namespace Lanpartyseating.Desktop.Business;

public class PhoenixChannelReactorService
{
    private readonly Channel _desktopChannel;
    private readonly Socket _socket;
    public PhoenixChannelReactorService(IOptions<SeatingOptions> options, Callbacks callbacks)
    {
        var socketOptions = new Socket.Options(new JsonMessageSerializer());
        var socketAddress = options.Value.WebsocketEndpoint;
        var socketFactory = new WebsocketSharpFactory();
        _socket = new Socket(socketAddress, null, socketFactory, socketOptions);
        
        _desktopChannel = _socket.Channel("desktop:all");
        _desktopChannel.On("new_reservation", callbacks.NewReservation);
        _desktopChannel.On("cancel_reservation", callbacks.CancelReservation);
        _desktopChannel.On("tournament_start", callbacks.TournamentStart);
    }
    
    public void Connect()
    {
        _socket.Connect();
        _desktopChannel.Join();
    }
    
    public void Disconnect()
    {
        _desktopChannel.Leave();
        _socket.Disconnect();
    }
}