using FxLink.Abstractions.Contexts;

namespace FxLink.Abstractions;

public interface IServerConnector<in TMessage> where TMessage : class
{
    // Task ConsumeAsync(IConsumerContext<TMessage> context, CancellationToken token = default);
    Task ConsumeAsync(IConsumerContext<TMessage> context, Type consumerType, CancellationToken token = default);
}