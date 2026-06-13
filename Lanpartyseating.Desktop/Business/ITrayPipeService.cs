using Lanpartyseating.Desktop.Abstractions;

namespace Lanpartyseating.Desktop.Business;

public interface ITrayPipeService
{
    Task SendMessageAsync<T>(T message, CancellationToken cancellationToken) where T : BaseMessage;
}
