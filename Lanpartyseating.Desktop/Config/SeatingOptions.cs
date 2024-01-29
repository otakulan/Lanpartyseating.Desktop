using System.ComponentModel.DataAnnotations;

namespace Lanpartyseating.Desktop.Config;

public class SeatingOptions
{
    [Required]
    public required string WebsocketEndpoint { get; set; }
    [Required]
    public required string GamerAccountUsername { get; set; }
    [Required(AllowEmptyStrings = true)]
    public required string GamerAccountPassword { get; set; }
    [Required]
    public required string TournamentAccountUsername { get; set; }
    [Required(AllowEmptyStrings = true)]
    public required string TournamentAccountPassword { get; set; }
}