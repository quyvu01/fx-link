using FxLink.Abstractions.Contexts;

namespace FxLink.RabbitMq.Abstractions;

internal interface IPublishMessage
{
    Task PublishMessageAsync<TRequest>(TRequest request, IPublisherContext context, CancellationToken token = default);
}