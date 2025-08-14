namespace Lanpartyseating.Desktop.Business;

public interface ISessionManager
{
    public Task SignInGamerAccountAsync();
    public Task SignInTournamentAccountAsync();
    public void SignOut();
    public void ClearAutoLogonCredentials();
}