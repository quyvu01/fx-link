using System.Text;
using System.Text.Json;
using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.Entities;
using FxLink.RabbitMq.Abstractions;
using Microsoft.Extensions.Logging;

namespace FxLink.RabbitMq.Implementations;

internal class RabbitMqMessageProcessor<TMessage>(
    IPublishMessage publishMessage,
    IConsumeMessage consumeMessage,
    ILogger<RabbitMqMessageProcessor<TMessage>> logger)
    : IMessageProcessor<TMessage> where TMessage : class
{
    public async Task PushMessageAsync(TMessage message, IContext context, CancellationToken token = default)
    {
        if (context is IPublisherContext publisherContext)
        {
            await publishMessage.PublishMessageAsync(message, publisherContext, token);
            return;
        }
    }

    public async IAsyncEnumerable<MessageData<TMessage>> MessagesProcessingAsync()
    {
        var channel = consumeMessage.MessageChannel();
        await foreach (var messageData in channel.Reader.ReadAllAsync())
        {
            logger.LogInformation("Message type: {@MessageType}", messageData.MessageType);
            if (messageData.MessageType != typeof(TMessage).AssemblyQualifiedName) continue;
            var message = JsonSerializer.Deserialize<TMessage>(Encoding.UTF8.GetString(messageData.MessageBody));
            yield return new MessageData<TMessage>(message,
                new PublisherContext(messageData.CorrelationId, messageData.Headers), messageData.Token);
        }
    }
}