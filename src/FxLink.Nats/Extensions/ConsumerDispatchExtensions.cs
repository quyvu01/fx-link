using FxLink.Abstractions;
using FxLink.Nats.Registries;
using FxLink.Registries;

namespace FxLink.Nats.Extensions;

public static class ConsumerDispatchExtensions
{
    extension<TConsumer>(IConsumerConfigurator<TConsumer> configurator) where TConsumer : IConsumer
    {
        // Overrides the JetStream durable consumer name a consumer reads through — otherwise
        // derived from the consumer type (see NatsNamingExtensions.GetConsumerName). Instances
        // sharing a name share one consumer and split its messages between them.
        public void ReceivedEndpoint(string endpoint)
        {
            ArgumentException.ThrowIfNullOrEmpty(endpoint);
            var consumerConfigurator = (AbstractConsumerConfigurator)configurator;
            var definition = new NatsReceiveEndpointDefinition();
            definition.SetReceiveEndpoint(endpoint);
            consumerConfigurator.AddConfigurator(typeof(TConsumer), definition);
        }
    }
}
