using System.Text.Json;
using FxLink.Abstractions;
using FxLink.Azure.ServiceBus.Abstractions;
using FxLink.Configurators;
using FxLink.Contexts;
using FxLink.Entities;

namespace FxLink.Azure.ServiceBus.Implementations;

/// <summary>
/// Default IDelayMessageProvider for FxLink.Azure.ServiceBus, backed by Service Bus scheduled messages (no upper
/// limit on the delay). Registered via IConfigurator.UseServiceBusDelayScheduler().
/// </summary>
internal sealed class ServiceBusDelayMessageProvider(IServiceBusMessagingClient client) : IDelayMessageProvider
{
    public Task PublishDelayedAsync<TMessage>(TMessage message, IContext context, long delayInMs,
        CancellationToken cancellationToken = default) where TMessage : class
    {
        var envelope = new Envelope<TMessage>(message, context);
        var serializedMessage = JsonSerializer.Serialize(envelope, DistributedConfigurators.JsonSerializerOptions);
        // No target subscription: a delayed publish still fans out to every subscriber.
        return client.PublishAsync(client.GetTopicName(typeof(TMessage)), serializedMessage,
            typeof(TMessage).AssemblyQualifiedName,
            new OutgoingOptions(MessageId: context.MessageId,
                ScheduledEnqueueTime: DateTimeOffset.UtcNow.AddMilliseconds(delayInMs)), cancellationToken);
    }
}
