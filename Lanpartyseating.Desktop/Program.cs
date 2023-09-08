using JetBrains.Annotations;
using Phoenix;
using PhoenixTests.WebSocketImpl;

namespace Lanpartyseating.Desktop;

[UsedImplicitly]
internal class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
        var socketOptions = new Socket.Options(new JsonMessageSerializer());
        var socketAddress = "ws://localhost:4000/desktop";
        var socketFactory = new WebsocketSharpFactory();
        var socket = new Socket(socketAddress, null, socketFactory, socketOptions);

        socket.Connect();
        
        var desktopChannel = socket.Channel(
            "desktop:lobby"
        );
        
        desktopChannel.On("new_reservation", payload =>
        {
            Console.WriteLine($"New reservation created for station #{payload.Payload.Unbox<NewReservation>().StationNumber}");
        });

        desktopChannel.Join();
            // .Receive(
            //     ReplyStatus.Ok,
            //     reply => okResponse = reply.Response.Unbox<JoinResponse>()
            // );
            // .Receive(
            //     ReplyStatus.Error,
            //     reply => errorResponse = reply.Response.Unbox<ChannelError>()
            // );
        
        Console.ReadLine();
    }
}
