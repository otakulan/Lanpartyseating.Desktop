using Newtonsoft.Json;

namespace Lanpartyseating.Desktop.Contracts;

public class ExtendReservation
{
    [JsonProperty("station_number")]
    public int StationNumber { get; init; }
    [JsonProperty("start_date")]
    public DateTimeOffset Start { get; init; }
    [JsonProperty("end_date")]
    public DateTimeOffset End { get; init; }
}