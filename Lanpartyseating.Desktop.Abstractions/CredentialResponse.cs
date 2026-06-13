namespace Lanpartyseating.Desktop.Abstractions;

public class CredentialResponse : BaseMessage
{
    public required string Username { get; set; }
    public required string Password { get; set; }
    public string? Domain { get; set; }
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
}
