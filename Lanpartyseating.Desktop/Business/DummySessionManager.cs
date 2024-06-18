using Microsoft.Extensions.Logging;

namespace Lanpartyseating.Desktop.Business;

public class DummySessionManager : ISessionManager
{
    private readonly ILogger _logger;

    public DummySessionManager(ILogger<DummySessionManager> logger)
    {
        _logger = logger;
        _logger.LogInformation("The dummy session manager is in use");
    }

    public void SignInGamerAccount()
    {
        _logger.LogInformation("The client would have logged in an interactive session for the gamer account now");
    }

    public void SignInTournamentAccount()
    {
        _logger.LogInformation("The client would have logged in an interactive session for the tournament account now");
    }

    public void SignOut()
    {
        _logger.LogInformation("The client would have logged out an the current interactive session now");
    }

    public void ClearAutoLogonCredentials()
    {
        _logger.LogInformation("The client would have cleared the autologon credentials now");
    }
}