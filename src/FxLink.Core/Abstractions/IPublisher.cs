namespace FxLink.Core.Abstractions;

public interface IPublisher
{
    Task PublishAsync<TMessage>(TMessage message, IPublisherContext context, CancellationToken token = default)
        where TMessage : class;

    Task PublishAsync<TMessage>(TMessage message, CancellationToken token = default) where TMessage : class;
}