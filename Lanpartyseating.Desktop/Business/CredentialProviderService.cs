using Lanpartyseating.Desktop.Abstractions;
using Microsoft.Extensions.Logging;

namespace Lanpartyseating.Desktop.Business;

public class CredentialProviderService : ICredentialProviderService
{
    private readonly ILogger<CredentialProviderService> _logger;
    private readonly INamedPipeServerService _namedPipeServerService;

    public CredentialProviderService(ILogger<CredentialProviderService> logger, INamedPipeServerService namedPipeServerService)
    {
        _logger = logger;
        _namedPipeServerService = namedPipeServerService;
    }

    public async Task TriggerLoginAsync(string username, string password, string? domain = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Triggering login for user: {Username}", username);
        
        var triggerMessage = new TriggerLoginRequest
        {
            Username = username,
            Password = password,
            Domain = domain ?? ""
        };

        await _namedPipeServerService.SendMessageAsync(triggerMessage, cancellationToken);
        _logger.LogInformation("Login trigger sent to credential provider");
    }
}
