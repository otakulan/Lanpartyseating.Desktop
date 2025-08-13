using Lanpartyseating.Desktop.Abstractions;

namespace Lanpartyseating.Desktop.Business;

public interface ICredentialProviderService
{
    Task TriggerLoginAsync(string username, string password, string? domain = null, CancellationToken cancellationToken = default);
}
