using FxLink.Contexts;

namespace FxLink.Abstractions;

public interface IConsumer : IMessageAction;

public interface IConsumer<in TMessage> : IConsumer where TMessage : class
{
    Task ConsumeAsync(IConsumeContext<TMessage> context, CancellationToken token = default);
}