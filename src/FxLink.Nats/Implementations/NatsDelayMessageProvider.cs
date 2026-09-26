using System.Text.Json;
using FxLink.Abstractions;
using FxLink.Configurators;
using FxLink.Contexts;
using FxLink.Entities;
using FxLink.Nats.Abstractions;

namespace FxLink.Nats.Implementations;

/// <summary>
/// Default IDelayMessageProvider for FxLink.Nats, backed by NATS message scheduling (server 2.12+).
/// Registered via IConfigurator.UseNatsDelayScheduler().
/// </summary>
internal sealed class NatsDelayMessageProvider(INatsMessagingClient client) : IDelayMessageProvider
{
    public Task PublishDelayedAsync<TMessage>(TMessage message, IContext context, long delayInMs,
        CancellationToken cancellationToken = default) where TMessage : class
    {
        var envelope = new Envelope<TMessage>(message, context);
        var serializedMessage = JsonSerializer.Serialize(envelope, DistributedConfigurators.JsonSerializerOptions);
        return client.PublishScheduledAsync(client.GetSubject(typeof(TMessage)), serializedMessage,
            typeof(TMessage).AssemblyQualifiedName, TimeSpan.FromMilliseconds(delayInMs), cancellationToken);
    }
}
