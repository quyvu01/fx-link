using System.Text.Json;
using FxLink.Abstractions;
using FxLink.Configurators;
using FxLink.Contexts;
using FxLink.Delegates;
using FxLink.Entities;

namespace FxLink.InternalPipelineBehaviors;

internal sealed class OutboxPublisherPipelineBehavior<TMessage>(IOutboxStoreResolver<TMessage> storeResolver)
    : IPublisherPipelineBehavior<TMessage> where TMessage : class
{
    public async Task PublishAsync(TMessage message, IPublishContext context, PublisherHandlerDelegate next,
        CancellationToken token = default)
    {
        var store = storeResolver.GetOutboxStore();
        if (store is null)
        {
            await next.Invoke(token);
            return;
        }

        var serializeOptions = DistributedConfigurators.JsonSerializerOptions;
        var outboxMessage = new OutboxMessage
        {
            PartitionKey = context.CorrelationId,
            MessageType = typeof(TMessage).AssemblyQualifiedName,
            Payload = JsonSerializer.Serialize(message, serializeOptions),
            SerializedHeaders = JsonSerializer.Serialize(context.Headers, serializeOptions),
            DelayTime = context.DelayTime,
            TimeToLive = context.TimeToLive,
            ScheduleToken = context.ScheduleToken,
            RequesterId = context.RequesterId
        };

        await store.EnqueueAsync(outboxMessage, token);
    }
}