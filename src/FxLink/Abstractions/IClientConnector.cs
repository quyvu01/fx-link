using FxLink.Abstractions.Contexts;

namespace FxLink.Abstractions;

public interface IClientConnector<in TMessage> : IBus where TMessage : class
{
    Task SendAsync(TMessage message, IContext context, CancellationToken token = default);
}