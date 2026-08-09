using FxLink.Contexts;

namespace FxLink.Abstractions;

internal interface IClientConnector<in TMessage> : IBus where TMessage : class
{
    Task SendAsync(TMessage message, IContext context, CancellationToken token = default);
}