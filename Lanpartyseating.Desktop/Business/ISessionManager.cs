namespace Lanpartyseating.Desktop.Business;

public interface ISessionManager
{
    public void SignInGamerAccount();
    public void SignInTournamentAccount();
    public void SignOut();
    public void ClearAutoLogonCredentials();
}