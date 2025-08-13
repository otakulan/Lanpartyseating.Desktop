namespace Lanpartyseating.Desktop.Config;

public class DebugOptions
{
    public bool ReactToAllStations { get; set; }
    public bool UseDummySessionManager { get; set; }
    public bool UseCredentialProvider { get; set; } = true; // Default to new approach
}