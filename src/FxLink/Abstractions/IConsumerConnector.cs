using FxLink.Contexts;

namespace FxLink.Abstractions;

public interface IConsumerConnector<in TMessage> where TMessage : class
{
    Task ConsumeAsync(IConsumeContext<TMessage> context, Type consumerType, CancellationToken token = default);
}