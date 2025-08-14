using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Lanpartyseating.Desktop.Abstractions;

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
        
        // Lazy resolve the named pipe service to avoid circular dependency
        var namedPipeServerService = _serviceProvider.GetRequiredService<INamedPipeServerService>();
        
        // Send trigger login message with credentials directly to credential provider
        await namedPipeServerService.TriggerLoginAsync(username, password ?? "", domain);
        
        _logger.LogInformation("Login trigger with credentials sent to credential provider");
    }
}
