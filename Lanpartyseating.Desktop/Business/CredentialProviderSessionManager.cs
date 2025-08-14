using Lanpartyseating.Desktop.Config;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Lanpartyseating.Desktop.Business;

public class CredentialProviderSessionManager : ISessionManager
{
    private readonly SeatingOptions _options;
    private readonly ICredentialProviderService _credentialProviderService;
    private readonly ILogger<CredentialProviderSessionManager> _logger;

    public CredentialProviderSessionManager(
        IOptions<SeatingOptions> options, 
        ICredentialProviderService credentialProviderService,
        ILogger<CredentialProviderSessionManager> logger)
    {
        _options = options.Value;
        _credentialProviderService = credentialProviderService;
        _logger = logger;
    }

    public async Task SignInGamerAccountAsync()
    {
        _logger.LogInformation("Storing gamer account credentials for credential provider");
        try
        {
            _credentialProviderService.StoreCredentials(
                _options.GamerAccountUsername, 
                _options.GamerAccountPassword);
            
            // Trigger the credential provider to attempt login
            await _credentialProviderService.TriggerLoginAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store gamer account credentials or trigger login");
        }
    }

    public async Task SignInTournamentAccountAsync()
    {
        _logger.LogInformation("Storing tournament account credentials for credential provider");
        try
        {
            _credentialProviderService.StoreCredentials(
                _options.TournamentAccountUsername, 
                _options.TournamentAccountPassword);
            
            // Trigger the credential provider to attempt login
            await _credentialProviderService.TriggerLoginAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store tournament account credentials or trigger login");
        }
    }

    public void SignOut()
    {
        _logger.LogInformation("Signing out current session");
        LogoffInteractiveSession();
    }

    public void ClearAutoLogonCredentials()
    {
        _logger.LogInformation("Clearing stored credentials for credential provider");
        try
        {
            _credentialProviderService.StoreCredentials("", "", "");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear stored credentials");
        }
    }

    private void LogoffInteractiveSession()
    {
        // Keep the existing logoff functionality from WindowsSessionManager
        var sessionId = WTSGetActiveConsoleSessionId();
        WTSLogoffSession(IntPtr.Zero, sessionId, false);
    }

    [System.Runtime.InteropServices.DllImport("wtsapi32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool WTSLogoffSession(IntPtr hServer, int sessionId, bool bWait);

    [System.Runtime.InteropServices.DllImport("Kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.U4)]
    private static extern int WTSGetActiveConsoleSessionId();
}
