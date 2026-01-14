namespace Lanpartyseating.Desktop.Business;

public interface ICredentialProviderPipeService
{
    Task TriggerLoginAsync(string username, string password, string? domain = null);
}
