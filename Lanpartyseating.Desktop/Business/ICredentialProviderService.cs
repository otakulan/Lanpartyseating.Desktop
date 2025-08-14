namespace Lanpartyseating.Desktop.Business;

public interface ICredentialProviderService
{
    void StoreCredentials(string username, string password, string? domain = null);
    Task TriggerLoginAsync();
}
