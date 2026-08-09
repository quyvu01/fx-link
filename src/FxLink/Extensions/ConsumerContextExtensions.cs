using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Extensions;

public static class ConsumerContextExtensions
{
    extension(IConsumerContext consumerContext)
    {
        public async Task<IResponseContext<TResponse>> RequestAsync<TMessage, TResponse>(TMessage message,
            Action<IRequestContext> contextOptions, CancellationToken token = default)
            where TMessage : class where TResponse : class
        {
            var services = consumerContext.GetPayload<IServiceProvider>();
            var requester = services.GetRequiredService<IRequester<TMessage>>();
            return await requester.RequestAsync<TResponse>(message, contextOptions, token);
        }

        public Task<IResponseContext<TResponse>> RequestAsync<TMessage, TResponse>(TMessage message,
            CancellationToken token = default) where TMessage : class where TResponse : class
            => consumerContext.RequestAsync<TMessage, TResponse>(message, null, token);

        public Task<IResponseContext<TResponse>> RequestAsync<TMessage, TResponse>(object values,
            Action<IRequestContext> contextOptions, CancellationToken token = default)
            where TMessage : class where TResponse : class
            => consumerContext.RequestAsync<TMessage, TResponse>(MessageContractActivator.CreateFrom<TMessage>(values),
                contextOptions, token);

        public Task<IResponseContext<TResponse>> RequestAsync<TMessage, TResponse>(object values,
            CancellationToken token = default) where TMessage : class where TResponse : class
            => consumerContext.RequestAsync<TMessage, TResponse>(values, null, token);
    }
}