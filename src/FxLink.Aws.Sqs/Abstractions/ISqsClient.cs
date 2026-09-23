namespace FxLink.Aws.Sqs.Abstractions;

// What SqsClientConnector<TMessage> needs from SqsClient — mirrors IRabbitMqClient's role
// (publish + topology lookups), kept as a thin interface so the connector depends on the
// contract, not the IMessageBrokerConnector implementation itself.
internal interface ISqsClient
{
    // Throws if messageType has no topic — StartAsync creates one for every message type in
    // IMessageKeys.GetMessageKeys(), so this only fires if a message is published before
    // StartAsync has run, or for a type that was never registered for this transport.
    string GetTopicArn(Type messageType);

    Task PublishAsync(string topicArn, string messageBody, string messageTypeName,
        CancellationToken token = default);
}
