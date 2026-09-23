using FxLink.Registries;

namespace FxLink.Aws.Sqs.Registries;

// Mirrors RabbitMq's IReceiveEndpointDefinition, minus AutoDelete — SQS queues have no
// AMQP-style "auto-delete when the last consumer disconnects" attribute to map it to.
public interface ISqsReceiveEndpointDefinition : IOption
{
    string ReceiveEndpoint { get; }
}

internal sealed class SqsReceiveEndpointDefinition : ISqsReceiveEndpointDefinition
{
    public string ReceiveEndpoint { get; private set; }
    internal void SetReceiveEndpoint(string receiveEndpoint) => ReceiveEndpoint = receiveEndpoint;
}
