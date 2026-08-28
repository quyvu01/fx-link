using System.Text.Json;
using FxLink.Abstractions;
using FxLink.Configurators;
using FxLink.Contexts;
using FxLink.Delegates;
using FxLink.Entities;

namespace FxLink.InternalPipelineBehaviors;

// Write-path half of the Outbox pattern. If TMessage resolves to a configured IOutboxStore, the
// publish is captured as a row instead of reaching the broker — the dispatcher (not yet built) is
// what actually sends it later. If no store resolves (Outbox never configured for TMessage), this
// is a no-op passthrough to next(), so Outbox stays fully opt-in per message type.
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

        // PartitionKey = CorrelationId: a fresh publish gets its own key (dispatched independently,
        // in parallel with everything else); a publish that inherited CorrelationId from an in-flight
        // consume (new PublishContext(context)) shares its partition and is dispatched in order with
        // whatever else that handler published — see IOutboxStore's ordering guarantee.
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

        // Deliberately not calling next() — the real broker publish happens later, from the
        // dispatcher, once this row (and whatever business write shares its unit-of-work) commits.
        await store.EnqueueAsync(outboxMessage, token);
    }
}