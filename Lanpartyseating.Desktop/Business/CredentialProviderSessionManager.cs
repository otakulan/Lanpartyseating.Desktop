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

    public void SignInGamerAccount()
    {
        _logger.LogInformation("Signing in gamer account via credential provider");
        _ = Task.Run(async () => 
        {
            try
            {
                await _credentialProviderService.TriggerLoginAsync(
                    _options.GamerAccountUsername, 
                    _options.GamerAccountPassword);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to trigger gamer account login");
            }
        });
    }

    public void SignInTournamentAccount()
    {
        _logger.LogInformation("Signing in tournament account via credential provider");
        _ = Task.Run(async () => 
        {
            try
            {
                await _credentialProviderService.TriggerLoginAsync(
                    _options.TournamentAccountUsername, 
                    _options.TournamentAccountPassword);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to trigger tournament account login");
            }
        });
    }

    public void SignOut()
    {
        _logger.LogInformation("Signing out current session");
        LogoffInteractiveSession();
    }

    public void ClearAutoLogonCredentials()
    {
        _logger.LogInformation("Auto-logon credentials cleared (no-op for credential provider)");
        // No need to clear registry with credential provider approach
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
