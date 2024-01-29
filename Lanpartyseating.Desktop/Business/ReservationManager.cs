namespace Lanpartyseating.Desktop.Business;

public class ReservationManager
{
    public DateTimeOffset ReservationStart { get; private set; }
    public DateTimeOffset ReservationEnd { get; private set; }
    public bool IsReservationActive { get; private set; }
    
    public void StartReservation(DateTimeOffset reservationStart, DateTimeOffset reservationEnd)
    {
        ReservationStart = reservationStart;
        ReservationEnd = reservationEnd;
        IsReservationActive = true;
    }
    
    public void EndReservation()
    {
        ReservationStart = DateTimeOffset.MinValue;
        ReservationEnd = DateTimeOffset.MinValue;
        IsReservationActive = false;
    }
    
    public void ExtendReservation(DateTimeOffset reservationEnd)
    {
        ReservationEnd = reservationEnd;
    }
}