using FxLink.Core.Abstractions;
using FxLink.Core.Statics;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Core.ContextImplementations;

public sealed class ConsumerContext<TMessage>(TMessage message, Guid correlationId, Dictionary<string, object> headers)
    : AbstractContext(correlationId, headers), IConsumerContext<TMessage> where TMessage : class
{
    public TMessage Message { get; } = message;

    public async Task ResponseAsync<TResponse>(TResponse message, CancellationToken token = default)
        where TResponse : class
    {
        var services = InternalServiceProvider.Services;
        var client = services.GetService<IClient<TResponse>>();
        if (client is null) return;
        await client.SendAsync(message, new ResponseContext(CorrelationId, Headers), token);
    }
}