using Lanpartyseating.Desktop.Abstractions;

namespace Lanpartyseating.Desktop;

public interface INamedPipeServerService
{
    Task SendMessageAsync<T>(T message, CancellationToken cancellationToken) where T : BaseMessage;
    Task TriggerLoginAsync(string username, string password, string? domain = null);
}