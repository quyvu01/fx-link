namespace FxLink.Core.Abstractions;

public interface IClient<in TMessage> : IBus where TMessage : class
{
    Task SendAsync(TMessage message, IContext context, CancellationToken token = default);
}