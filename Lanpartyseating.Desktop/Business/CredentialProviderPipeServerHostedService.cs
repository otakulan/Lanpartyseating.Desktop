using Lanpartyseating.Desktop.Abstractions;
using Microsoft.Extensions.Logging;

namespace Lanpartyseating.Desktop.Business;

public class CredentialProviderPipeServerHostedService : BasePipeServerHostedService, ICredentialProviderPipeService
{
    protected override string PipeName => "Lanpartyseating.Desktop";

    public CredentialProviderPipeServerHostedService(ILogger<CredentialProviderPipeServerHostedService> logger)
        : base(logger)
    {
    }

    protected override Task HandleMessageAsync(BaseMessage message, StreamWriter writer, CancellationToken stoppingToken)
    {
        switch (message)
        {
            case CredentialProviderConnected credProviderConnected:
                Logger.LogInformation("Credential provider connected from process {ProcessId} at {Timestamp}",
                    credProviderConnected.ProcessId, credProviderConnected.Timestamp);
                break;

            default:
                Logger.LogWarning("{PipeName}: Received an unknown message type: {Type}", PipeName, message.GetType().Name);
                break;
        }

        return Task.CompletedTask;
    }

    public async Task TriggerLoginAsync(string username, string password, string? domain = null)
    {
        Logger.LogInformation("Triggering credential provider login for user: {Username} (domain: {Domain}) - Password length: {PasswordLength}",
            username, string.IsNullOrEmpty(domain) ? "local" : domain, password?.Length ?? 0);

        if (!IsConnected)
        {
            Logger.LogWarning("{PipeName}: No client is connected to send a message.", PipeName);
            return;
        }

        var triggerLoginRequest = new TriggerLoginRequest
        {
            Username = username,
            Password = password ?? "",
            Domain = domain
        };

        await SendMessageAsync(triggerLoginRequest, CancellationToken.None);
        Logger.LogInformation("Trigger login message with credentials sent to credential provider");
    }
}
