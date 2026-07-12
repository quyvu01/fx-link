using FxLink.Abstractions.Contexts;

namespace FxLink.Abstractions;

public interface IConsumerConnector<in TMessage> where TMessage : class
{
    Task ConsumeAsync(IConsumerContext<TMessage> context, Type consumerType, CancellationToken token = default);
}