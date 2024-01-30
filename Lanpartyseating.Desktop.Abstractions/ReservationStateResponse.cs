namespace Lanpartyseating.Desktop.Abstractions;

public class ReservationStateResponse : BaseMessage
{
    public bool IsSessionActive { get; set; }
    public DateTimeOffset ReservationStart { get; set; }
    public DateTimeOffset ReservationEnd { get; set; }
}
