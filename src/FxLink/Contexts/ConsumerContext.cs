using FxLink.Abstractions;
using FxLink.Statics;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Contexts;

public sealed class ConsumerContext<TMessage>(TMessage message, Guid correlationId, Dictionary<string, object> headers)
    : AbstractContext(correlationId, headers), IConsumerContext<TMessage> where TMessage : class
{
    public TMessage Message { get; } = message;

    public async Task ResponseAsync<TResponse>(TResponse message, CancellationToken token = default)
        where TResponse : class
    {
        var services = ServiceProviderAmbient.Services;
        var client = services.GetService<IClient<TResponse>>();
        if (client is null) return;
        await client.SendAsync(message, new ResponseContext(CorrelationId, Headers), token);
    }
}