using FxLink.Abstractions.Contexts;

namespace FxLink.Abstractions;

public interface IServer<in TMessage> where TMessage : class
{
    Task ConsumeAsync(IConsumerContext<TMessage> context, CancellationToken token = default);
}