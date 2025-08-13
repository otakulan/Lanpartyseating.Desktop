namespace Lanpartyseating.Desktop.Abstractions;

public class CredentialProviderConnected : BaseMessage
{
    public int ProcessId { get; set; }
    public long Timestamp { get; set; }
}
