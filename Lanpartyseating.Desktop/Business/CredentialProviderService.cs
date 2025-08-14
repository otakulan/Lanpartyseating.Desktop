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

    public void StoreCredentials(string username, string password, string? domain = null)
    {
        _logger.LogInformation("Storing credentials for user: {Username} (domain: {Domain}, password length: {PasswordLength})", 
            username, domain ?? "local", password?.Length ?? 0);
        
        // Lazy resolve the named pipe service to avoid circular dependency
        var namedPipeServerService = _serviceProvider.GetRequiredService<INamedPipeServerService>();
        
        // Store credentials in the pipe server for when credential provider requests them
        namedPipeServerService.StoreCredentials(username, password ?? "", domain ?? "");
        
        _logger.LogInformation("Credentials stored for credential provider");
    }

    public async Task TriggerLoginAsync()
    {
        _logger.LogInformation("Triggering credential provider login");
        
        // Lazy resolve the named pipe service to avoid circular dependency
        var namedPipeServerService = _serviceProvider.GetRequiredService<INamedPipeServerService>();
        
        // Send trigger login message to credential provider
        await namedPipeServerService.TriggerLoginAsync();
        
        _logger.LogInformation("Login trigger sent to credential provider");
    }
}
