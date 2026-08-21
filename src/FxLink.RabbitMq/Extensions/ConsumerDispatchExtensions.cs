using FxLink.Abstractions;
using FxLink.RabbitMq.Registries;
using FxLink.Registries;

namespace FxLink.RabbitMq.Extensions;

public static class ConsumerDispatchExtensions
{
    extension<TConsumer>(IConsumerConfigurator<TConsumer> configurator) where TConsumer : IConsumer
    {
        public void PrefetchCount(ushort prefetchCount)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(prefetchCount, 0);
            var consumerConfigurator = (AbstractConsumerConfigurator)configurator;
            var currentConfigurator = consumerConfigurator
                .GetConfigurator<IConsumerDispatchDefinition>(typeof(TConsumer));
            if (currentConfigurator is null)
            {
                var dispatchDefinition = new ConsumerDispatchDefinition();
                dispatchDefinition.SetPrefetchCount(prefetchCount);
                consumerConfigurator.AddConfigurator(typeof(TConsumer), dispatchDefinition);
            }

            ((ConsumerDispatchDefinition)currentConfigurator)?.SetPrefetchCount(prefetchCount);
        }

        public void ConcurrentMessageLimit(ushort limitCount)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(limitCount, 0);
            var consumerConfigurator = (AbstractConsumerConfigurator)configurator;
            var currentConfigurator = consumerConfigurator
                .GetConfigurator<IConsumerDispatchDefinition>(typeof(TConsumer));
            if (currentConfigurator is null)
            {
                var dispatchDefinition = new ConsumerDispatchDefinition();
                dispatchDefinition.SetConcurrentMessageLimit(limitCount);
                consumerConfigurator.AddConfigurator(typeof(TConsumer), dispatchDefinition);
            }

            ((ConsumerDispatchDefinition)currentConfigurator)?.SetConcurrentMessageLimit(limitCount);
        }

        public void ReceivedEndpoint(string endpoint, Action<IReceivedEndpointConfigurator> options = null)
        {
            var consumerConfigurator = (AbstractConsumerConfigurator)configurator;
            var currentConfigurator = consumerConfigurator
                .GetConfigurator<IReceiveEndpointDefinition>(typeof(TConsumer));
            var receivedEndpointConfigurator = new ReceivedEndpointConfigurator();
            options?.Invoke(receivedEndpointConfigurator);
            if (currentConfigurator is null)
            {
                var newConfig = new ReceiveEndpointDefinition();
                SetConfig(newConfig);
                consumerConfigurator.AddConfigurator(typeof(TConsumer), newConfig);
            }

            SetConfig(currentConfigurator);
            return;

            void SetConfig(IReceiveEndpointDefinition config)
            {
                if (config is not ReceiveEndpointDefinition definition) return;
                definition.SetReceivedEndpoint(endpoint);
                if (options is not null)
                {
                    definition.SetAutoDelete(receivedEndpointConfigurator.AutoDelete);
                }
            }
        }
    }
}