namespace Lanpartyseating.Desktop.Abstractions;

public class CredentialRequest : BaseMessage
{
    public int ProcessId { get; set; }
    public long Timestamp { get; set; }
}
