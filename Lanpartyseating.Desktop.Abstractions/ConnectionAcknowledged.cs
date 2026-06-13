namespace Lanpartyseating.Desktop.Abstractions;

public class ConnectionAcknowledged : BaseMessage
{
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
