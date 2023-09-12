using Newtonsoft.Json;

namespace Lanpartyseating.Desktop.Contracts;

public class CancelReservation
{
    [JsonProperty("station_number")]
    public int StationNumber { get; init; }
}