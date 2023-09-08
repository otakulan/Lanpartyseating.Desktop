using Newtonsoft.Json;

namespace Lanpartyseating.Desktop;

public class NewReservation
{
    [JsonProperty("station_number")]
    public int StationNumber { get; init; }
}