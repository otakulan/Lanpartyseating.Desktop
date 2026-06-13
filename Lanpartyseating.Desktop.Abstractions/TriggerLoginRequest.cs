namespace Lanpartyseating.Desktop.Abstractions;

public class TriggerLoginRequest : BaseMessage
{
    public required string Username { get; set; }
    public required string Password { get; set; }
    public string? Domain { get; set; }
}
