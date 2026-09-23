using FxLink.Abstractions;
using FxLink.Aws.Sqs.Registries;
using FxLink.Registries;

namespace FxLink.Aws.Sqs.Extensions;

public static class ConsumerDispatchExtensions
{
    extension<TConsumer>(IConsumerConfigurator<TConsumer> configurator) where TConsumer : IConsumer
    {
        // Overrides the SQS queue name a consumer's messages are read from — otherwise derived
        // from the consumer type (see SqsNamingExtensions.GetQueueName).
        public void ReceivedEndpoint(string endpoint)
        {
            ArgumentException.ThrowIfNullOrEmpty(endpoint);
            var consumerConfigurator = (AbstractConsumerConfigurator)configurator;
            var definition = new SqsReceiveEndpointDefinition();
            definition.SetReceiveEndpoint(endpoint);
            consumerConfigurator.AddConfigurator(typeof(TConsumer), definition);
        }
    }
}
