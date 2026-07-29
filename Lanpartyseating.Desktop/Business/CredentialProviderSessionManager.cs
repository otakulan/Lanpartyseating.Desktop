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
        _logger.LogInformation("Triggering gamer account login via credential provider");
        try
        {
            // Send credentials directly with trigger - no separate storage needed
            await _credentialProviderService.TriggerLoginAsync(
                _options.GamerAccountUsername, 
                _options.GamerAccountPassword,
                _options.WindowsDomain);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger gamer account login");
        }
    }

    public async Task SignInTournamentAccountAsync()
    {
        _logger.LogInformation("Triggering tournament account login via credential provider");
        try
        {
            // Send credentials directly with trigger - no separate storage needed
            await _credentialProviderService.TriggerLoginAsync(
                _options.TournamentAccountUsername, 
                _options.TournamentAccountPassword,
                _options.WindowsDomain);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger tournament account login");
        }
    }

    public void SignOut()
    {
        _logger.LogInformation("Signing out current session");
        LogoffInteractiveSession();
    }

    public void ClearAutoLogonCredentials()
    {
        _logger.LogInformation("Clear auto logon credentials called - no stored credentials to clear in new approach");
        // No action needed since we don't store credentials anymore
        // The credential provider will simply not receive any trigger messages
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
