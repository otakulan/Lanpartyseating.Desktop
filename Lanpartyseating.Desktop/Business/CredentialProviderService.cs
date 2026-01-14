using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Lanpartyseating.Desktop.Business;

public class CredentialProviderService : ICredentialProviderService
{
    private readonly ILogger<CredentialProviderService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public CredentialProviderService(ILogger<CredentialProviderService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task TriggerLoginAsync(string username, string password, string? domain = null)
    {
        _logger.LogInformation("Triggering credential provider login for user: {Username} (domain: {Domain}, password length: {PasswordLength})",
            username, domain ?? "local", password?.Length ?? 0);

        // Lazy resolve the credential provider pipe service to avoid circular dependency
        var credentialProviderPipeService = _serviceProvider.GetRequiredService<ICredentialProviderPipeService>();

        // Send trigger login message with credentials directly to credential provider
        await credentialProviderPipeService.TriggerLoginAsync(username, password ?? "", domain);

        _logger.LogInformation("Login trigger with credentials sent to credential provider");
    }
}
