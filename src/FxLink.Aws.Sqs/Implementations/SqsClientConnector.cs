using System.Text.Json;
using Amazon.SQS.Model;
using FxLink.Abstractions;
using FxLink.Configurators;
using FxLink.Contexts;
using FxLink.Entities;
using FxLink.Aws.Sqs.Abstractions;
using FxLink.Statics;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Aws.Sqs.Implementations;

// Analogous to AbstractRabbitMqConnector — lets a future poll loop resolve
// IClientConnector<>.MakeGenericType(messageType) generically and call into the receive path
// without knowing TMessage at compile time.
internal abstract class AbstractSqsConnector
{
    public abstract Task ProcessMessageReceivedAsync(Message message, Type consumerType,
        CancellationToken token = default);
}

internal sealed class SqsClientConnector<TMessage>(ISqsClient client, IServiceProvider serviceProvider) :
    AbstractSqsConnector, IClientConnector<TMessage> where TMessage : class
{
    public async Task SendAsync(TMessage message, IContext context, CancellationToken token = default)
    {
        var deliveryKind = context.Headers.Get<string>(DistributedConfigurators.Headers.DeliveryKindKey);

        // Retry/dead-letter wire mechanics and delayed publish aren't implemented yet for this
        // transport — fail loudly instead of silently sending as a plain publish.
        if (deliveryKind is DistributedConfigurators.DeliveryKinds.Retry or DistributedConfigurators.DeliveryKinds.DeadLetter)
            throw new NotSupportedException($"{deliveryKind} delivery is not implemented yet for the SQS transport.");
        if (context is IPublishContext { DelayTime: not null })
            throw new NotSupportedException("Delayed publish is not implemented yet for the SQS transport.");
        if (context is IRequestContext or IResponseContext)
            throw new NotSupportedException("Request/response is not implemented yet for the SQS transport.");

        var envelope = new Envelope<TMessage>(message, context);
        var serializedMessage = JsonSerializer.Serialize(envelope, DistributedConfigurators.JsonSerializerOptions);

        var topicArn = client.GetTopicArn(typeof(TMessage));
        await client.PublishAsync(topicArn, serializedMessage, typeof(TMessage).AssemblyQualifiedName, token);
    }

    public override async Task ProcessMessageReceivedAsync(Message message, Type consumerType,
        CancellationToken token = default)
    {
        var messageDefinition = serviceProvider.GetService<IMessageDefinition<TMessage>>();

        var envelope = (messageDefinition is { MessageConfigurator.IsRawJsonSerializer: true }) switch
        {
            false => JsonSerializer.Deserialize<ConsumerContextEnvelope<TMessage>>(message.Body,
                DistributedConfigurators.JsonSerializerOptions),
            _ => new ConsumerContextEnvelope<TMessage>
            {
                Message = JsonSerializer.Deserialize<TMessage>(message.Body,
                    messageDefinition.MessageConfigurator.RawJsonSerializerOptions),
                Context = new ConsumerContextSerializable
                {
                    MessageId = Id.New(),
                    CorrelationId = Id.New(),
                    Headers = new HeaderBag(),
                    SentTime = DateTime.UtcNow,
                    HostInfo = null,
                    TimeToLive = null
                }
            }
        };

        if (envelope is null) return;

        using var scope = serviceProvider.CreateScope();
        var serverConnector = scope.ServiceProvider.GetRequiredService<IConsumerConnector<TMessage>>();
        var consumerContext = new ConsumeContext<TMessage>(envelope.Message, envelope.Context.Headers,
            envelope.Context.CorrelationId, envelope.Context.RequesterId, envelope.Context.SentTime,
            envelope.Context.HostInfo, envelope.Context.TimeToLive, envelope.Context.MessageId);
        await serverConnector.ConsumeAsync(consumerContext, consumerType, token);
    }
}
