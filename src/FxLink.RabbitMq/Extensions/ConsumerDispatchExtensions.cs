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
            var consumerConfigurator = (ConsumerConfigurator<TConsumer>)configurator;
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
            var consumerConfigurator = (ConsumerConfigurator<TConsumer>)configurator;
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
    }
}