using FxLink.Contexts;

namespace FxLink.Abstractions;

public interface IPublisher : IMessageAction
{
    Task PublishAsync<TMessage>(TMessage message, Action<IPublisherContext> contextOptions,
        CancellationToken token = default) where TMessage : class;

    Task PublishAsync<TMessage>(TMessage message, CancellationToken token = default) where TMessage : class;
}