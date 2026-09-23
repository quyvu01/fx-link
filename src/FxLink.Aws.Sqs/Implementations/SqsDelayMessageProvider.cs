using System.Text.Json;
using Amazon.SQS.Model;
using FxLink.Abstractions;
using FxLink.Aws.Sqs.Abstractions;
using FxLink.Configurators;
using FxLink.Contexts;
using FxLink.Entities;

namespace FxLink.Aws.Sqs.Implementations;

/// <summary>
/// Default IDelayMessageProvider for FxLink.Aws.Sqs, backed by SQS's native
/// SendMessageRequest.DelaySeconds. Registered via IConfigurator.UseSqsDelayScheduler().
/// </summary>
internal sealed class SqsDelayMessageProvider(ISqsClient client, ISqsConnection sqsConnection) : IDelayMessageProvider
{
    // SQS's own hard limit — unlike RabbitMq's delayed-message-exchange plugin, there is no way
    // to ask SQS for a longer delay short of scheduling a normal (non-delayed) publish yourself
    // later (e.g. via EventBridge Scheduler, a Quartz/Hangfire-backed IDelayMessageProvider, ...).
    private const int MaxDelaySeconds = 900;

    public async Task PublishDelayedAsync<TMessage>(TMessage message, IContext context, long delayInMs,
        CancellationToken cancellationToken = default) where TMessage : class
    {
        var delaySeconds = (int)Math.Ceiling(delayInMs / 1000.0);
        if (delaySeconds > MaxDelaySeconds)
            throw new NotSupportedException(
                $"Cannot delay {typeof(TMessage).Name} by {delaySeconds}s: SQS's DelaySeconds caps at " +
                $"{MaxDelaySeconds}s (15 minutes). Register a different IDelayMessageProvider " +
                "(e.g. backed by EventBridge Scheduler, Quartz, Hangfire, ...) for longer delays.");

        // SNS PublishRequest has no delay parameter — only SQS's own SendMessageRequest does — so
        // this bypasses the topic entirely and sends directly to every queue that would otherwise
        // have received the message via that topic's subscription (see
        // SqsClient.GetQueueUrlsForMessageType). Nothing further downstream needs to know the
        // difference: the wire format (body + "MessageType" attribute) is identical either way.
        var queueUrls = client.GetQueueUrlsForMessageType(typeof(TMessage));
        if (queueUrls.Count == 0) return;

        var envelope = new Envelope<TMessage>(message, context);
        var serializedMessage = JsonSerializer.Serialize(envelope, DistributedConfigurators.JsonSerializerOptions);

        await Task.WhenAll(queueUrls.Select(queueUrl => sqsConnection.Sqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = queueUrl,
            MessageBody = serializedMessage,
            DelaySeconds = delaySeconds,
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["MessageType"] = new() { DataType = "String", StringValue = typeof(TMessage).AssemblyQualifiedName }
            }
        }, cancellationToken)));
    }
}
