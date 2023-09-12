using Newtonsoft.Json;

namespace Lanpartyseating.Desktop.Contracts;

public class TournamentStart
{
    [JsonProperty("station_number")]
    public int StationNumber { get; init; }
}