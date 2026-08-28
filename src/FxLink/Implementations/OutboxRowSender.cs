using System.Text.Json;
using FxLink.Configurators;
using FxLink.Contexts;
using FxLink.Entities;
using FxLink.InternalPipelineBehaviors;
using FxLink.PipelineBehaviors;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Implementations;

internal abstract class AbstractOutboxRowSender
{
    internal abstract Task SendAsync(object message, IPublishContext context, CancellationToken token);
}

internal sealed class OutboxTypedRowSender<TMessage>(IServiceProvider serviceProvider) : AbstractOutboxRowSender
    where TMessage : class
{
    internal override async Task SendAsync(object message, IPublishContext context, CancellationToken token)
    {
        using var scope = serviceProvider.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<PublisherPipelineBehaviorOrchestrator<TMessage>>();
        await orchestrator.ExecuteAsync((TMessage)message, context,
            exclude: b => b is OutboxPublisherPipelineBehavior<TMessage>, token);
    }
}

internal sealed class OutboxRowSender(IServiceProvider serviceProvider)
{
    public async Task SendAsync(OutboxMessage row, CancellationToken token = default)
    {
        var serializerOptions = DistributedConfigurators.JsonSerializerOptions;
        var messageType = Type.GetType(row.MessageType)
            ?? throw new InvalidOperationException(
                $"Cannot resolve outbox message type '{row.MessageType}' for outbox message {row.Id}.");

        var message = JsonSerializer.Deserialize(row.Payload, messageType, serializerOptions)
            ?? throw new InvalidOperationException($"Outbox message {row.Id} deserialized to null.");

        var headers = string.IsNullOrEmpty(row.SerializedHeaders)
            ? new HeaderBag()
            : JsonSerializer.Deserialize<IHeaders>(row.SerializedHeaders, serializerOptions);

        var context = new PublishContext(headers, row.PartitionKey)
        {
            DelayTime = row.DelayTime,
            TimeToLive = row.TimeToLive,
            ScheduleToken = row.ScheduleToken,
            RequesterId = row.RequesterId
        };

        var senderType = typeof(OutboxTypedRowSender<>).MakeGenericType(messageType);
        var sender = (AbstractOutboxRowSender)serviceProvider.GetRequiredService(senderType);
        await sender.SendAsync(message, context, token);
    }
}
