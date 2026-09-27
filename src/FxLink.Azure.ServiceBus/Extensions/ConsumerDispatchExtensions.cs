using FxLink.Abstractions;
using FxLink.Azure.ServiceBus.Registries;
using FxLink.Registries;

namespace FxLink.Azure.ServiceBus.Extensions;

public static class ConsumerDispatchExtensions
{
    extension<TConsumer>(IConsumerConfigurator<TConsumer> configurator) where TConsumer : IConsumer
    {
        // Overrides the subscription a consumer reads through — otherwise derived from the consumer type
        // (see ServiceBusNamingExtensions.GetSubscriptionName). Names over 50 characters are shortened.
        public void ReceivedEndpoint(string endpoint)
        {
            ArgumentException.ThrowIfNullOrEmpty(endpoint);
            var consumerConfigurator = (AbstractConsumerConfigurator)configurator;
            var definition = new ServiceBusReceiveEndpointDefinition();
            definition.SetReceiveEndpoint(endpoint);
            consumerConfigurator.AddConfigurator(typeof(TConsumer), definition);
        }
    }
}
