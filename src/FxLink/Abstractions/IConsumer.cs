namespace FxLink.Abstractions;

public interface IConsumer;

public interface IConsumer<in TMessage> : IConsumer where TMessage : class
{
    Task ConsumeAsync(IConsumerContext<TMessage> context, CancellationToken token = default);
}