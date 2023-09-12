using Newtonsoft.Json;

namespace Lanpartyseating.Desktop.Contracts;

public class NewReservation
{
    [JsonProperty("station_number")]
    public int StationNumber { get; init; }
}