using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.Serialization;

namespace FxLink.Extensions;

/// <summary>
/// Adds the "hydrate from object values" overloads on top of <see cref="IPublisher"/>,
/// <see cref="IResponse"/>, and <see cref="IRequester{TMessage}"/> — each one builds the strongly
/// typed message via <see cref="MessageContractActivator.CreateFrom{TMessage}"/> and delegates to
/// the corresponding typed-message overload.
/// </summary>
public static class MessageContractExtensions
{
    extension(IPublisher publisher)
    {
        public Task PublishAsync<TMessage>(object values, IPublisherContext context,
            CancellationToken token = default) where TMessage : class =>
            publisher.PublishAsync(MessageContractActivator.CreateFrom<TMessage>(values), context, token);

        public Task PublishAsync<TMessage>(object values,
            CancellationToken token = default) where TMessage : class =>
            publisher.PublishAsync(MessageContractActivator.CreateFrom<TMessage>(values), token);
    }

    extension(IResponse response)
    {
        public Task ResponseAsync<TResponse>(object values, CancellationToken token = default)
            where TResponse : class =>
            response.ResponseAsync(MessageContractActivator.CreateFrom<TResponse>(values), token);
    }
}